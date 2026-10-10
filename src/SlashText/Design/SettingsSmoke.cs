using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

internal static class SettingsSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, Path.Combine(output, "fixture"), Path.Combine(output, "unused-installed"), isCapturePilot: true));
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
        var checks = new List<string>();
        CheckBackups(output); checks.Add("Emojis: normalized transparent PNG/catalog identity and hashes; rollback after assets; invalid ZIP unchanged; old backup preserves collection; new empty collection clears only selected collection: PASS");
        foreach (var theme in new[] { "Light", "Dark", "System" })
        foreach (var size in new[] { new Size(1440, 900), new Size(980, 680) })
        {
            ThemeService.Apply(theme);
            var window = new MainWindow(captureEvidence: true);
            var preferences = new AppSettings { Theme = theme, CloseToTray = true, StartWithWindows = true, ShowSuggestions = false, CheckUpdatesOnStartup = true, QuickAccentInputDelayMs = 777 };
            window.ConfigureSettingsEvidence(preferences, size.Width);
            ((CheckBox)window.FindName("StartWithWindowsCheckBox")).IsEnabled = !AppPaths.IsCapturePilot;
            ((CheckBox)window.FindName("CheckUpdatesCheckBox")).IsEnabled = !AppPaths.IsCapturePilot;
            ((Button)window.FindName("SettingsCheckUpdatesButton")).IsEnabled = !AppPaths.IsCapturePilot;
            await window.SaveSettingsPreferenceEvidenceAsync();
            await window.ApplySettingsControlsEvidenceAsync(theme == "Light" ? "Dark" : "Light");
            await window.ApplySettingsControlsEvidenceAsync(theme);
            window.ConfigureSettingsEvidence(preferences, size.Width);
            var persisted = await new JsonFileStore<AppSettings>(AppPaths.SettingsFile).LoadAsync();
            Require(persisted.Theme == theme && persisted.CloseToTray && persisted.StartWithWindows && !persisted.ShowSuggestions && persisted.CheckUpdatesOnStartup && persisted.QuickAccentInputDelayMs == 777, "Existing preferences persist without Lab/pilot defaults");
            await window.TestSettingsBusyEvidenceAsync();
            var root = (FrameworkElement)window.Content; window.Content = null;
            Require(root.IsEnabled, "Maintenance restores enabled state");
            LabMotion.SetReduced(root, true);
            var host = new Border { Child = root, Width = size.Width, Height = size.Height };
            host.SetResourceReference(Border.BackgroundProperty, "Lab.bg");
            void Layout() { host.Measure(size); host.Arrange(new Rect(size)); host.UpdateLayout(); }
            T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
            Layout();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Loaded); Layout();
            SaveImage(host, output, $"settings-{theme}-{size.Width}-layout", size, 1);
            Require(Control<TextBlock>("SettingsDataPathText").Text == AppPaths.DataDirectory, "Actual data path");
            // Offscreen hosts have IsVisible=false without a PresentationSource; verify actual arranged controls instead.
            foreach (var name in new[] { "SettingsLightThemeButton", "SettingsDarkThemeButton", "SettingsSystemThemeButton" })
                Require(Control<Button>(name).Visibility == Visibility.Visible && Control<Button>(name).ActualWidth > 100 && Control<Button>(name).ActualHeight > 50 && Control<Button>(name).Focusable, "Arranged theme choice and keyboard focus: " + name);
            Require(Control<Button>("SettingsImportButton").IsEnabled && Control<ComboBox>("ImportSourceBox").Items.Count == 3, "All real import sources available");
            Require(Grid.GetColumn(Control<Border>("SettingsBehaviorCard")) == (size.Width < 1100 ? 0 : 2), "Responsive cards");
            for (var index = 0; index < 3; index++)
            { Control<ComboBox>("ImportSourceBox").SelectedIndex = index; Require(Control<ComboBox>("ImportSourceBox").SelectedItem is ComboBoxItem { Tag: string }, "Import source tag preserved"); }
            var prefsBefore = await File.ReadAllBytesAsync(AppPaths.SettingsFile);
            foreach (var scale in new[] { 1d, 1.25, 1.5, 2 }) SaveImage(host, output, $"settings-{theme}-{size.Width}", size, scale);
            var scroll = Control<ScrollViewer>("SettingsView"); scroll.ScrollToBottom(); Layout();
            var dataBounds = Control<Border>("SettingsDataCard").TransformToAncestor(scroll).TransformBounds(new Rect(Control<Border>("SettingsDataCard").RenderSize));
            Require(scroll.HorizontalOffset == 0 && dataBounds.Top >= -1 && dataBounds.Bottom <= scroll.ActualHeight + 1, "Local data reachable without horizontal scrolling");
            SaveImage(host, output, $"settings-{theme}-{size.Width}-bottom", size, 1);
            Control<TextBlock>("SettingsOperationText").Text = "Não foi possível restaurar: arquivo inválido. Os dados atuais foram preservados.";
            scroll.ScrollToTop(); Layout(); SaveImage(host, output, $"settings-{theme}-{size.Width}-error", size, 1);
            foreach (var topic in SettingsHelpContent.Create().Topics) Require(window.FindName(topic.Target!) is FrameworkElement, "Help target: " + topic.Id);
            var guide = new ScreenHelpWindow(SettingsHelpContent.Create()); guide.SearchForEvidence("emojis"); Require(guide.ResultCount > 0, "Searchable emoji backup guidance");
            foreach (var topic in SettingsHelpContent.Create().Topics) guide.OpenTopic(topic.Id);
            guide.Close();
            var prefsAfter = await File.ReadAllBytesAsync(AppPaths.SettingsFile); Require(prefsBefore.SequenceEqual(prefsAfter), "Viewing/help never writes preferences");
            checks.Add($"{theme} {size}: real controls/sources/path; persisted preferences; busy gate; help; responsive scroll; 100/125/150/200% render: PASS");
            host.Child = null; window.Close();
        }
        await File.WriteAllLinesAsync(Path.Combine(output, "result.txt"), checks.Append("Isolated synthetic data through real services; no hooks, registry writes, network updates or physical DPI test. The rendered error text is a presentation fixture."));
    }

    private static void CheckBackups(string output)
    {
        var data = Path.Combine(output, "backup-source"); var target = Path.Combine(output, "backup-target");
        Directory.CreateDirectory(data); Directory.CreateDirectory(target);
        var sourceFile = Path.Combine(data, "snippets.md"); File.WriteAllText(sourceFile, "source");
        var assets = Path.Combine(data, "assets"); Directory.CreateDirectory(assets); File.WriteAllText(Path.Combine(assets, "image.txt"), "asset-source");
        var emotes = Path.Combine(data, "capture-emotes");
        var input = Path.Combine(output, "transparent.png");
        using (var bitmap = new System.Drawing.Bitmap(8, 8, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        { bitmap.SetPixel(4, 4, System.Drawing.Color.FromArgb(100, 200, 80, 50)); bitmap.Save(input, System.Drawing.Imaging.ImageFormat.Png); }
        var catalog = new CaptureStampCatalog(emotes); var identity = catalog.Import(input, "Meu emoji çã");
        var backups = Path.Combine(output, "backups-verified");
        var source = new BackupService(backups, [sourceFile], assets, data, emotes);
        var zip = source.CreateManualSnapshot(); BackupService.ValidateSnapshot(zip);
        using (var archive = ZipFile.OpenRead(zip))
            Require(archive.GetEntry("capture-emotes/catalog.json") is not null && archive.GetEntry("capture-emotes/" + identity[7..] + ".png") is not null, "Catalog and PNG included");
        var targetFile = Path.Combine(target, "snippets.md"); File.WriteAllText(targetFile, "target-original");
        var targetAssets = Path.Combine(target, "assets"); Directory.CreateDirectory(targetAssets); File.WriteAllText(Path.Combine(targetAssets, "old.txt"), "old-asset");
        var targetEmotes = Path.Combine(target, "capture-emotes"); var targetCatalog = new CaptureStampCatalog(targetEmotes); var oldIdentity = targetCatalog.Import(input, "Antes");
        var restore = new BackupService(backups, [targetFile], targetAssets, target, targetEmotes);
        restore.BeforeRestoreApply = (_, path) => { if (path == "capture-emotes/") throw new IOException("Injected apply failure after assets"); };
        try { restore.RestoreSnapshot(zip); throw new InvalidOperationException("Restore should fail"); } catch (IOException) { }
        Require(File.ReadAllText(targetFile) == "target-original" && File.Exists(Path.Combine(targetAssets, "old.txt")) && new CaptureStampCatalog(targetEmotes).Items.Single().Value == oldIdentity, "Complete rollback across files/assets/emojis");
        restore.BeforeRestoreApply = null; restore.RestoreSnapshot(zip);
        var restored = new CaptureStampCatalog(targetEmotes); Require(restored.Items.Single().Value == identity && restored.Items.Single().Name == "Meu emoji çã", "Identity and label round trip");
        using (var image = restored.GetImage(identity)!.CreateBitmap()) Require(image.GetPixel(0, 0).A == 0 && image.GetPixel(4, 4).A == 100, "Transparency retained");
        var imageName = identity[7..] + ".png";
        Require(SHA256.HashData(File.ReadAllBytes(Path.Combine(emotes, imageName))).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(targetEmotes, imageName)))), "Exact normalized PNG bytes retained");
        var invalid = Path.Combine(output, "corrupt-emojis.zip"); File.Copy(zip, invalid, true);
        using (var archive = ZipFile.Open(invalid, ZipArchiveMode.Update))
        { var entry = archive.GetEntry("capture-emotes/" + imageName)!; entry.Delete(); using var writer = new StreamWriter(archive.CreateEntry("capture-emotes/" + imageName).Open()); writer.Write("invalid"); }
        try { restore.RestoreSnapshot(invalid); throw new InvalidOperationException("Corrupt ZIP should fail"); } catch (InvalidDataException) { }
        Require(new CaptureStampCatalog(targetEmotes).Items.Single().Value == identity && File.ReadAllText(targetFile) == "source", "Invalid ZIP leaves current data intact");
        var legacy = new BackupService(Path.Combine(output, "old-backups"), [sourceFile], dataDirectory: data).CreateManualSnapshot();
        restore.RestoreSnapshot(legacy); Require(new CaptureStampCatalog(targetEmotes).Items.Single().Value == identity, "Old v2 backup preserves current emojis");
        catalog.Remove(identity); var empty = source.CreateManualSnapshot(); restore.RestoreSnapshot(empty);
        Require(new CaptureStampCatalog(targetEmotes).Items.Count == 0 && File.ReadAllText(targetFile) == "source", "Explicit empty collection restores empty state");
        var maintenance = new AssetMaintenanceService(targetAssets); var report = maintenance.Analyze([]);
        Require(report.Orphans.All(item => !item.FullPath.StartsWith(targetEmotes, StringComparison.OrdinalIgnoreCase)), "Emoji collection excluded from snippet asset analysis");
    }

    private static void SaveImage(FrameworkElement host, string output, string name, Size size, double scale)
    {
        host.UpdateLayout(); var image = new RenderTargetBitmap((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32); image.Render(host);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(output, $"{name}-{(int)(scale * 100)}.png")); encoder.Save(file);
    }
    private static void Require(bool condition, string scenario) { if (!condition) throw new InvalidOperationException("Settings smoke: " + scenario); }
}
