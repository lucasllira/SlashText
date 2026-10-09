using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

internal static partial class ShortcutsWorkspaceSmoke
{
    private static void SettleOwnedCaptureLayout(Window window)
    {
        // Owned windows are live visuals: allow the queued render to complete
        // after search/layout changes before rasterizing their visual tree.
        window.UpdateLayout();
        var frame = new DispatcherFrame();
        window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        window.UpdateLayout();
    }
    private static void CheckCaptureComplements(string theme, string output)
    {
        using var bitmap = new System.Drawing.Bitmap(900, 480);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(System.Drawing.Color.FromArgb(241, 245, 249));
            using var font = new System.Drawing.Font("Segoe UI", 26);
            graphics.DrawString("SlashDesk · edição de anotações", font, System.Drawing.Brushes.DarkSlateGray, 48, 48);
            graphics.DrawString("Selecione, mova, edite e desfaça.", font, System.Drawing.Brushes.DarkSlateGray, 48, 105);
        }
        foreach (var expanded in new[] { false, true })
        {
            var window = new MainWindow(captureEvidence: true);
            var surface = (FrameworkElement)window.Content; window.Content = null;
            LabMotion.SetReduced(surface, true);
            window.PrepareCaptureEvidence(bitmap, CaptureAnnotationKind.Text, expanded);
            var editor = (CaptureWorkbenchEditor)window.FindName("CaptureInlineEditor");
            editor.InsertAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Text, Start = new(50, 190), Text = "Texto que pode ser editado", Size = 26 });
            var id = editor.Document!.EditableAnnotations().Last().Id;
            Require(editor.SelectAnnotation(id), "Select existing text annotation");
            editor.AnnotationText = "Texto atualizado"; editor.TextFontFamily = "Georgia"; editor.TextSize = 32; editor.TextBold = true;
            Require(editor.ApplySelectedProperties() && editor.SelectedAnnotation!.Annotation.Text == "Texto atualizado", "Apply selected text properties");
            editor.Undo(); Require(editor.SelectedAnnotation!.Annotation.Text == "Texto que pode ser editado", "Selection refreshes on undo"); editor.Redo();
            editor.InsertAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Rectangle, Start = new(500, 210), End = new(680, 310), FillArgb = System.Drawing.Color.LightSkyBlue.ToArgb(), OutlineArgb = null });
            var shapeId = editor.Document.EditableAnnotations().Last().Id; editor.SelectAnnotation(shapeId);
            Require(editor.ApplySelectedProperties(220, 120) && editor.SelectedAnnotation!.Annotation.End == new Point(720, 330), "Apply existing rectangle dimensions");
            editor.DeleteSelectedAnnotation(); editor.Undo(); editor.SelectAnnotation(id);
            foreach (var topic in CaptureHelpContent.Create(new CaptureSettings()).Topics)
                Require(topic.Target is null || window.FindName(topic.Target) is FrameworkElement, "Capture help target exists: " + topic.Id);
            var size = new Size(1440, 1000); surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout();
            foreach (var scale in new[] { 1d, 1.5d }) SaveImage(surface, output, $"capture-{theme}-objects-{expanded}", size, scale);
            window.DisposeCaptureEvidence(); window.Close();
        }
        var originalSettings = File.Exists(AppPaths.SettingsFile) ? File.ReadAllBytes(AppPaths.SettingsFile) : null;
        var ownerRoot = new Grid(); ownerRoot.SetResourceReference(Grid.BackgroundProperty, "Lab.bg");
        var owner = new Window { Width = 980, Height = 680, Content = ownerRoot, ShowInTaskbar = false };
        owner.Show(); owner.UpdateLayout();
        try
        {
            Exception? failure = null;
            var records = Enumerable.Range(0, 75).Select(i => new CaptureRecord { FilePath = Path.Combine(output, $"captura-{i:000}.png"), Type = "regiao", CreatedAt = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero).AddMinutes(i) }).ToArray();
            var history = new CaptureHistoryWindow(records, record => record.FilePath, "", "all") { Owner = owner };
            LabMotion.SetReduced(history, true);
            history.Loaded += (_, _) => history.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    history.UpdateLayout(); Require(history.VisibleCount == 25, "Full history bounds rows to one page");
                    history.PageForEvidence(2); Require(history.VisibleCount == 25 && history.PageForEvidenceValue == 2, "History reaches records beyond carousel limit");
                    history.SearchForEvidence("captura-074"); Require(history.VisibleCount == 1 && history.PageForEvidenceValue == 0, "Search resets pagination");
                    history.SearchForEvidence("região 09/10/2026"); Require(history.VisibleCount == 25, "History name/type/date search");
                    SettleOwnedCaptureLayout(history);
                    SaveImage((FrameworkElement)history.Content, output, $"capture-history-owned-{theme}", new Size(history.Width, history.Height), 1);
                    Require(!ModalBackdrop.DismissAt(history.Surface, new Point(40, 40), history.Close), "Click inside history keeps it open");
                    Require(ModalBackdrop.DismissAt(history.Surface, new Point(-10, 40), history.Close), "Click outside full history closes it");
                }
                catch (Exception exception) { failure = exception; history.Close(); }
            }), DispatcherPriority.ApplicationIdle);
            history.ShowDialog(); if (failure is not null) throw new InvalidOperationException("Capture history modal smoke", failure);
            Require(history.RequestedRecord is null, "Dismissing history triggers no record action");
            var help = new ScreenHelpWindow(CaptureHelpContent.Create(new CaptureSettings())) { Owner = owner };
            LabMotion.SetReduced(help, true); help.EnableBackdrop();
            help.Loaded += (_, _) => help.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    help.OpenTopic("objects"); help.SearchForEvidence("anotacoes"); Require(help.ResultCount > 0, "Search new object help topic");
                    help.SearchForEvidence(""); SettleOwnedCaptureLayout(help);
                    SaveImage((FrameworkElement)help.Content, output, $"capture-help-owned-{theme}", new Size(help.Width, help.Height), 1);
                    Require(!ModalBackdrop.DismissAt(help.HelpSurface, new Point(40, 40), help.Close), "Inside help does not dismiss");
                    Require(ModalBackdrop.DismissAt(help.HelpSurface, new Point(-10, 40), help.Close), "Capture help closes outside");
                }
                catch (Exception exception) { failure = exception; help.Close(); }
            }), DispatcherPriority.ApplicationIdle);
            help.ShowDialog(); if (failure is not null) throw new InvalidOperationException("Capture help modal smoke", failure);
            Require(help.RequestedTarget is null, "Outside dismissal does not activate help target");
            Require(originalSettings is null ? !File.Exists(AppPaths.SettingsFile) : originalSettings.SequenceEqual(File.ReadAllBytes(AppPaths.SettingsFile)), "History/help do not modify settings");
        }
        finally { owner.Close(); }
    }
}
