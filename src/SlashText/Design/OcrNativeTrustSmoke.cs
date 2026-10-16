using System.Drawing;
using System.IO;
using System.Text.Json;
using SlashText.Services;

namespace SlashText.Design;

// Runs against the actual single-file app: generated images only, no UI/clipboard/capture.
internal static class OcrNativeTrustSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, output, Path.Combine(output, "unused"), true));
        string tesseract;
        string leptonica;
        using (var native = OcrNativeAssets.Acquire())
        { tesseract = native.TesseractVersion; leptonica = native.LeptonicaVersion; }
        var results = new List<object>();
        foreach (var model in new[] { "Best", "Fast" })
        foreach (var language in new[] { "por", "eng", "por+eng" })
        {
            using var image = new Bitmap(800, 160);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.White);
                using var font = new Font("Segoe UI", 24);
                graphics.DrawString("SlashDesk 12345\nLocal offline OCR", font, Brushes.Black, 24, 24);
            }
            var result = await new CaptureOcrService(options: new() { Model = model, Languages = language, Layout = "Block" }).RecognizeAsync(image);
            if (!result.Text.Contains("SlashDesk", StringComparison.OrdinalIgnoreCase) || result.Confidence < .70f)
                throw new InvalidDataException("Published OCR synthetic fixture failed.");
            results.Add(new { model, language, result.Confidence, result.ElapsedMilliseconds });
        }
        if (Directory.EnumerateDirectories(TemporarySession.Root, "ocr-*").Any()) throw new IOException("Published OCR session was retained.");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        { status = "PASS", tesseract, leptonica, results, limits = "Synthetic native OCR only; no clipboard, screen/audio recording or clean-VM attestation." },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
