using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using SlashText.Services;

internal static class CaptureEditorPerformanceChecks
{
    public static void Run()
    {
        using (var alpha = new Bitmap(2, 2, PixelFormat.Format32bppArgb))
        {
            alpha.SetPixel(0, 0, Color.Red); alpha.SetPixel(1, 0, Color.FromArgb(128, 25, 200, 70));
            alpha.SetPixel(0, 1, Color.Blue); alpha.SetPixel(1, 1, Color.White);
            var preview = CaptureBitmapSource.Create(alpha);
            var pixels = Pixels(preview);
            Check(preview.IsFrozen && pixels[2] == 255 && pixels[3] == 255 &&
                pixels[4] == 70 && pixels[5] == 200 && pixels[6] == 25 && pixels[7] == 128 && pixels[8] == 255,
                "Direct transfer preserves BGRA, alpha and row orientation");
            alpha.SetPixel(0, 0, Color.Black);
            Check(Pixels(preview).SequenceEqual(pixels), "Frozen preview owns its pixels after bitmap changes");
        }
        var memory = Marshal.AllocHGlobal(16);
        try
        {
            Marshal.Copy(new byte[16], 0, memory, 16);
            using var reversed = new Bitmap(2, 2, -8, PixelFormat.Format32bppArgb, IntPtr.Add(memory, 8));
            reversed.SetPixel(0, 0, Color.Red); reversed.SetPixel(0, 1, Color.Blue);
            var pixels = Pixels(CaptureBitmapSource.Create(reversed));
            Check(pixels[2] == 255 && pixels[8] == 255, "Negative-stride bitmap is not vertically inverted");
        }
        finally { Marshal.FreeHGlobal(memory); }
        using var source = new Bitmap(2560, 2317, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(source))
        using (var brush = new LinearGradientBrush(new Rectangle(0, 0, source.Width, source.Height), Color.SteelBlue, Color.White, 35))
        using (var font = new Font("Segoe UI", 30, GraphicsUnit.Pixel))
        {
            graphics.FillRectangle(brush, 0, 0, source.Width, source.Height);
            for (var y = 80; y < 2100; y += 150) graphics.DrawString("Captura de teste · imagem grande, emojis e efeitos sobrepostos", font, Brushes.Black, 100, y);
        }
        var annotations = new List<CaptureAnnotation>();
        for (var i = 0; i < 61; i++)
        {
            var x = 120 + (i % 8) * 280; var y = 130 + (i % 6) * 280;
            annotations.Add(i % 3 == 0
                ? new CaptureAnnotation { Kind = CaptureAnnotationKind.Blur, Start = new(x, y), End = new(x + 300, y + 160), PrivacyStrength = 16 }
                : new CaptureAnnotation { Kind = CaptureAnnotationKind.Stamp, Start = new(x + 80, y + 60),
                    Text = NotoEmojiCatalog.QuickItems[i % NotoEmojiCatalog.QuickItems.Count].Value, Size = 64 });
        }
        // Warm codecs/rendering before timing; this benchmark is evidence, not a
        // flaky wall-clock pass/fail assertion or a claim about physical UI FPS.
        using (var warm = CaptureAnnotationRenderer.Render(source, [annotations[0]], source.Width, source.Height))
        { _ = PngPreview(warm); _ = CaptureBitmapSource.Create(warm); }
        var baseline = Stopwatch.StartNew();
        for (var count = 1; count <= annotations.Count; count++)
        {
            using var rendered = CaptureAnnotationRenderer.Render(source, annotations.Take(count).ToArray(), source.Width, source.Height);
            _ = PngPreview(rendered);
        }
        baseline.Stop();
        using var document = new CaptureEditorDocument(source);
        var incremental = Stopwatch.StartNew();
        foreach (var annotation in annotations)
        {
            document.AddAnnotation(annotation);
            using var rendered = document.Render(); _ = CaptureBitmapSource.Create(rendered);
        }
        incremental.Stop();
        using var expected = CaptureAnnotationRenderer.Render(source, annotations, source.Width, source.Height);
        using (var actual = document.Render())
            Check(EqualPixels(actual, expected), "Incremental output matches full replay for 61 overlapping stamps/blur operations");
        document.MarkSaved();
        var pending = new CaptureAnnotation { Kind = CaptureAnnotationKind.Line, Start = new(10, 10), End = new(2500, 10),
            OutlineArgb = Color.Red.ToArgb(), Thickness = 12 };
        using (var pendingPreview = document.Render(pending))
            Check(pendingPreview.GetPixel(100, 10).R > 240, "Pending preview includes a new annotation");
        using (var unmodified = document.Render())
        {
            Check(EqualPixels(unmodified, expected) && !document.HasUnsavedChanges && document.OperationCount == 61,
                "Pending rendering does not contaminate the cache/checkpoint");
            unmodified.SetPixel(0, 0, Color.HotPink);
        }
        using (var ownedCopy = document.Render())
            Check(ownedCopy.GetPixel(0, 0).ToArgb() == expected.GetPixel(0, 0).ToArgb(), "Caller mutation cannot corrupt cached pixels");
        document.Undo();
        using (var undone = document.Render())
        using (var undoReference = CaptureAnnotationRenderer.Render(source, annotations.Take(60).ToArray(), source.Width, source.Height))
            Check(EqualPixels(undone, undoReference), "Undo invalidates cache without changing render order");
        document.AddAnnotation(pending);
        using (var branched = document.Render())
        using (var branchReference = CaptureAnnotationRenderer.Render(source, annotations.Take(60).Append(pending).ToArray(), source.Width, source.Height))
            Check(EqualPixels(branched, branchReference) && !document.CanRedo, "Branch after undo cannot reuse stale pixels");
        document.DiscardChanges();
        using (var restored = document.Render())
            Check(EqualPixels(restored, expected) && !document.HasUnsavedChanges, "Discard restores checkpoint after a cache branch");
        Console.WriteLine($"Capture stress 2560x2317 / 61 operations: replay+PNG={baseline.ElapsedMilliseconds} ms; incremental+pixels={incremental.ElapsedMilliseconds} ms; exact pixels PASS.");
    }

    private static BitmapSource PngPreview(Bitmap bitmap)
    {
        using var stream = new MemoryStream(); bitmap.Save(stream, ImageFormat.Png); stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
    private static byte[] Pixels(BitmapSource image)
    {
        var bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes;
    }
    private static bool EqualPixels(Bitmap first, Bitmap second) =>
        first.Size == second.Size && Pixels(CaptureBitmapSource.Create(first)).SequenceEqual(Pixels(CaptureBitmapSource.Create(second)));
    private static void Check(bool condition, string scenario)
    { if (!condition) throw new InvalidOperationException("Capture performance regression: " + scenario); }
}
