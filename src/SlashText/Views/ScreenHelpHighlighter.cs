using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SlashText.Design;

namespace SlashText.Views;

/// <summary>Temporary visual overlay; never changes the target's style, data or action.</summary>
internal sealed class ScreenHelpHighlighter : Adorner
{
    private readonly AdornerLayer _layer;
    private bool _removed;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };
    private ScreenHelpHighlighter(FrameworkElement target, AdornerLayer layer) : base(target)
    {
        _layer = layer; IsHitTestVisible = false; _timer.Tick += (_, _) => Remove();
        target.Unloaded += TargetUnloaded;
        if (LabMotion.Allowed(target)) BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(550))
        { AutoReverse = true, RepeatBehavior = new RepeatBehavior(3), FillBehavior = FillBehavior.Stop });
    }
    public static ScreenHelpHighlighter? Show(FrameworkElement target)
    {
        var layer = AdornerLayer.GetAdornerLayer(target); if (layer is null) return null;
        var highlight = new ScreenHelpHighlighter(target, layer); layer.Add(highlight); highlight._timer.Start(); return highlight;
    }
    internal void Remove()
    { if (_removed) return; _removed = true; _timer.Stop(); ((FrameworkElement)AdornedElement).Unloaded -= TargetUnloaded; BeginAnimation(OpacityProperty, null); _layer.Remove(this); }
    private void TargetUnloaded(object sender, RoutedEventArgs e) => Remove();
    protected override void OnRender(DrawingContext context)
    {
        var brush = (Brush)((FrameworkElement)AdornedElement).FindResource("Lab.accent");
        context.DrawRoundedRectangle(null, new Pen(brush, 3), new Rect(1, 1, Math.Max(0, AdornedElement.RenderSize.Width - 2), Math.Max(0, AdornedElement.RenderSize.Height - 2)), 7, 7);
    }
}
