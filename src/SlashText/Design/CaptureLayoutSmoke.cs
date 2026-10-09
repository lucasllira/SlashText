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
            LabMotion.SetReduced(overlay, true);
            Window? toolbarHost = null;
            try
            {
                overlay.SetSelectionForEvidence(new Rect(40, 40, 750, 350));
                var toolbar = overlay.ToolbarForEvidence; toolbar.Visibility = Visibility.Visible; LabMotion.SetReduced(toolbar, true);
                toolbarHost = new Window { Content = toolbar, Width = 900, SizeToContent = SizeToContent.Height,
                    WindowStyle = WindowStyle.None, ShowInTaskbar = false };
                toolbarHost.Show(); await SettleAsync();
                var textButton = Descendants(toolbar).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Extrair texto da região");
                Require(textButton.Content is LabIcon { Kind: "ScanText" }, "OCR uses only a text scan icon with an accessible name");
                var clear = Descendants(toolbar).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Limpar marcações");
                Require(!clear.IsEnabled, "History clear is disabled with no annotations");
                Require(new[] { "ÁREA", "ANOTAR", "PRIVACIDADE", "HISTÓRICO", "OCR", "FINALIZAR" }
                    .All(name => Descendants(toolbar).OfType<TextBlock>().Any(t => t.Text == name)), "Toolbar has six labeled blocks");
                foreach (var compact in new[] { false, true })
                {
                    await SettleAsync();
                    overlay.SetDensityForEvidence(compact);
                    toolbarHost.Width = compact ? 530 : 900;
                    overlay.FitToolbarForEvidence(toolbarHost.Width); await SettleAsync();
                    CheckDistribution(overlay);
                    Require(textButton.Visibility == Visibility.Visible, "OCR stays visible in both densities");
                    var finish = Descendants(toolbar).OfType<StackPanel>().Single(p => AutomationProperties.GetName(p) == "FINALIZAR");
                    var finishBounds = finish.TransformToAncestor(toolbar).TransformBounds(new Rect(finish.RenderSize));
                    var ocrBounds = textButton.TransformToAncestor(toolbar).TransformBounds(new Rect(textButton.RenderSize));
                    Require(finishBounds.Left >= ocrBounds.Right, "Finalize stays on right even when other blocks wrap");
                    Require(Descendants(toolbar).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Mais ferramentas").IsVisible == compact,
                        "More appears only when tools are collapsed");
                    Save(toolbar, output, $"{theme}-toolbar-{(compact ? "compact" : "normal")}", toolbarHost.Width);
                    var menu = overlay.OverflowForEvidence();
                    var menuSurface = new Border { Child = menu, CornerRadius = new CornerRadius(10), Padding = new Thickness(8) };
                    menuSurface.SetResourceReference(Border.BackgroundProperty, "Lab.panel");
                    Save(menuSurface, output, $"{theme}-more-{(compact ? "compact" : "normal")}", 310);
                }
                overlay.SetDensityForEvidence(false);
                foreach (var tool in new[] { CaptureAnnotationKind.Pencil, CaptureAnnotationKind.Highlighter,
                    CaptureAnnotationKind.Rectangle, CaptureAnnotationKind.Text, CaptureAnnotationKind.Stamp })
                {
                    overlay.ToolForEvidence(tool).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await SettleAsync();
                    var context = overlay.ContextForEvidence ?? throw new InvalidOperationException("Tool opens its own panel on first click");
                    Save(context, output, $"{theme}-panel-{tool}", context.ActualWidth, preserveLayout: true);
                    Require(!Descendants(toolbar).OfType<Slider>().Any(), "Toolbar has no duplicate persistent property row");
                    if (tool == CaptureAnnotationKind.Pencil)
                    {
                        Descendants(context).OfType<Button>().Single(b => b.Name == "OpenInkRgb").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var hex = Descendants(context).OfType<TextBox>().Single(t => t.Name == "InkHex"); hex.Text = "#137B53";
                        Descendants(context).OfType<Button>().Single(b => b.Name == "ApplyInkHex").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Descendants(context).OfType<Slider>().Single(s => s.Name == "InkThickness").Value = 9;
                        var drawn = overlay.FinishDragForEvidence(new Point(100, 100), new Point(200, 120));
                        Require(drawn.Argb == System.Drawing.Color.FromArgb(19, 123, 83).ToArgb() && drawn.Thickness == 9,
                            "Panel RGB/thickness reach the actual drawing command");
                    }
                    if (tool == CaptureAnnotationKind.Stamp)
                        Require(Descendants(context).OfType<Image>().Count(i => i.Source is BitmapSource) >= 36,
                            "Emoji panel uses real catalog images");
                }
                foreach (var privacy in new[] { CaptureAnnotationKind.Blur, CaptureAnnotationKind.Pixelate })
                {
                    overlay.ToolForEvidence(privacy)
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await SettleAsync(); overlay.SetDensityForEvidence(false); toolbarHost.Width = 900;
                    overlay.FitToolbarForEvidence(900); await SettleAsync();
                    CheckDistribution(overlay);
                    Require(overlay.ToolForEvidence(privacy).Visibility == Visibility.Visible, "Active privacy tool appears in toolbar");
                    Save(toolbar, output, $"{theme}-toolbar-{privacy}", 900);
                }
                overlay.AddForEvidence(new CaptureAnnotation { Kind = CaptureAnnotationKind.Line, Start = new Point(100, 100), End = new Point(180, 100) });
                var count = overlay.AnnotationCountForEvidence;
                Require(clear.IsEnabled, "History clear enables for annotations"); clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(overlay.AnnotationCountForEvidence == 0 && !clear.IsEnabled, "History trash clears annotations");
                Descendants(toolbar).OfType<Button>().Single(b => AutomationProperties.GetName(b).StartsWith("Desfazer")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(overlay.AnnotationCountForEvidence == count, "Undo restores the marks cleared by trash");
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
                Require(dialog.UseLayoutRounding && dialog.SnapsToDevicePixels &&
                    TextOptions.GetTextFormattingMode(dialog) == TextFormattingMode.Display &&
                    RenderOptions.GetClearTypeHint(dialog.Surface) == ClearTypeHint.Enabled,
                    "Settings uses pixel rounding and text hinting");
                foreach (var text in Descendants(dialog.Surface).OfType<TextBlock>())
                    for (DependencyObject? parent = text; parent is not null && parent != background; parent = VisualTreeHelper.GetParent(parent))
                        Require(parent is not UIElement element || element.Effect is null, "Text is outside the shadow effect subtree");
                Require(Math.Abs(background.ActualHeight - ownerRoot.ActualHeight) < 2 && Math.Abs(background.ActualWidth - ownerRoot.ActualWidth) < 2,
                    "Backdrop covers the entire owner client area, without height/width caps");
                var center = dialog.Surface.TransformToAncestor(background).Transform(new Point(dialog.Surface.ActualWidth / 2, dialog.Surface.ActualHeight / 2));
                Require(Math.Abs(center.X - background.ActualWidth / 2) < 2 && Math.Abs(center.Y - background.ActualHeight / 2) < 2,
                    "Settings surface is centered in normal and maximized owners");
                Save(background, output, $"{theme}-settings-{(maximized ? "maximized" : "normal")}", background.ActualWidth, preserveLayout: true);
                Save(background, output, $"{theme}-settings-{(maximized ? "maximized" : "normal")}-125", background.ActualWidth, preserveLayout: true, scale: 1.25);
                Save(background, output, $"{theme}-settings-{(maximized ? "maximized" : "normal")}-150", background.ActualWidth, preserveLayout: true, scale: 1.5);
                Require(!ModalBackdrop.DismissAt(dialog.Surface, new Point(40, 40), dialog.Close), "Inside click keeps settings open");
                Require(ModalBackdrop.DismissAt(dialog.Surface, new Point(-10, 40), dialog.Close), "Outside click dismisses settings");
                Require(System.Text.Json.JsonSerializer.Serialize(settings) == before, "Backdrop dismissal preserves settings");
                owner.Close();
            }
        }
        CheckPrivacyExports(output);
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: six blocks with Finalize on right, icon-only OCR, history trash/undo, tool panels and real RGB/thickness/Noto, toolbar/menu without duplicates, distinct privacy pixels and PNG exports, normal/compact rendering, full centered backdrop, effect-free settings text, 100/125/150%, safe dismissal, all themes. Mixed-DPI hardware remains manual.");
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
        Require(overlay.ToolForEvidence(CaptureAnnotationKind.Blur).Visibility == Visibility.Visible &&
            overlay.ToolForEvidence(CaptureAnnotationKind.Pixelate).Visibility == Visibility.Visible, "Privacy stays visible in both densities");
        Require(!labels.Contains("Selecionar / mover") && !labels.Any(value => value.StartsWith("Refazer seleção")) && !labels.Contains("Limpar marcações"),
            "Area/history commands are never repeated in overflow");
    }

    private static void CheckPrivacyExports(string output)
    {
        using var source = new System.Drawing.Bitmap(400, 240);
        for (var y = 0; y < source.Height; y++) for (var x = 0; x < source.Width; x++)
            source.SetPixel(x, y, System.Drawing.Color.FromArgb(x * 255 / 400, y * 255 / 240, (x / 7 + y / 7) % 2 == 0 ? 220 : 30));
        source.Save(Path.Combine(output, "privacy-original.png"));
        var annotation = new CaptureAnnotation { Start = new Point(40, 40), End = new Point(360, 200), PrivacyStrength = 18 };
        using var blurDocument = new CaptureEditorDocument(source); using var pixelDocument = new CaptureEditorDocument(source);
        blurDocument.AddAnnotation(annotation with { Kind = CaptureAnnotationKind.Blur }); pixelDocument.AddAnnotation(annotation with { Kind = CaptureAnnotationKind.Pixelate });
        using var blur = blurDocument.Render(); using var pixel = pixelDocument.Render();
        int different = 0, blurFlat = 0, pixelFlat = 0;
        for (var y = 0; y < source.Height; y++) for (var x = 0; x < source.Width; x++)
        {
            if (x < 40 || x >= 360 || y < 40 || y >= 200)
                Require(blur.GetPixel(x, y) == source.GetPixel(x, y) && pixel.GetPixel(x, y) == source.GetPixel(x, y), "Privacy preserves pixels outside its region");
            else if (x > 60 && x < 340 && y > 60 && y < 180)
            {
                if (blur.GetPixel(x, y) != pixel.GetPixel(x, y)) different++;
                if (blur.GetPixel(x, y) == blur.GetPixel(x - 1, y)) blurFlat++;
                if (pixel.GetPixel(x, y) == pixel.GetPixel(x - 1, y)) pixelFlat++;
            }
        }
        Require(different > 10000 && pixelFlat > blurFlat * 2, "Pixelate produces uniform blocks; blur produces smooth transitions");
        foreach (var (name, bitmap) in new[] { ("blur", blur), ("pixelate", pixel) })
        {
            var path = Path.Combine(output, $"privacy-{name}.png"); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            using var exported = new System.Drawing.Bitmap(path);
            Require(exported.Size == source.Size, "Privacy export preserves dimensions");
            for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++)
                Require(exported.GetPixel(x, y) == bitmap.GetPixel(x, y), "PNG export preserves the composed privacy pixels");
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
        if (parent is ContentControl { Content: DependencyObject content } && VisualTreeHelper.GetChildrenCount(parent) == 0)
        { yield return content; foreach (var child in Descendants(content)) yield return child; }
    }

    private static Task SettleAsync() => System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
    private static void Save(FrameworkElement visual, string output, string name, double width, bool preserveLayout = false, double scale = 1)
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
        var render = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        // Capture the element itself, without its centering offset in the evidence host.
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen()) context.DrawRectangle(new VisualBrush(visual) {
            ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, width, height) }, null, new Rect(0, 0, width, height));
        render.Render(drawing); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render));
        using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
