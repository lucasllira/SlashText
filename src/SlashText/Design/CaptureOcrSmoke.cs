using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Services;
using SlashText.Views;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;

namespace SlashText.Design;

/// <summary>Real Windows worker + WPF checks; all content is synthetic, no personal captures.</summary>
internal static class CaptureOcrSmoke
{
    internal static async Task RunAsync(string output, string? bestModels)
    {
        var clipboard = Clipboard.GetDataObject();
        try { await RunChecksAsync(output, bestModels); }
        finally { if (clipboard is not null) Clipboard.SetDataObject(clipboard, copy: true); else Clipboard.Clear(); }
    }
    private static async Task RunChecksAsync(string output, string? alternativeModels)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, Path.Combine(output, "fixture-data"), output, isCapturePilot: true));
        var metrics = new List<object>();
        foreach (var (name, text, dark, size, font) in new[] {
            ("portuguese-light", "Organização financeira\nAção, seleção e informação.\nTotal: R$ 123,45", false, 18f, "Segoe UI"),
            ("portuguese-dark", "Organização financeira\nAção, seleção e informação.\nTotal: R$ 123,45", true, 18f, "Segoe UI"),
            ("english-url", "Connection failed: error 404\nhttps://example.com/docs\nuser@example.com", false, 18f, "Segoe UI"),
            ("small-font", "Captura de texto pequeno\nInformação: 2026-10-08", false, 11f, "Segoe UI"),
            ("code-dark", "if (value == 42) {\n    return value + 1;\n}", true, 16f, "Consolas"),
            ("numeric", "Conta 1234567890\nValor: R$ 1.234,56\n2026-10-08", false, 18f, "Segoe UI") })
        {
            using var bitmap = Fixture(text, dark, size, font);
            bitmap.Save(Path.Combine(output, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            foreach (var model in alternativeModels is null ? new[] { "best" } : new[] { "best", "fast" })
            {
                var result = await new CaptureOcrService(modelsDirectory: model == "fast" ? alternativeModels : null).RecognizeAsync(bitmap);
                Require(result.Text.Length > 0, name + " recognizes text");
                metrics.Add(new { Fixture = name, Model = model, result.ElapsedMilliseconds, result.PeakWorkingSetBytes,
                    CharacterErrorRate = ErrorRate(text, result.Text), Expected = text, Actual = result.Text });
            }
        }
        File.WriteAllText(Path.Combine(output, "benchmark.json"), JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        using var blank = Fixture("", false, 18, "Segoe UI");
        Require(string.IsNullOrWhiteSpace((await new CaptureOcrService().RecognizeAsync(blank)).Text), "Blank image produces empty text");
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            try { await new CaptureOcrService().RecognizeAsync(blank, canceled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
        }
        using var large = Fixture("Cancelable reading\n" + new string('A', 400), false, 18, "Segoe UI");
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            try { await new CaptureOcrService().RecognizeAsync(large, cancellation.Token); throw new Exception("Active worker cancellation ignored"); }
            catch (OperationCanceledException) { }
        }
        // Worker inputs/results have been removed even after cancellation.
        var jobs = Path.Combine(Path.GetTempPath(), "SlashDesk-OCR");
        Require(!Directory.Exists(jobs) || Directory.GetDirectories(jobs).Length == 0, "No capture or text left in OCR temporary jobs");

        using var fixture = Fixture("SlashDesk\nExtração local de texto\nTeste em português e inglês", false, 18, "Segoe UI");
        foreach (var theme in new[] { "Light", "Dark", "System" })
        {
            ThemeService.Apply(theme);
            var owner = new Window { Width = 940, Height = 760, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            var editor = new CaptureWorkbenchEditor(); editor.LoadImage(fixture); owner.Content = editor;
            LabMotion.SetReduced(owner, true); owner.Show();
            var revision = editor.Document!.Revision;
            var before = editor.Render(); using var beforeStream = new MemoryStream(); before.Save(beforeStream, System.Drawing.Imaging.ImageFormat.Png); before.Dispose();
            var dialog = new CaptureOcrWindow(fixture, editor.SetOcrReading) { Owner = owner };
            LabMotion.SetReduced(dialog, true); dialog.Show();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Save(dialog, output, theme + "-reading", 1);
            await dialog.FirstRead;
            Require(dialog.ResultText.Contains("SlashDesk", StringComparison.OrdinalIgnoreCase), "WPF panel shows actual OCR output");
            Require(dialog.CopyAllEnabled, "Copy enabled only after text is ready");
            dialog.SelectTextForEvidence(0, 9); dialog.CopySelectionForEvidence(); Require(Clipboard.GetText() == dialog.ResultText[..9], "Copies only selected text");
            dialog.CopyAllForEvidence(); Require(Clipboard.GetText() == dialog.ResultText, "Copies complete edited result");
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Save(dialog, output, theme + "-result", 1); Save(dialog, output, theme + "-result", 1.5);
            dialog.Close();
            var compact = new CaptureOcrWindow(fixture) { Owner = owner, Width = 540, Height = 500 };
            LabMotion.SetReduced(compact, true); compact.Show(); await compact.FirstRead;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Save(compact, output, theme + "-compact", 1); compact.Close();
            using var after = editor.Render(); using var afterStream = new MemoryStream(); after.Save(afterStream, System.Drawing.Imaging.ImageFormat.Png);
            Require(revision == editor.Document.Revision && beforeStream.ToArray().SequenceEqual(afterStream.ToArray()), "OCR and tint preserve pixels/revision/undo");
            var pulse = new CaptureOcrPulse(); LabMotion.SetReduced(pulse, true); pulse.Start();
            Require(!pulse.HasActiveAnimation, "Reduced motion uses static indication");
            LabMotion.SetReduced(pulse, false); pulse.Start(); pulse.Stop(); Require(!pulse.HasActiveAnimation, "Stop removes animation clocks");
            editor.Dispose(); owner.Close();
        }
        // Selection OCR must leave the selection/session alive without finalizing an image.
        ThemeService.Apply("Light");
        var overlay = new RegionCaptureWindow(fixture, pilotVisuals: true); overlay.Show();
        overlay.SetSelectionForEvidence(new Rect(0, 0, fixture.Width, fixture.Height));
        using var rendered = overlay.RenderForEvidence(); var selectionRevision = overlay.CompletedSession;
        var selectionDialog = new CaptureOcrWindow(rendered) { Owner = overlay, Topmost = true }; LabMotion.SetReduced(selectionDialog, true);
        selectionDialog.Show(); await selectionDialog.FirstRead; selectionDialog.Close();
        Require(overlay.EditedBitmap is null && overlay.CompletedSession == selectionRevision, "OCR does not complete selection or create image/session");
        overlay.Close();
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: offline workers, pt/en, empty result, cancellation, cleanup, WPF results/copy, reduced motion, unchanged pixels and selection. Synthetic fixtures only; real screenshots/DPI mixed-monitor validation remains manual.");
    }
    private static DrawingBitmap Fixture(string text, bool dark, float size, string family)
    {
        var bitmap = new DrawingBitmap(950, 260);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap); graphics.Clear(dark ? DrawingColor.FromArgb(24, 24, 24) : DrawingColor.White);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var font = new System.Drawing.Font(family, size, System.Drawing.GraphicsUnit.Pixel);
        using var brush = new System.Drawing.SolidBrush(dark ? DrawingColor.White : DrawingColor.FromArgb(24, 24, 24));
        graphics.DrawString(text, font, brush, 28, 28); return bitmap;
    }
    private static double ErrorRate(string expected, string actual)
    {
        static string Normalize(string value) => System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ");
        expected = Normalize(expected); actual = Normalize(actual); var row = Enumerable.Range(0, actual.Length + 1).ToArray();
        for (var i = 1; i <= expected.Length; i++) { var next = new int[actual.Length + 1]; next[0] = i;
            for (var j = 1; j <= actual.Length; j++) next[j] = Math.Min(Math.Min(next[j - 1] + 1, row[j] + 1), row[j - 1] + (expected[i - 1] == actual[j - 1] ? 0 : 1)); row = next; }
        return row[^1] / (double)Math.Max(1, expected.Length);
    }
    private static void Save(Window window, string output, string name, double scale)
    {
        window.UpdateLayout(); var visual = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)(visual.ActualWidth * scale), (int)(visual.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name + $"-{scale * 100:0}.png")); encoder.Save(file);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
