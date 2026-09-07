using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SlashText.Design;

/// <summary>Opt-in motion; never attach entrance motion to measured capture geometry.</summary>
public static class LabMotion
{
    public static readonly DependencyProperty ReducedProperty = DependencyProperty.RegisterAttached(
        "Reduced", typeof(bool), typeof(LabMotion),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, ReducedChanged));
    public static void SetReduced(DependencyObject target, bool value) => target.SetValue(ReducedProperty, value);
    public static bool GetReduced(DependencyObject target) => (bool)target.GetValue(ReducedProperty);
    public static bool Allowed(DependencyObject target) => SystemParameters.ClientAreaAnimation && !GetReduced(target);

    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.RegisterAttached(
        "Interactive", typeof(bool), typeof(LabMotion), new PropertyMetadata(false, InteractiveChanged));
    public static void SetInteractive(DependencyObject target, bool value) => target.SetValue(InteractiveProperty, value);
    public static bool GetInteractive(DependencyObject target) => (bool)target.GetValue(InteractiveProperty);

    public static readonly DependencyProperty SwitchProperty = DependencyProperty.RegisterAttached(
        "Switch", typeof(bool), typeof(LabMotion), new PropertyMetadata(false, SwitchChanged));
    public static void SetSwitch(DependencyObject target, bool value) => target.SetValue(SwitchProperty, value);
    public static bool GetSwitch(DependencyObject target) => (bool)target.GetValue(SwitchProperty);

    public static readonly DependencyProperty EntranceProperty = DependencyProperty.RegisterAttached(
        "Entrance", typeof(string), typeof(LabMotion), new PropertyMetadata("", EntranceChanged));
    public static void SetEntrance(DependencyObject target, string value) => target.SetValue(EntranceProperty, value);
    public static string GetEntrance(DependencyObject target) => (string)target.GetValue(EntranceProperty);

    private static void InteractiveChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not ButtonBase button) return;
        button.PreviewMouseDown -= Press; button.PreviewMouseUp -= Release;
        button.MouseLeave -= Release; button.LostMouseCapture -= Release;
        button.PreviewKeyDown -= KeyDown; button.PreviewKeyUp -= KeyUp;
        button.IsEnabledChanged -= EnabledChanged;
        if (!(bool)e.NewValue) return;
        button.PreviewMouseDown += Press; button.PreviewMouseUp += Release;
        button.MouseLeave += Release; button.LostMouseCapture += Release;
        button.PreviewKeyDown += KeyDown; button.PreviewKeyUp += KeyUp;
        button.IsEnabledChanged += EnabledChanged;
    }

    // CSS active translateY(1px); a template-local transform never replaces caller transforms.
    private static void Press(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) PressTo((ButtonBase)sender, 1); }
    private static void Release(object sender, RoutedEventArgs e) => PressTo((ButtonBase)sender, 0);
    private static void KeyDown(object sender, KeyEventArgs e) { if (e.Key is Key.Space or Key.Enter) PressTo((ButtonBase)sender, 1); }
    private static void KeyUp(object sender, KeyEventArgs e) { if (e.Key is Key.Space or Key.Enter) PressTo((ButtonBase)sender, 0); }
    private static void EnabledChanged(object sender, DependencyPropertyChangedEventArgs e) => PressTo((ButtonBase)sender, 0);
    private static void PressTo(ButtonBase control, double value)
    {
        if (!control.IsEnabled) value = 0;
        control.ApplyTemplate();
        if (VisualTreeHelper.GetChildrenCount(control) == 0 || VisualTreeHelper.GetChild(control, 0) is not FrameworkElement surface) return;
        if (surface.RenderTransform is not TranslateTransform) surface.RenderTransform = new TranslateTransform();
        var transform = (TranslateTransform)surface.RenderTransform;
        Animate(transform, TranslateTransform.YProperty, value, control, "Lab.Motion.Control", false);
    }

    private static void SwitchChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not ToggleButton toggle) return;
        toggle.Loaded -= SwitchLoaded; toggle.Checked -= SwitchToggled; toggle.Unchecked -= SwitchToggled; toggle.Indeterminate -= SwitchToggled;
        if (!(bool)e.NewValue) return;
        toggle.Loaded += SwitchLoaded; toggle.Checked += SwitchToggled; toggle.Unchecked += SwitchToggled; toggle.Indeterminate += SwitchToggled;
    }
    private static void SwitchLoaded(object sender, RoutedEventArgs e) => MoveThumb((ToggleButton)sender, false);
    private static void SwitchToggled(object sender, RoutedEventArgs e) => MoveThumb((ToggleButton)sender, true);
    private static void MoveThumb(ToggleButton toggle, bool animate)
    {
        toggle.ApplyTemplate();
        if (toggle.Template?.FindName("Thumb", toggle) is not FrameworkElement thumb) return;
        if (thumb.RenderTransform is not TranslateTransform) thumb.RenderTransform = new TranslateTransform();
        var transform = (TranslateTransform)thumb.RenderTransform;
        var target = toggle.IsChecked == true ? 18d : 0d;
        if (animate) Animate(transform, TranslateTransform.XProperty, target, toggle, "Lab.Motion.Switch", false);
        else { transform.BeginAnimation(TranslateTransform.XProperty, null); transform.X = target; }
    }

    private static void EntranceChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element) return;
        element.Loaded -= Enter;
        if (!string.IsNullOrEmpty((string)e.NewValue)) element.Loaded += Enter;
    }
    private static void Enter(object sender, RoutedEventArgs e) => PlayEntrance((FrameworkElement)sender);
    public static void PlayEntrance(FrameworkElement element)
    {
        var popup = GetEntrance(element) == "Popup";
        // Explicit opt-in reserves this element's transform. Never apply to the selection bounds.
        var translate = new TranslateTransform(); var scale = new ScaleTransform(1, 1);
        element.RenderTransform = new TransformGroup { Children = { scale, translate } };
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.BeginAnimation(UIElement.OpacityProperty, null); element.Opacity = 1;
        if (!Allowed(element)) return;
        var duration = (Duration)element.FindResource(popup ? "Lab.Motion.Popup" : "Lab.Motion.Page");
        var spline = ((KeySpline)element.FindResource("Lab.Motion.EaseOut")).Clone();
        Start(translate, TranslateTransform.YProperty, popup ? 6 : 5, 0, duration, spline);
        Start(element, UIElement.OpacityProperty, popup ? 0 : .65, 1, duration, spline);
        if (popup) { Start(scale, ScaleTransform.ScaleXProperty, .98, 1, duration, spline); Start(scale, ScaleTransform.ScaleYProperty, .98, 1, duration, spline); }
    }
    private static void ReducedChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) return;
        if (target is ToggleButton toggle && GetSwitch(toggle)) MoveThumb(toggle, false);
        if (target is ButtonBase button && GetInteractive(button)) PressTo(button, 0);
        if (target is FrameworkElement element && GetEntrance(element).Length > 0 && element.IsLoaded) PlayEntrance(element);
    }
    private static void Animate(Animatable target, DependencyProperty property, double to, FrameworkElement owner, string key, bool easeOut)
    {
        var from = (double)target.GetValue(property);
        target.BeginAnimation(property, null); target.SetValue(property, to);
        if (Allowed(owner)) Start(target, property, from, to, (Duration)owner.FindResource(key), ((KeySpline)owner.FindResource(easeOut ? "Lab.Motion.EaseOut" : "Lab.Motion.Ease")).Clone());
    }
    private static void Start(DependencyObject target, DependencyProperty property, double from, double to, Duration duration, KeySpline spline)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration, FillBehavior = FillBehavior.Stop };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration.TimeSpan), spline));
        // Set the resting value first: no held clocks and no stale values after interruption.
        target.SetValue(property, to);
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        storyboard.Begin();
    }
}
