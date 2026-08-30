using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlashText.Services;

namespace SlashText.Views;

public partial class CaptureToolbarPreviewWindow : Window
{
    private readonly Dictionary<string, FrameworkElement> _states;
    private readonly Dictionary<string, Button> _stateButtons;

    public CaptureToolbarPreviewWindow()
    {
        InitializeComponent();
        var workArea = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, Math.Min(1280, workArea.Width - 24));
        Height = Math.Max(MinHeight, Math.Min(800, workArea.Height - 24));
        _states = new(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = DefaultState,
            ["capture"] = CaptureState,
            ["shapes"] = ShapesState,
            ["emoji"] = EmojiState
        };
        _stateButtons = new(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = DefaultButton,
            ["capture"] = CaptureButton,
            ["shapes"] = ShapesButton,
            ["emoji"] = EmojiButton
        };
        PopulateNotoEmojiCatalog();
        ShowState("default");
    }

    private void PopulateNotoEmojiCatalog()
    {
        foreach (var emoji in NotoEmojiCatalog.Items)
        {
            var button = new Button
            {
                Style = (Style)FindResource("Preview.EmojiButton"),
                Tag = emoji == NotoEmojiCatalog.Items[0] ? "Selected" : null,
                ToolTip = emoji.Name,
                Content = new Image
                {
                    Source = NotoEmojiCatalog.CreateImageSource(emoji.Value),
                    Width = 28,
                    Height = 28,
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = false
                }
            };
            AutomationProperties.SetName(button, $"Emoticon {emoji.Name}");
            EmojiCatalogGrid.Children.Add(button);
        }
    }

    private void OnStateClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string state }) ShowState(state);
    }

    private void ShowState(string state)
    {
        foreach (var item in _states) item.Value.Visibility =
            item.Key.Equals(state, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        foreach (var item in _stateButtons) item.Value.Tag =
            item.Key.Equals(state, StringComparison.OrdinalIgnoreCase)
                ? "Selected"
                : null;
        StateTitle.Text = state switch
        {
            "capture" => "Menu Capturar",
            "shapes" => "Formas e propriedades",
            "emoji" => "Emoticons e carimbos",
            _ => "Barra principal"
        };
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.D1) ShowState("default");
        else if (e.Key == Key.D2) ShowState("capture");
        else if (e.Key == Key.D3) ShowState("shapes");
        else if (e.Key == Key.D4) ShowState("emoji");
    }
}
