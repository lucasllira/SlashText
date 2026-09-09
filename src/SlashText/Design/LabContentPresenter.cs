using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlashText.Design;

/// <summary>
/// Preserve ContentPresenter/access-key behavior while shielding generated string labels
/// from the legacy application's implicit TextBlock.Foreground setter.
/// Authored UIElement content and custom data templates are never rewritten.
/// </summary>
public sealed class LabContentPresenter : ContentPresenter
{
    public LabContentPresenter() => Loaded += (_, _) => BindGeneratedLabels();

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        Dispatcher.BeginInvoke(BindGeneratedLabels, DispatcherPriority.Loaded);
    }

    private void BindGeneratedLabels()
    {
        if (Content is not string || ContentTemplate is not null || ContentTemplateSelector is not null) return;
        BindChildren(this);
    }

    private void BindChildren(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock text)
                text.SetBinding(TextBlock.ForegroundProperty, new Binding
                {
                    Source = this,
                    Path = new PropertyPath(TextElement.ForegroundProperty),
                    Mode = BindingMode.OneWay
                });
            BindChildren(child);
        }
    }
}
