using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlashText.Services;

namespace SlashText.Views;

public sealed class RecordingControlWindow : Window
{
    private readonly IRecordingController _service;
    private readonly string _mediaName;
    private readonly TextBlock _time;
    private readonly TextBlock _status;
    private readonly Button _pause;
    private readonly DispatcherTimer _timer;
    private int _stopRequested;

    internal RecordingControlWindow(IRecordingController service, string mediaName)
    {
        _service = service;
        _mediaName = mediaName;
        Title = "Controle de gravação";
        Width = 380;
        Height = 86;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Bottom - Height - 24;

        var root = new Border
        {
            Background = (Brush)Application.Current.FindResource("PanelBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("DividerBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 12, 10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 4,
                Opacity = .28
            }
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = "●",
            Foreground = (Brush)Application.Current.FindResource("DangerBrush"),
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _time = new TextBlock
        {
            Text = "00:00",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("InkBrush")
        };
        _status = new TextBlock
        {
            Text = $"Gravando {_mediaName}",
            FontSize = 11,
            Foreground = (Brush)Application.Current.FindResource("MutedBrush")
        };
        info.Children.Add(_time);
        info.Children.Add(_status);
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _pause = ActionButton("Pausar", (_, _) => TogglePause());
        actions.Children.Add(_pause);
        var stop = ActionButton("Finalizar", (_, _) => StopOnce());
        stop.Style = (Style)Application.Current.FindResource("PrimaryButton");
        actions.Children.Add(stop);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);
        root.Child = grid;
        Content = root;

        MouseLeftButtonDown += (_, args) =>
        {
            if (args.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        };
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Space)
            {
                TogglePause();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                StopOnce();
                args.Handled = true;
            }
        };
        _service.ProgressChanged += OnProgress;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += Timer_OnTick;
        _timer.Start();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= Timer_OnTick;
            _service.ProgressChanged -= OnProgress;
            AppDiagnosticLog.Write(
                "recording.overlay-closed",
                ("recordingId", _service.RecordingId.ToString("N")),
                ("media", _mediaName),
                ("elapsedMs", _service.Elapsed.TotalMilliseconds));
        };
    }

    private static Button ActionButton(string text, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 76,
            Height = 36,
            Margin = new Thickness(6, 0, 0, 0)
        };
        button.Click += click;
        return button;
    }

    private void TogglePause()
    {
        if (Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }
        if (_service.IsPaused)
        {
            _service.Resume();
        }
        else
        {
            _service.Pause();
        }
    }

    private void OnProgress(object? sender, Models.RecordingProgress progress)
    {
        void Update()
        {
            UpdateElapsed(progress.Elapsed);
            _status.Text = progress.Status;
            _pause.Content = progress.IsPaused ? "Continuar" : "Pausar";
        }
        if (Dispatcher.CheckAccess())
        {
            Update();
        }
        else
        {
            _ = Dispatcher.BeginInvoke(Update);
        }
    }

    private void Timer_OnTick(object? sender, EventArgs e) => UpdateElapsed(_service.Elapsed);

    private void UpdateElapsed(TimeSpan elapsed) => _time.Text = elapsed.TotalHours >= 1
        ? elapsed.ToString(@"hh\:mm\:ss")
        : elapsed.ToString(@"mm\:ss");

    private void StopOnce()
    {
        if (Interlocked.CompareExchange(ref _stopRequested, 1, 0) != 0)
        {
            return;
        }
        AppDiagnosticLog.Write(
            "recording.overlay-finish-clicked",
            ("recordingId", _service.RecordingId.ToString("N")),
            ("media", _mediaName),
            ("elapsedMs", _service.Elapsed.TotalMilliseconds));
        _service.Stop();
        _pause.IsEnabled = false;
        _status.Text = $"Finalizando {_mediaName}…";
    }
}
