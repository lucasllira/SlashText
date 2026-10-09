using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SlashText.Views;

/// <summary>The modal owns the background hit area; dismissing it never clicks through to its owner.</summary>
internal static class ModalBackdrop
{
    internal static bool IsOutside(FrameworkElement surface, Point point) =>
        !new Rect(0, 0, surface.ActualWidth, surface.ActualHeight).Contains(point);

    internal static bool DismissAt(FrameworkElement surface, Point point, Action dismiss)
    {
        if (!IsOutside(surface, point)) return false;
        dismiss(); return true;
    }

    internal static void Attach(Window dialog, FrameworkElement surface, Action dismiss)
    {
        if (dialog.Owner is not { IsVisible: true, Content: FrameworkElement ownerRoot } owner ||
            PresentationSource.FromVisual(ownerRoot)?.CompositionTarget is not { } target) return;

        var origin = target.TransformFromDevice.Transform(ownerRoot.PointToScreen(new Point()));
        var width = ownerRoot.ActualWidth; var height = ownerRoot.ActualHeight;
        surface.Width = Math.Min(dialog.Width - 16, Math.Max(360, width - 48));
        if (dialog.SizeToContent == SizeToContent.Manual)
            surface.Height = Math.Min(dialog.Height - 16, Math.Max(300, height - 48));
        surface.MaxHeight = Math.Max(300, height - 48);
        surface.HorizontalAlignment = HorizontalAlignment.Center;
        surface.VerticalAlignment = VerticalAlignment.Center;
        dialog.Content = null;
        var background = new Grid { Background = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)) };
        background.Children.Add(surface);
        background.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && IsOutside(surface, e.GetPosition(surface)))
            {
                e.Handled = true; DismissAt(surface, e.GetPosition(surface), dismiss);
            }
        };
        dialog.Content = background;
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.ResizeMode = ResizeMode.NoResize; dialog.SizeToContent = SizeToContent.Manual;
        dialog.MinWidth = 0; dialog.MinHeight = 0;
        dialog.Left = origin.X; dialog.Top = origin.Y; dialog.Width = width; dialog.Height = height;
    }
}
