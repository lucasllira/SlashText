using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SlashText.Design;

namespace SlashText.Views;

/// <summary>Visual-only tint: never enters a document, export or OCR input.</summary>
public sealed class CaptureOcrPulse : Border
{
    private readonly SolidColorBrush _fill = new(Color.FromArgb(35, 57, 151, 244));
    private readonly SolidColorBrush _stroke = new(Color.FromRgb(57, 151, 244));
    private readonly DependencyPropertyDescriptor _reduced = DependencyPropertyDescriptor.FromProperty(LabMotion.ReducedProperty, typeof(CaptureOcrPulse));
    private bool _active;
    public CaptureOcrPulse()
    {
        Background = _fill; BorderBrush = _stroke; BorderThickness = new Thickness(2);
        IsHitTestVisible = false; Visibility = Visibility.Collapsed;
        Loaded += (_, _) => { _reduced.AddValueChanged(this, MotionChanged); if (_active) Animate(); };
        Unloaded += (_, _) => { _reduced.RemoveValueChanged(this, MotionChanged); ClearClocks(); };
    }
    private void MotionChanged(object? sender, EventArgs e) { if (_active) Animate(); }
    public void Start() { _active = true; Visibility = Visibility.Visible; Animate(); }
    public void Stop() { _active = false; ClearClocks(); Visibility = Visibility.Collapsed; }
    private void ClearClocks()
    { _fill.BeginAnimation(SolidColorBrush.ColorProperty, null); _stroke.BeginAnimation(SolidColorBrush.ColorProperty, null); }
    private void Animate()
    {
        ClearClocks(); if (!LabMotion.Allowed(this)) return;
        _fill.BeginAnimation(SolidColorBrush.ColorProperty, Animation(_fill.Color, Color.FromArgb(45, 151, 105, 232)));
        _stroke.BeginAnimation(SolidColorBrush.ColorProperty, Animation(_stroke.Color, Color.FromRgb(151, 105, 232)));
    }
    private static ColorAnimation Animation(Color from, Color to) => new(from, to, TimeSpan.FromMilliseconds(1000))
    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
    internal bool HasActiveAnimation => _fill.HasAnimatedProperties || _stroke.HasAnimatedProperties;
}
