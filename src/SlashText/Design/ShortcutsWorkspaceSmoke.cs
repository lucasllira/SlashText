using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

/// <summary>Real WPF editor fixture; no tray, hooks, updater or user data.</summary>
internal static class ShortcutsWorkspaceSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "data-fixture"), Path.Combine(output, "unused-installed"), isCapturePilot: true));
        Directory.CreateDirectory(AppPaths.AssetsDirectory);
        var imagePath = Path.Combine(AppPaths.AssetsDirectory, "fixture.png");
        using (var image = new System.Drawing.Bitmap(32, 24))
        { image.SetPixel(10, 10, System.Drawing.Color.Cyan); image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png); }
        var originalAsset = File.ReadAllBytes(imagePath);
        var fixtures = new[]
        {
            new Snippet { Name = "Resposta de boas-vindas", Trigger = "/ola", Category = "Geral", Content = "Olá, {{nome}}!\nComo posso ajudar?" },
            new Snippet { Name = "Assinatura com imagem", Trigger = "/assinatura", Category = "Trabalho", Format = SnippetFormat.Markdown,
                Content = "**Olá**, {{campo|equipe}}!\n<span style=\"font-family:Georgia;font-size:18px;color:#137b53\">Texto formatado</span>\n![Exemplo](assets/fixture.png)\n\n| Item | Valor |\n| --- | --- |\n| Data | {{data}} |" },
            new Snippet { Name = "Retorno da solicitação", Trigger = "/retorno", Category = "Trabalho", Content = "Sua solicitação será revisada em {{data:+7d}}.\nAtenciosamente, {{usuario}}." }
        };
        var checks = new List<string>();
        foreach (var theme in new[] { "Light", "Dark", "System" })
        {
            ThemeService.Apply(theme);
            foreach (var size in new[] { new Size(1440, 900), new Size(980, 680) })
            {
                var window = new MainWindow(captureEvidence: true);
                var root = (FrameworkElement)window.Content; window.Content = null;
                LabMotion.SetReduced(root, true);
                var host = new Border { Child = root, Width = size.Width, Height = size.Height };
                host.SetResourceReference(Border.BackgroundProperty, "Lab.bg");
                void Layout() { host.Measure(size); host.Arrange(new Rect(size)); host.UpdateLayout(); }
                window.PrepareShortcutsEvidence(size.Width, fixtures); Layout();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Layout();
                Require(!window.ShortcutsDraftDirtyForEvidence, "Selecting a saved snippet must not mark it dirty");
                Require(((TextBox)window.FindName("NameBox")).Text == fixtures[0].Name, "Real editor loads selected metadata");
                Require(window.ShortcutPreviewForEvidence.Contains("Olá, [nome]!", StringComparison.Ordinal), "Real variable preview renders a placeholder");
                foreach (var name in new[] { "ShortcutHeaderActions", "ShortcutSidebarPanel", "ShortcutEditorPanel", "ShortcutVariablesPanel", "NameBox", "TriggerBox", "CategoryBox" })
                {
                    var element = (FrameworkElement)window.FindName(name);
                    Require(element.ActualWidth > 0 && element.ActualHeight > 0, "Measured control: " + name);
                }
                foreach (var action in new[] { "ShortcutNewButton", "ShortcutSaveButton" })
                {
                    var button = (Button)window.FindName(action);
                    var label = ((StackPanel)button.Content).Children.OfType<TextBlock>().Single();
                    Require(label.Foreground.ToString() == button.Foreground.ToString(), "Primary authored label follows on-accent color: " + action);
                }
                SaveImage(host, output, $"shortcuts-{theme}-plain-{size.Width}", size, 1);
                var nameBox = (TextBox)window.FindName("NameBox"); nameBox.Text += " (rascunho)";
                Require(window.ShortcutsDraftDirtyForEvidence, "Metadata edits mark the draft dirty");
                window.SelectShortcutForEvidence(fixtures[1], allowDiscard: false);
                Require(window.SelectedShortcutForEvidence == fixtures[0].Id && nameBox.Text.EndsWith("(rascunho)", StringComparison.Ordinal), "Cancelled navigation preserves selection and unsaved text");
                window.SelectShortcutForEvidence(fixtures[1], allowDiscard: true); Layout();
                Require(!window.ShortcutsDraftDirtyForEvidence && window.SelectedShortcutForEvidence == fixtures[1].Id, "Confirmed discard loads the requested snippet");
                var before = window.ShortcutContentForEvidence;
                Require(before.Contains("assets/fixture.png", StringComparison.Ordinal) && before.Contains("font-family", StringComparison.Ordinal), "Rich content retains images and font styling");
                window.InsertVariableForEvidence("{{hora}}");
                Require(window.ShortcutsDraftDirtyForEvidence && window.ShortcutContentForEvidence.Contains("{{hora}}", StringComparison.Ordinal), "Variable chip inserts at the real editor caret");
                Require(!window.ShortcutPreviewForEvidence.Contains("{{hora}}", StringComparison.Ordinal), "Variable insertion updates the real preview");
                Require(await window.SaveShortcutForEvidence(), "Real async save succeeds");
                var saved = (await new SnippetMarkdownRepository().LoadAsync()).Single(s => s.Id == fixtures[1].Id);
                Require(saved.Content == window.ShortcutContentForEvidence && saved.Format == SnippetFormat.Markdown, "Saved/reopened rich content is identical");
                Require(originalAsset.SequenceEqual(File.ReadAllBytes(imagePath)), "Rich editor save does not alter image bytes");
                Require(!window.ShortcutsDraftDirtyForEvidence, "Successful save resets the draft indicator");
                Layout();
                foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d }) SaveImage(host, output, $"shortcuts-{theme}-rich-{size.Width}", size, scale);
                var variables = (FrameworkElement)window.FindName("ShortcutVariablesPanel"); var editor = (FrameworkElement)window.FindName("ShortcutEditorPanel");
                var widthBefore = editor.ActualWidth;
                window.SetVariablesVisibleForEvidence(false); Layout();
                Require(variables.Visibility == Visibility.Collapsed && editor.ActualWidth > widthBefore, "Hiding variables gives space to the editor");
                window.SetVariablesVisibleForEvidence(true); Layout();
                Require(variables.Visibility == Visibility.Visible && variables.ActualWidth >= 240, "Showing variables restores a usable width");
                var search = (TextBox)window.FindName("SearchBox"); search.Text = "sem resultado neste teste"; Layout();
                var list = (StackPanel)window.FindName("SnippetListPanel");
                Require(list.Children.Count == 1 && list.Children[0] is StackPanel, "Empty search exposes recovery action");
                search.Clear(); Layout(); Require(list.Children.Count == fixtures.Length, "Clearing search restores the list");
                window.SetShortcutProtectedForEvidence(true);
                Require(!((Button)window.FindName("ShortcutSaveButton")).IsEnabled && !((Button)window.FindName("ShortcutDeleteButton")).IsEnabled && !((Button)window.FindName("ShortcutImportButton")).IsEnabled, "Protected storage blocks all snippet write actions");
                window.SetShortcutProtectedForEvidence(false);
                foreach (var topic in ShortcutsHelpContent.Create().Topics)
                    Require(topic.Target is null || window.FindName(topic.Target) is FrameworkElement, "Help target exists: " + topic.Id);
                checks.Add($"{theme} {size}: real plain/rich editor, preview, caret, discard cancellation, save/reopen, image preservation, search, variable panel, protected mode, help targets OK");
                window.DisposeShortcutsEvidence(); window.Close();
            }
            var guide = new ScreenHelpWindow(ShortcutsHelpContent.Create());
            guide.OpenTopic("variables"); guide.SearchForEvidence("variaveis");
            Require(guide.ResultCount > 0, "Accent-insensitive help search");
            guide.SearchForEvidence("");
            var surface = guide.HelpSurface; guide.Content = null; LabMotion.SetReduced(surface, true);
            var helpSize = new Size(1040, 780); surface.Measure(helpSize); surface.Arrange(new Rect(helpSize)); surface.UpdateLayout();
            SaveImage(surface, output, $"shortcuts-help-{theme}", helpSize, 1); guide.Close();
        }
        File.WriteAllLines(Path.Combine(output, "result.txt"), checks);
    }

    private static void SaveImage(FrameworkElement visual, string output, string name, Size size, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)(size.Width * scale), (int)(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, $"{name}-{scale * 100:0}.png")); encoder.Save(file);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Shortcuts workspace: " + message); }
}
