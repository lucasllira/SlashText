using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SlashText.Models;
using SlashText.Services;

namespace SlashText.Design;

internal static class ProtectedStorageSmoke
{
    // Creates only new, explicitly selected test folders; never reseeds an existing profile.
    internal static void CreateFixtures(string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new IOException("Choose an empty fixture folder.");
        foreach (var mode in new[] { "history", "settings", "blocked" })
        {
            var data = Path.Combine(root, mode, "SlashDeskPilotData");
            Directory.CreateDirectory(data);
            var settings = new AppSettings { OnboardingCompleted = true, CloseToTray = false, CheckUpdatesOnStartup = false };
            settings.Capture.OutputDirectoryTemplate = Path.Combine(data, "Captures");
            var json = mode == "settings" ? "{\"theme\":" : JsonSerializer.Serialize(settings);
            File.WriteAllText(Path.Combine(data, "settings.json"), json);
            var record = new CaptureRecord { Id = "legivel-116", CreatedAt = mode == "settings" ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddDays(-180),
                FilePath = Path.Combine(data, "azul.png"), PortableRelativePath = "SlashDeskPilotData/azul.png",
                RequiresFileApproval = true, Width = 32, Height = 24 };
            using (var bitmap = new System.Drawing.Bitmap(32, 24))
            {
                using var graphics = System.Drawing.Graphics.FromImage(bitmap); graphics.Clear(System.Drawing.Color.Blue);
                bitmap.Save(Path.Combine(data, "azul.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            var history = "[" + JsonSerializer.Serialize(record) + (mode == "settings" ? "]" : ", {\"width\":\"incompatível\"}]");
            var historyPath = Path.Combine(data, "capture-history.json");
            File.WriteAllText(historyPath, history);
            if (mode == "blocked")
            {
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(historyPath))).ToLowerInvariant();
                Directory.CreateDirectory(historyPath + ".corrupt-" + hash[..16] + ".bak");
            }
            File.WriteAllText(Path.Combine(data, "snippets.md"), "# Meus atalhos\n\n## /fixture116\n<!-- slashtext:{\"id\":\"fixture116\",\"name\":\"Teste fictício 116\",\"category\":\"Geral\",\"format\":\"plain\",\"enabled\":true} -->\n```text\nTESTE FICTÍCIO 116\n```\n");
            var target = mode == "settings" ? "settings.json" : "capture-history.json";
            var manifest = new { target, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(data, target)))).ToLowerInvariant(),
                imageSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(data, "azul.png")))).ToLowerInvariant() };
            File.WriteAllText(Path.Combine(root, mode, "fixture-116.json"), JsonSerializer.Serialize(manifest));
        }
    }

    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Require(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label + ": PASS"); }
        foreach (var language in new[] { "pt-BR", "en", "es" })
        foreach (var theme in new[] { "Light", "Dark" })
        foreach (var mode in new[] { "history", "settings", "blocked" })
        {
            var root = Path.Combine(output, language + "-" + theme + "-" + mode);
            CreateFixtures(root);
            AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, Path.Combine(root, mode), Path.Combine(root, "unused"), true));
            UiText.Apply(language); ThemeService.Apply(theme);
            var original = File.ReadAllBytes(mode == "settings" ? AppPaths.SettingsFile : AppPaths.CaptureHistoryFile);
            var window = new MainWindow(captureEvidence: true) { Width = 980, Height = 680 };
            await window.LoadStorageProtectionEvidenceAsync();
            T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
            Require(Control<Border>("StorageProtectionPanel").Visibility == Visibility.Visible, "notice " + language + theme + mode);
            Require(Control<Button>("StorageProtectionHeaderButton").Visibility == Visibility.Visible, "global review route " + mode);
            Require(Control<Border>("SettingsAppearanceCard").IsEnabled == (mode != "settings"), "healthy preferences remain usable " + mode);
            Require(Control<Button>("SettingsImportButton").IsEnabled && Control<Button>("CaptureTabButton").IsEnabled, "healthy modules available " + mode);
            var action = Control<Button>(mode == "settings" ? "StorageRecoverSettingsButton" : "StorageRecoverHistoryButton");
            Require(action.IsEnabled == (mode != "blocked"), "verified preservation gates recovery " + mode);
            Require(Control<TextBlock>("StorageProtectionDescription").Text == UiText.Get(mode == "settings"
                ? "Não foi possível ler todas as preferências. Ajustes desta sessão não serão salvos."
                : "O histórico está disponível somente para consulta. Limpeza e remoção estão suspensas. Novas capturas podem ser salvas, mas não entram no histórico até a recuperação.") + "\n\n" + UiText.Get(mode == "blocked"
                ? "Não foi possível verificar uma cópia do original. A recuperação continua bloqueada. Verifique a pasta de dados e tente ler novamente; você também pode restaurar um ZIP válido."
                : "Uma cópia do original foi verificada. Você pode tentar ler novamente, restaurar um ZIP válido ou confirmar uma recuperação abaixo."), "localized protection copy " + language + mode);
            Require(AutomationProperties.GetLiveSetting(Control<TextBlock>("StorageProtectionDescription")) == AutomationLiveSetting.Polite, "accessible notice");
            await window.SaveSettingsPreferenceEvidenceAsync();
            Require(File.ReadAllBytes(mode == "settings" ? AppPaths.SettingsFile : AppPaths.CaptureHistoryFile).SequenceEqual(original), "UI ordinary save preserves original " + mode);
            var rootElement = (FrameworkElement)window.Content; window.Content = null;
            LabMotion.SetReduced(rootElement, true);
            var host = new Border { Child = rootElement, Width = 980, Height = 680 };
            host.Measure(new Size(980, 680)); host.Arrange(new Rect(0, 0, 980, 680)); host.UpdateLayout();
            var panel = Control<Border>("StorageProtectionPanel");
            Require(panel.ActualWidth > 0 && panel.ActualHeight > 0 && panel.ActualWidth < 980, "notice layout fits compact window " + mode);
            foreach (var name in new[] { "StorageRetryButton", "StorageOpenFolderButton" })
                Require(Control<Button>(name).ActualWidth > 0 && Control<Button>(name).ActualHeight > 0, "recovery actions reachable " + name);
            if (mode != "blocked" || language == "pt-BR")
            {
                var image = new RenderTargetBitmap(980, 680, 96, 96, PixelFormats.Pbgra32); image.Render(host);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(Path.Combine(output, language + "-" + theme + "-" + mode + ".png")); encoder.Save(stream);
            }
            if (mode == "history")
            {
                await window.RecoverStorageHistoryEvidenceAsync();
                await window.LoadStorageProtectionEvidenceAsync();
                Require(Control<Border>("StorageProtectionPanel").Visibility == Visibility.Collapsed, "successful recovery clears notice " + language + theme);
            }
            host.Child = null;
        }
        UiText.Apply("pt-BR");
        File.WriteAllLines(Path.Combine(output, "result.txt"), checks);
    }
}
