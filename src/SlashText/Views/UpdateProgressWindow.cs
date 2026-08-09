using System.Windows;
using System.Windows.Controls;
using SlashText.Models;

namespace SlashText.Views;

internal sealed class UpdateProgressWindow : Window, IProgress<UpdateProgress>
{
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Button _cancel;

    public UpdateProgressWindow()
    {
        Title = "Atualizando o SlashDesk";
        Width = 500;
        Height = 190;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Preparando atualização segura",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        _status = new TextBlock
        {
            Text = "Conectando ao GitHub...",
            Margin = new Thickness(0, 12, 0, 8)
        };
        panel.Children.Add(_status);
        _progress = new ProgressBar { Height = 8, IsIndeterminate = true };
        panel.Children.Add(_progress);
        _cancel = new Button
        {
            Content = "Cancelar",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        _cancel.Click += (_, _) => Cancellation.Cancel();
        panel.Children.Add(_cancel);
        Content = panel;
        Closing += (_, args) =>
        {
            if (!CanClose)
            {
                args.Cancel = true;
            }
        };
    }

    public CancellationTokenSource Cancellation { get; } = new();
    public bool CanClose { get; private set; }

    public void Report(UpdateProgress value)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Report(value));
            return;
        }
        _status.Text = value.Stage;
        _cancel.IsEnabled = !value.IsApplying;
        if (value.TotalBytes is > 0)
        {
            _progress.IsIndeterminate = false;
            _progress.Minimum = 0;
            _progress.Maximum = value.TotalBytes.Value;
            _progress.Value = Math.Min(value.BytesReceived, value.TotalBytes.Value);
        }
        else
        {
            _progress.IsIndeterminate = true;
        }
    }

    public void AllowClose()
    {
        CanClose = true;
        Close();
    }
}
