using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;

namespace SlashText.Design;

/// <summary>Presented providers, synthetic data; no global input, hooks or personal clipboard.</summary>
internal static class AccessibilityNamesSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "fixture"), Path.Combine(output, "unused-installed"), isCapturePilot: true));
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await File.WriteAllTextAsync(AppPaths.SettingsFile, "{\"language\":\"pt-BR\",\"theme\":\"Dark\"}");
        var original = await File.ReadAllBytesAsync(AppPaths.SettingsFile);
        var previousLanguage = UiText.Language;
        var checks = new List<string>();
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var namedChecks = 0;
        var providerChecks = 0;
        var expected = new Dictionary<string, string>
        {
            ["ShortcutsTabButton"] = "Atalhos", ["CaptureTabButton"] = "Captura",
            ["QuickAccentTabButton"] = "Acento Rápido", ["StatisticsTabButton"] = "Estatísticas",
            ["SettingsTabButton"] = "Configurações", ["AboutTabButton"] = "Sobre",
            ["ShortcutImportButton"] = "Importar", ["ShortcutNewButton"] = "Novo atalho",
            ["ShortcutSaveButton"] = "Salvar", ["ShortcutDeleteButton"] = "Excluir", ["CaptureRuleButton"] = "Configurações de captura",
            ["CaptureNewButton"] = "Novo", ["CaptureImageMediaButton"] = "Imagem",
            ["CaptureVideoMediaButton"] = "Vídeo", ["CaptureGifMediaButton"] = "GIF",
            ["CaptureMonitorModeButton"] = "Monitor", ["CaptureRegionModeButton"] = "Região",
            ["CaptureWindowModeButton"] = "Janela", ["CaptureScrollingModeButton"] = "Captura longa",
            ["CaptureGifModeButton"] = "Gravar GIF", ["CaptureDelayBox"] = "Atraso",
            ["CaptureHistoryFilterBox"] = "Filtrar histórico por tipo", ["SettingsImportButton"] = "Selecionar arquivo"
        };
        try
        {
            foreach (var language in new[] { "pt-BR", "en", "es" })
            foreach (var theme in new[] { "Light", "Dark", "System" })
            foreach (var size in new[] { new Size(1440, 900), new Size(980, 680) })
            {
                UiText.Apply(language); ThemeService.Apply(theme);
                var main = new MainWindow(captureEvidence: true);
                var root = (FrameworkElement)main.Content;
                LabMotion.SetReduced(root, true);
                // Keep the original Window namescope: ElementName bindings must be
                // exercised as in production, not on a detached visual tree.
                var host = main;
                host.Width = size.Width; host.Height = size.Height; host.Left = -30000; host.Top = -30000;
                host.ShowActivated = false; host.ShowInTaskbar = false;
                try
                {
                    var fixture = new Snippet { Name = "Fixture çã ñ — must stay unchanged", Trigger = "/fixture143",
                        Content = "Private synthetic text {{nome}}", Category = "Geral", Enabled = true };
                    main.PrepareShortcutsEvidence(size.Width, [fixture]);
                    host.Show(); await SettleAsync(host);
                    // Check the actual painted footer, including live resource
                    // replacement on this same window. No antialiased glyph pixels
                    // or unused Control.Background defaults are contrast inputs.
                    foreach (var activeTheme in new[] { theme, "Light", "Dark", "System" })
                    {
                        ThemeService.Apply(activeTheme); await SettleAsync(host);
                        var footer = (TextBlock)main.FindName("ShellCollectionStatusText");
                        var surface = Ancestors(footer).OfType<Border>()
                            .First(b => b.Background is SolidColorBrush { Color.A: 255 });
                        Require(footer.IsVisible && footer.IsEnabled && footer.FontSize == 11 &&
                            Ancestors(footer).OfType<UIElement>().All(e => e.Opacity == 1),
                            "Footer contrast target is enabled opaque small text");
                        var foreground = ((SolidColorBrush)footer.Foreground).Color;
                        var background = ((SolidColorBrush)surface.Background).Color;
                        Require(foreground.A == 255, "Opaque footer foreground");
                        var lighter = Math.Max(Luminance(foreground), Luminance(background));
                        var darker = Math.Min(Luminance(foreground), Luminance(background));
                        var contrast = (lighter + .05) / (darker + .05);
                        var measurement = $"{language}/{theme}/{size.Width}/{activeTheme}: footer {foreground}/{background}, contrast {contrast:F4}:1";
                        Require(contrast >= 4.5, measurement);
                        checks.Add(measurement + ": PASS");
                    }
                    ThemeService.Apply(theme); await SettleAsync(host);
                    foreach (var pair in expected)
                    {
                        var control = (Control)main.FindName(pair.Key);
                        Require(Name(control) == UiText.Get(pair.Value), "Localized purpose: " + pair.Key);
                        namedChecks++;
                    }
                    foreach (var view in new[] { "Shortcuts", "Capture", "Accent", "Statistics", "Settings", "About" })
                    {
                        main.ConfigureLanguageEvidence(view, size.Width); await SettleAsync(host);
                        CheckProviders(view + "/top");
                        var scrollers = Elements(root).OfType<ScrollViewer>().Where(s => s.IsVisible && s.ScrollableHeight > 1).ToArray();
                        foreach (var scroller in scrollers) scroller.ScrollToBottom();
                        await SettleAsync(host); CheckProviders(view + "/bottom");
                        foreach (var scroller in scrollers) scroller.ScrollToHome();
                    }
                    main.ConfigureLanguageEvidence("Capture", size.Width); await SettleAsync(host);
                    var postMode = (TextBlock)main.FindName("CapturePostModeText");
                    var modeButton = Ancestors(postMode).OfType<Button>().First();
                    Require(Name(modeButton) == postMode.Text, $"Capture post-action purpose follows displayed mode: actual '{Name(modeButton)}', label '{postMode.Text}'");
                    var old = postMode.Text; postMode.Text = UiText.Get("Configurações de captura");
                    Require(Name(modeButton) == postMode.Text, "Capture post-action accessible name updates with its label");
                    postMode.Text = old;
                    Require(main.ShortcutContentForEvidence == fixture.Content && fixture.Name == "Fixture çã ñ — must stay unchanged",
                        "User content and identity remain unchanged");
                    checks.Add($"{language}/{theme}/{size.Width}: {expected.Count} localized named purposes; six presented views top/bottom; dynamic post-action label; unchanged content: PASS");

                    void CheckProviders(string stage)
                    {
                        foreach (var control in Elements(root).OfType<Control>())
                        {
                            if (!control.IsVisible || !control.IsEnabled || !control.Focusable ||
                                !System.Windows.Input.KeyboardNavigation.GetIsTabStop(control)) continue;
                            var peer = UIElementAutomationPeer.CreatePeerForElement(control);
                            if (peer is null || !peer.IsControlElement()) continue;
                            providerChecks++;
                            if (string.IsNullOrWhiteSpace(peer.GetName()))
                                missing.Add($"{language}/{theme}/{size.Width}/{stage}: {control.GetType().Name} {control.Name}; " +
                                    string.Join("/", Ancestors(control).OfType<FrameworkElement>().Where(e => e.Name.Length > 0).Take(4).Select(e => e.Name)));
                        }
                        foreach (var toggle in Elements(root).OfType<ToggleButton>().Where(t => t.Name == "DropToggle"))
                            Require(!toggle.Focusable, "Internal combo toggle must not gain keyboard focus");
                    }
                }
                finally { main.Close(); main.DisposeCaptureEvidence(); }
            }
            var after = await File.ReadAllBytesAsync(AppPaths.SettingsFile);
            Require(original.SequenceEqual(after), "No preference writes");
            await File.WriteAllLinesAsync(Path.Combine(output, "missing-names.txt"), missing.Order(StringComparer.Ordinal));
            Require(missing.Count == 0, "Enabled focusable provider names missing: " + string.Join(" | ", missing.Take(8)));
            checks.Add($"PASS: {namedChecks} explicit purpose checks; {providerChecks} presented provider samples; no hooks, global input, browser, capture, personal clipboard or preference writes.");
            checks.Add("Limits: provider metadata/programmatic layout, not Narrator/NVDA speech, physical keyboard/DPI or native hardware approval.");
            await File.WriteAllLinesAsync(Path.Combine(output, "result.txt"), checks);
        }
        finally { UiText.Apply(previousLanguage); }
    }

    private static string Name(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element)?.GetName() ?? "";
    private static double Luminance(Color color)
    {
        static double Linear(byte value)
        {
            var s = value / 255d;
            return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4);
        }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }
    private static async Task SettleAsync(Window host)
    {
        host.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.Loaded); host.UpdateLayout();
    }
    private static IEnumerable<DependencyObject> Elements(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Elements(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static IEnumerable<DependencyObject> Ancestors(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current)) yield return current;
    }
    private static void Require(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
    }
}
