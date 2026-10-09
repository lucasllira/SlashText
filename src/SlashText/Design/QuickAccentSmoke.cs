using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

/// <summary>Real WPF controls and real isolated persistence. No hooks, tray or updater.</summary>
internal static class QuickAccentSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "data-fixture"), Path.Combine(output, "unused-installed"), isCapturePilot: true));
        var checks = new List<string>();
        foreach (var theme in new[] { "Light", "Dark", "System" })
        foreach (var size in new[] { new Size(1440, 900), new Size(980, 680) })
        {
            ThemeService.Apply(theme);
            var window = new MainWindow(captureEvidence: true);
            var root = (FrameworkElement)window.Content; window.Content = null;
            LabMotion.SetReduced(root, true);
            var host = new Border { Child = root, Width = size.Width, Height = size.Height };
            host.SetResourceReference(Border.BackgroundProperty, "Lab.bg");
            void Layout() { host.Measure(size); host.Arrange(new Rect(size)); host.UpdateLayout(); }
            T Control<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
            var settings = new AppSettings { QuickAccentEnabled = true, QuickAccentInputDelayMs = 2000,
                QuickAccentExcludedApps = "mstsc.exe; game.exe\nterminal.exe", Theme = theme };
            window.PrepareQuickAccentEvidence(settings, size.Width); Layout();
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); Layout();
            Require(!window.QuickAccentHookRunningForEvidence, "Fixture must not install a global keyboard hook");
            Require(window.QuickAccentChoicesForEvidence == "áàâã", "PT-BR preview uses the real engine choices");
            Require(Control<TextBox>("QuickAccentDelayBox").Text == "2000" && Control<Slider>("QuickAccentDelaySlider").Value == 2000, "Loading 2000 ms never truncates the saved preference");
            Control<Slider>("QuickAccentDelaySlider").Value = 150;
            await window.SaveQuickAccentForEvidence();
            Require(Control<TextBox>("QuickAccentDelayBox").Text == "150" && (await new JsonFileStore<AppSettings>(AppPaths.SettingsFile).LoadAsync()).QuickAccentInputDelayMs == 150,
                "Native slider change handler updates field and persistence");
            foreach (var delay in new[] { 0, 100, 200, 2000 })
            {
                Control<TextBox>("QuickAccentDelayBox").Text = delay.ToString();
                await window.SaveQuickAccentForEvidence();
                var reopened = await new JsonFileStore<AppSettings>(AppPaths.SettingsFile).LoadAsync();
                Require(reopened.QuickAccentInputDelayMs == delay && Control<Slider>("QuickAccentDelaySlider").Value == delay, "Delay survives real save/reopen: " + delay);
                window.PrepareQuickAccentEvidence(reopened, size.Width);
                Require(Control<TextBox>("QuickAccentDelayBox").Text == delay.ToString(), "Restart restores both delay controls: " + delay);
            }
            foreach (var invalid in new[] { "-1", "2001", "abc", "" })
            {
                Control<TextBox>("QuickAccentDelayBox").Text = invalid;
                Control<CheckBox>("QuickAccentEnabledCheckBox").IsChecked = false;
                await window.SaveQuickAccentForEvidence();
                var saved = await new JsonFileStore<AppSettings>(AppPaths.SettingsFile).LoadAsync();
                Require(saved.QuickAccentInputDelayMs == 2000 && !saved.QuickAccentEnabled && Control<TextBlock>("QuickAccentDelayErrorText").Visibility == Visibility.Visible, "Invalid delay preserves last value while allowing disable: " + invalid);
                Require(!Control<Button>("QuickAccentPreviewChoice0").IsEnabled && !Control<TextBox>("QuickAccentTestBox").IsEnabled, "Disabled preview follows actual feature state");
            }
            Control<TextBox>("QuickAccentDelayBox").Text = "200";
            Control<CheckBox>("QuickAccentEnabledCheckBox").IsChecked = true;
            Control<CheckBox>("QuickAccentUnicodeCheckBox").IsChecked = true;
            await window.SaveQuickAccentForEvidence();
            Require(Control<TextBlock>("QuickAccentDelayErrorText").Visibility == Visibility.Collapsed, "Correcting the delay clears the error");
            Require(((StackPanel)Control<Button>("QuickAccentPreviewChoice0").Content).Children.OfType<TextBlock>().Any(t => t.Text == "U+00E1"), "Unicode preview matches its actual character");
            var testBox = Control<TextBox>("QuickAccentTestBox");
            testBox.Text = "Olá, X!"; testBox.Select(5, 1);
            Control<Button>("QuickAccentPreviewChoice0").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(testBox.Text == "Olá, á!" && testBox.SelectionLength == 0 && testBox.CaretIndex == 6, "Click inserts once at the local selection");
            Control<Button>("QuickAccentPreviewChoice1").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(testBox.Text == "Olá, áà!", "Repeated click inserts once at the updated caret");
            Control<Button>("QuickAccentClearTestButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(testBox.Text == "", "Clear affects only the local test");
            foreach (var name in new[] { "Portuguese", "Spanish", "French", "German", "Italian", "Nordic", "CentralEuropean", "Currency", "Special" })
                Control<CheckBox>($"QuickAccent{name}CheckBox").IsChecked = true;
            await window.SaveQuickAccentForEvidence();
            Require(window.QuickAccentChoicesForEvidence == "áàâãäæå", "All sets preview is a unique real ordered union");
            var letterBox = Control<ComboBox>("QuickAccentPreviewLetterBox");
            letterBox.SelectedItem = letterBox.Items.OfType<ComboBoxItem>().Single(i => i.Tag?.ToString() == "E");
            Require(window.QuickAccentChoicesForEvidence.Contains('€'), "Currency follows the selected base letter");
            letterBox.SelectedIndex = 0;
            foreach (var key in new[] { "Space", "Left", "Right" })
            foreach (var position in new[] { "TopCenter", "Center", "BottomCenter" })
            {
                var activation = Control<ComboBox>("QuickAccentActivationBox");
                activation.SelectedItem = activation.Items.OfType<ComboBoxItem>().Single(i => i.Tag?.ToString() == key);
                var positions = Control<ComboBox>("QuickAccentPositionBox");
                positions.SelectedItem = positions.Items.OfType<ComboBoxItem>().Single(i => i.Tag?.ToString() == position);
                await window.SaveQuickAccentForEvidence();
                var saved = await new JsonFileStore<AppSettings>(AppPaths.SettingsFile).LoadAsync();
                Require(saved.QuickAccentActivationKey == key && saved.QuickAccentToolbarPosition == position && saved.QuickAccentExcludedApps == settings.QuickAccentExcludedApps, "Activation, real position and exclusions survive persistence");
            }
            Layout();
            Require(Grid.GetRow(Control<Border>("QuickAccentSetsPanel")) == (size.Width < 1100 ? 1 : 0), "Narrow window stacks cards without clipping controls");
            Require(Control<Border>("QuickAccentActivationPanel").ActualWidth > 300 && Control<Border>("QuickAccentSetsPanel").ActualWidth > 300, "Both cards retain usable width");
            foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d })
                SaveImage(host, output, $"accent-{theme}-{size.Width}", size, scale);
            var viewer = Control<ScrollViewer>("QuickAccentView"); viewer.ScrollToBottom(); Layout();
            Require(viewer.HorizontalOffset == 0 && Control<TextBox>("QuickAccentExcludedAppsBox").ActualWidth > 300, "Exclusions stay reachable with no horizontal scrolling");
            SaveImage(host, output, $"accent-{theme}-{size.Width}-bottom", size, 1);
            // Loading no sets must use the existing fallback rather than leaving a dead feature.
            window.PrepareQuickAccentEvidence(new AppSettings { QuickAccentEnabled = true, QuickAccentCharacterSets = [] }, size.Width);
            Require(Control<CheckBox>("QuickAccentPortugueseCheckBox").IsChecked == true && window.QuickAccentChoicesForEvidence == "áàâã", "Empty stored set selection falls back to PT-BR");
            letterBox.SelectedItem = letterBox.Items.OfType<ComboBoxItem>().Single(i => i.Tag?.ToString() == "Z");
            Require(window.QuickAccentChoicesForEvidence == "" && Control<TextBlock>("QuickAccentPreviewEmptyText").Visibility == Visibility.Visible, "Missing character choices show a real empty state");
            foreach (var topic in QuickAccentHelpContent.Create().Topics)
                Require(window.FindName(topic.Target!) is FrameworkElement, "Help anchor resolves: " + topic.Target);
            checks.Add($"{theme} {size}: real sets, Unicode, caret, clear, disabled/empty states, delays, exclusions, activation/position, persistence, responsive cards, rendered 100/125/150/200% OK");
            window.DisposeShortcutsEvidence(); window.Close();
        }
        var help = new ScreenHelpWindow(QuickAccentHelpContent.Create()); help.SearchForEvidence("ativacao");
        Require(help.ResultCount > 0, "Help search ignores diacritics"); help.SearchForEvidence(""); help.OpenTopic("preferences");
        var surface = help.HelpSurface; help.Content = null; LabMotion.SetReduced(surface, true);
        var helpSize = new Size(1040, 780); surface.Measure(helpSize); surface.Arrange(new Rect(helpSize)); surface.UpdateLayout();
        SaveImage(surface, output, "accent-help", helpSize, 1); help.Close();
        checks.Add("No native hooks or synthetic global insertion in WPF fixture. Physical US/ABNT/Caps/Shift/focus checks remain manual in the pilot.");
        File.WriteAllLines(Path.Combine(output, "result.txt"), checks);
    }

    private static void SaveImage(Visual visual, string output, string name, Size size, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, $"{name}-{scale * 100:0}.png")); encoder.Save(file);
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Quick Accent: " + message); }
}
