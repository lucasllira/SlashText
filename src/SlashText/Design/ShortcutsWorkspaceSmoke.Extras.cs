using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

internal static partial class ShortcutsWorkspaceSmoke
{
    private static async Task CheckExtrasAsync(string theme, string output)
    {
        foreach (var language in ShortcutCodeEditor.Languages)
            Require((ShortcutCodeEditor.Definition(language.Id, theme != "Light") is not null) == (language.Resource is not null), "Bundled syntax: " + language.Id);
        var window = new MainWindow(captureEvidence: true);
        var root = (FrameworkElement)window.Content; window.Content = null;
        var owner = new Window { Width = 1440, Height = 900, Content = root, ShowInTaskbar = false };
        var fixture = new Snippet { Name = "Exemplo de código", Trigger = "/codigo", Category = "Desenvolvimento", Format = SnippetFormat.Markdown, Content = "Antes Depois" };
        var other = new Snippet { Name = "Outro atalho", Trigger = "/outro", Content = "Outro" };
        var literal = "// {{nome}} e {{tab}} são literais\nconst meu_valor = 2 * 3;\n\tconsole.log(\"<tag> &amp; ```\");\n  ";
        Border Block() => ShortcutCodeBlockView.GetView(((RichTextBox)window.FindName("ContentEditor")).Document.Blocks.OfType<BlockUIContainer>().Single())!;
        string Raw() { Require(ShortcutCodeBlockView.TryRead(Block(), out var data), "Code metadata survives native undo"); return data.Code; }
        try
        {
            window.PrepareShortcutsEvidence(1440, [fixture, other]); owner.Show(); owner.UpdateLayout();
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var editor = (RichTextBox)window.FindName("ContentEditor");
            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock;
            editor.CaretPosition = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward).GetPositionAtOffset(6)!;
            editor.Selection.Select(editor.CaretPosition, editor.CaretPosition); editor.IsUndoEnabled = false; editor.IsUndoEnabled = true;
            var initial = window.ShortcutContentForEvidence;
            window.InsertCodeForEvidence(new CodeBlockContent("javascript", literal)); owner.UpdateLayout();
            Require(CodeBlockMarkdown.Read(window.ShortcutContentForEvidence).Single().Content.Code == literal, "Inserted code remains literal");
            Require(window.ShortcutContentForEvidence.Contains("Antes", StringComparison.Ordinal) && window.ShortcutContentForEvidence.Contains("Depois", StringComparison.Ordinal), "Code insertion preserves surrounding prose");
            editor.Undo(); Require(window.ShortcutContentForEvidence == initial, "Undo removes inserted block without losing prose");
            editor.Redo(); Require(Raw() == literal, "Redo restores literal code metadata");
            var edited = literal + "\nconsole.log(meu_valor);";
            window.InsertCodeForEvidence(new CodeBlockContent("javascript", edited), Block());
            Require(Raw() == edited, "Edit replaces only code block");
            editor.Undo(); Require(Raw() == literal, "Undo restores previous code");
            editor.Redo(); Require(Raw() == edited, "Redo restores edited code");
            var copyCode = (Button)ShortcutCodeBlockView.Find(Block(), "CodeCopy")!;
            copyCode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(Clipboard.GetText() == edited, "Code copy copies exact source after native redo");
            var editRequested = false;
            Block().AddHandler(ShortcutCodeBlockView.EditRequestedEvent, new RoutedEventHandler((_, args) => { editRequested = true; args.Handled = true; }));
            ((Button)ShortcutCodeBlockView.Find(Block(), "CodeEdit")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(editRequested, "Edit remains interactive after native redo");
            var content = window.ShortcutContentForEvidence;
            ShortcutCodeBlockView.SetExpanded(Block(), false); ShortcutCodeBlockView.SetExpanded(Block(), true);
            Require(window.ShortcutContentForEvidence == content, "Collapsing a block does not edit its source");
            var preview = (Expander)window.FindName("ShortcutPreviewExpander"); preview.IsExpanded = true;
            window.ExpandShortcutForEvidence(true); owner.UpdateLayout();
            Require(((FrameworkElement)window.FindName("ShortcutSidebarPanel")).Visibility == Visibility.Collapsed && editor.ActualWidth > 1100, "Expanded editor occupies workspace");
            Require(window.ShortcutContentForEvidence == content, "Expansion preserves draft including code");
            SaveImage(root, output, $"shortcuts-code-expanded-{theme}", new Size(root.ActualWidth, root.ActualHeight), 1);
            window.ExpandShortcutForEvidence(false); owner.UpdateLayout();
            Require(preview.IsExpanded && ((FrameworkElement)window.FindName("ShortcutSidebarPanel")).Visibility == Visibility.Visible, "Collapse restores panels and prior preview state");
            Require(await window.FlagShortcutForEvidence(true) && await window.FlagShortcutForEvidence(false), "Favorite and pin save successfully");
            Require(window.ShortcutsDraftDirtyForEvidence && window.ShortcutContentForEvidence == content, "Preference save never overwrites dirty draft");
            var preference = (await new SnippetMarkdownRepository().LoadAsync()).Single(s => s.Id == fixture.Id);
            Require(preference.IsFavorite && preference.IsPinned && preference.Content == fixture.Content, "Preference saves last committed content, not pending draft");
            var list = (StackPanel)window.FindName("SnippetListPanel");
            window.FilterShortcutForEvidence(true, false); Require(list.Children.Count == 1, "Favorites filter");
            window.FilterShortcutForEvidence(false, true); Require(list.Children.Count == 1, "Pinned filter");
            window.FilterShortcutForEvidence(false, false); Require(list.Children.Count == 2, "All restores list");
            Require(await window.SaveShortcutForEvidence(), "Code persists with real main Save");
            var saved = (await new SnippetMarkdownRepository().LoadAsync()).Single(s => s.Id == fixture.Id);
            Require(saved.IsFavorite && saved.IsPinned && CodeBlockMarkdown.Read(saved.Content).Single().Content.Code == edited, "Save preserves code and preferences");
            window.SelectShortcutForEvidence(saved, true); Require(Raw() == edited && !window.ShortcutsDraftDirtyForEvidence, "Reopen reconstructs editable block exactly");
            owner.UpdateLayout(); SaveImage(root, output, $"shortcuts-code-{theme}", new Size(root.ActualWidth, root.ActualHeight), 1);
            var beforeDuplicate = File.ReadAllBytes(AppPaths.SnippetsFile);
            window.DuplicateShortcutForEvidence();
            Require(window.SelectedShortcutForEvidence is null && window.ShortcutsDraftDirtyForEvidence && window.ShortcutContentForEvidence == saved.Content, "Duplicate creates editable unsaved draft with all rich content");
            Require(beforeDuplicate.SequenceEqual(File.ReadAllBytes(AppPaths.SnippetsFile)), "Duplicate does not persist before main Save");
            Require(await window.SaveShortcutForEvidence(), "Duplicate saves");
            var reopened = await new SnippetMarkdownRepository().LoadAsync(); var copy = reopened.Single(s => s.Trigger == "/codigo_copia");
            Require(copy.Id != fixture.Id && copy.Content == saved.Content && !copy.IsFavorite && !copy.IsPinned && reopened.Single(s => s.Id == fixture.Id).Content == saved.Content, "Duplicate has independent identity and leaves original unchanged");
            window.SetShortcutProtectedForEvidence(true); Require(!await window.FlagShortcutForEvidence(true), "Protected storage cannot modify favorite"); window.SetShortcutProtectedForEvidence(false);
            owner.Width = 980; owner.Height = 680; window.PrepareShortcutsEvidence(980, reopened.ToArray()); owner.UpdateLayout();
            SaveImage(root, output, $"shortcuts-code-narrow-{theme}", new Size(root.ActualWidth, root.ActualHeight), 1);
            var dialog = new ShortcutCodeBlockWindow(new CodeBlockContent("javascript", edited)) { Owner = owner };
            LabMotion.SetReduced(dialog, true); dialog.EnableBackdrop(); Exception? failure = null;
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    dialog.UpdateLayout(); Require(dialog.Surface.ActualHeight <= root.ActualHeight - 32, "Code popup fits native owner");
                    SaveImage((FrameworkElement)dialog.Content, output, $"shortcuts-code-popup-{theme}", new Size(dialog.Width, dialog.Height), 1);
                    dialog.CodeEditor.Text = edited + "\n// salvo"; dialog.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            }), DispatcherPriority.ApplicationIdle);
            Require(dialog.ShowDialog() == true && dialog.Result?.Code == edited + "\n// salvo", "Native code popup returns literal edited source");
            if (failure is not null) throw new InvalidOperationException("Code popup smoke", failure);
            var cancel = new ShortcutCodeBlockWindow(new CodeBlockContent("javascript", edited)) { Owner = owner }; cancel.EnableBackdrop();
            cancel.Loaded += (_, _) => cancel.Dispatcher.BeginInvoke(new Action(() => { cancel.CodeEditor.Text = "não salvar"; cancel.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }), DispatcherPriority.ApplicationIdle);
            Require(cancel.ShowDialog() != true && cancel.Result is null, "Cancel returns no replacement code");
        }
        finally { owner.Close(); window.DisposeShortcutsEvidence(); window.Close(); }
    }
}
