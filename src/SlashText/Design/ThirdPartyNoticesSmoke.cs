using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

internal static class ThirdPartyNoticesSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, Path.Combine(output, "fixture"), Path.Combine(output, "unused-installed"), isCapturePilot: true));
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await File.WriteAllTextAsync(AppPaths.SettingsFile, "{\"theme\":\"Dark\",\"language\":\"pt-BR\"}");
        var original = await File.ReadAllBytesAsync(AppPaths.SettingsFile);
        var notices = ThirdPartyNotices.Load();
        var checks = new List<string>();
        foreach (var language in new[] { "pt-BR", "en", "es" })
            foreach (var theme in new[] { "Light", "Dark", "System" })
            {
                UiText.Apply(language); ThemeService.Apply(theme);
                var owner = new MainWindow(captureEvidence: true);
                var browsers = 0; var opened = 0;
                owner.ConfigureAboutEvidence(980, _ => browsers++);
                owner.ConfigureAboutNoticesEvidence(dialog =>
                {
                    opened++;
                    Require(dialog.Title == UiText.Get("Avisos de terceiros"), "translated dialog title");
                    Require(dialog.Components.Focusable && dialog.LicenseText.Focusable && dialog.LicenseText.IsReadOnly, "keyboard/read-only controls");
                    Require(AutomationProperties.GetName(dialog.Components) == UiText.Get("Componente"), "component accessible name");
                    foreach (var document in notices.Documents)
                    {
                        dialog.Components.SelectedItem = document;
                        Require(dialog.LicenseText.Text.EndsWith(notices.Text(document), StringComparison.Ordinal), "complete original license displayed");
                        Require(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(notices.Text(document)))) == document.Sha256, "document digest");
                    }
                    dialog.Components.SelectedItem = notices.Documents.Single(d => d.Id == "ScreenRecorderLib");
                    var content = (FrameworkElement)dialog.Content; dialog.Content = null;
                    foreach (var size in new[] { new Size(920, 720), new Size(480, 520) })
                    {
                        var host = new Border { Child = content, Width = size.Width, Height = size.Height };
                        host.Measure(size); host.Arrange(new Rect(size)); host.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (var file = File.Create(Path.Combine(output, $"notices-{language}-{theme}-{size.Width}.png"))) encoder.Save(file);
                        Require(dialog.LicenseText.ActualWidth > 100 && dialog.LicenseText.ActualHeight > 100, "readable body at compact/reference size");
                        host.Child = null;
                    }
                    dialog.Close();
                });
                var button = (Button)owner.FindName("AboutThirdPartyNoticesButton");
                Require(button.IsEnabled && button.Focusable && button.Content?.ToString() == UiText.Get("Avisos de terceiros"), "translated keyboard-accessible About entrypoint");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(opened == 1 && browsers == 0, "actual About handler opens only local notices");
                owner.Close(); checks.Add($"{language}/{theme}: actual About handler, all license bodies/hashes, keyboard/accessible names, compact/reference renders: PASS");
            }
        var current = await File.ReadAllBytesAsync(AppPaths.SettingsFile);
        Require(original.SequenceEqual(current), "viewing notices preserves preferences");
        await File.WriteAllTextAsync(Path.Combine(output, "catalog.json"), notices.ManifestJson);
        foreach (var document in notices.Documents) await File.WriteAllTextAsync(Path.Combine(output, document.Entry), notices.Text(document), new UTF8Encoding(false));
        await File.WriteAllLinesAsync(Path.Combine(output, "result.txt"), checks.Append($"PASS: {notices.Documents.Count} embedded texts; no network/browser/clipboard/registry writes/recording; original upstream text retained. Renders are not physical DPI tests."));
        UiText.Apply("pt-BR");
    }
    private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException("Third-party notices: " + label); }
}
