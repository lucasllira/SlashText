using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Design;
using SlashText.Models;
using SlashText.Services;
using Button = System.Windows.Controls.Button;

namespace SlashText;

public partial class MainWindow
{
    private void RefreshCaptureHistory()
    {
        if (CaptureHistoryPanel is null)
        {
            return;
        }

        CaptureHistoryPanel.Children.Clear();
        _captureThumbnailCancellation?.Cancel(); _captureThumbnailCancellation?.Dispose();
        _captureThumbnailCancellation = new CancellationTokenSource();
        CaptureHistoryScroller.ScrollToLeftEnd();

        var mostRecent = _captureService.History.FirstOrDefault();
        if (!CaptureInlineEditor.HasImage && mostRecent is not null)
        {
            var mostRecentPath = _captureService.ResolveFilePath(mostRecent);
            if (mostRecent.MediaKind.Equals("image", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(mostRecentPath) &&
                File.Exists(mostRecentPath))
            {
                _ = LoadCaptureWorkbenchImageAsync(
                    mostRecentPath,
                    mostRecent,
                    $"{CaptureTypeLabel(mostRecent)} · {mostRecent.Width}×{mostRecent.Height}", showProgress: false);
            }
        }

        var filter = CaptureHistoryFilterBox is null
            ? "all"
            : SelectedTag(CaptureHistoryFilterBox, "all");
        var filtered = CaptureHistoryQuery.Filter(_captureService.History, filter, CaptureHistorySearchBox?.Text ?? "");
        foreach (var item in filtered.Take(40))
        {
            CaptureHistoryPanel.Children.Add(BuildCaptureHistoryCard(item));
        }
        if (CaptureHistoryPanel.Children.Count == 0)
        {
            var empty = new Border
            {
                Padding = new Thickness(18),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Child = new TextBlock
                {
                    Text = _captureService.History.Count > 0 ? UiText.Get("Nenhuma captura corresponde à busca e ao filtro.") : UiText.Get("As últimas capturas aparecerão aqui."),
                    Foreground = (Brush)FindResource("MutedBrush")
                }
            };
            empty.SetResourceReference(Border.BackgroundProperty, "Lab.panel");
            empty.SetResourceReference(Border.BorderBrushProperty, "Lab.line");
            CaptureHistoryPanel.Children.Add(empty);
        }
        RefreshStorageProtection();
        CaptureHistoryStatusText.Text = filtered.Count > 40 ? UiText.Format($"40 de {filtered.Count:N0} · Ver todas") : filtered.Count.ToString("N0");
        Dispatcher.BeginInvoke(new Action(UpdateCaptureHistoryNavigationState));
    }

    private Border BuildCaptureHistoryCard(CaptureRecord record)
    {
        var path = _captureService.ResolveFilePath(record);
        var card = new Border
        {
            Width = 370,
            Height = 96,
            Margin = new Thickness(0, 0, 12, 2),
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            ToolTip = UiText.Get("Selecionar no editor"),
            Tag = record,
            Cursor = Cursors.Hand,
            RenderTransform = new TranslateTransform(),
            RenderTransformOrigin = new Point(.5, .5)
        };
        card.SetResourceReference(Border.BackgroundProperty, "Lab.panel");
        card.SetResourceReference(Border.BorderBrushProperty, "Lab.line");
        LabMotion.SetEntrance(card, "Page");
        card.MouseLeftButtonUp += CaptureHistoryCard_OnClick;
        card.MouseEnter += (_, _) =>
        {
            card.SetResourceReference(Border.BorderBrushProperty, "Lab.accent");
            if (card.RenderTransform is TranslateTransform transform)
            {
                AnimateHistoryCard(transform, -3, card);
            }
        };
        card.MouseLeave += (_, _) =>
        {
            card.SetResourceReference(Border.BorderBrushProperty, "Lab.line");
            if (card.RenderTransform is TranslateTransform transform)
            {
                AnimateHistoryCard(transform, 0, card);
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var thumbnail = new Border
        {
            Margin = new Thickness(0, 0, 12, 0),
            CornerRadius = new CornerRadius(5),
            ClipToBounds = true
        };
        thumbnail.SetResourceReference(Border.BackgroundProperty, "Lab.canvas");
        {
            var icon = new System.Windows.Shapes.Path
            {
                Data = (Geometry)FindResource(record.MediaKind.Equals("video", StringComparison.OrdinalIgnoreCase)
                    ? "Lab.Icon.Video"
                    : "Lab.Icon.Image"),
                Width = 26,
                Height = 26,
                Stretch = Stretch.Uniform,
                StrokeThickness = 1.6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Lab.muted");
            thumbnail.Child = icon;
        }
        if (!record.MediaKind.Equals("video", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(path))
            _ = LoadHistoryThumbnailAsync(thumbnail, path, record, _captureThumbnailCancellation?.Token ?? default);
        grid.Children.Add(thumbnail);

        // Display metadata without granting access for a thumbnail or other file action.
        var displayPath = CapturePathResolver.Resolve(record, AppPaths.Current);
        var title = string.IsNullOrWhiteSpace(displayPath)
            ? CaptureTypeLabel(record)
            : Path.GetFileNameWithoutExtension(displayPath);
        var duration = record.DurationSeconds > 0
            ? $" · {TimeSpan.FromSeconds(record.DurationSeconds):mm\\:ss}"
            : string.Empty;
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 170
        });
        copy.Children.Add(new TextBlock
        {
            Text = record.RequiresFileApproval && string.IsNullOrEmpty(path)
                ? UiText.Get("Confirme o arquivo do histórico")
                : $"{CaptureTypeLabel(record)} · {record.CreatedAt:dd/MM HH:mm}{duration}",
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180
        });
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(HistoryButton("ExternalLink", UiText.Get("Abrir"), record, OpenHistoryItem_OnClick));
        actions.Children.Add(HistoryButton("Copy", UiText.Get("Copiar"), record, CopyHistoryItem_OnClick));
        if (record.MediaKind.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            actions.Children.Add(HistoryButton("PenLine", UiText.Get("Editar"), record, EditHistoryItem_OnClick));
        }
        actions.Children.Add(HistoryButton("X", UiText.Get("Excluir"), record, DeleteHistoryItem_OnClick));
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);
        card.Child = grid;
        return card;
    }

    private async void CaptureHistoryCard_OnClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: CaptureRecord record })
        {
            return;
        }
        if (!TryAuthorizeHistoryFile(record, out var path)) return;
        if (!record.MediaKind.Equals("image", StringComparison.OrdinalIgnoreCase))
        {
            OpenHistoryItem_OnClick(new Button { Tag = record }, new RoutedEventArgs());
            return;
        }
        if (await LoadCaptureWorkbenchImageAsync(path, record,
                $"{CaptureTypeLabel(record)} · {record.Width}×{record.Height}"))
        {
            CaptureInlineEditor.BringIntoView();
            StatusText.Text = UiText.Get("Captura selecionada no editor");
        }
    }

    private static string CaptureTypeLabel(CaptureRecord record) => record.MediaKind.ToLowerInvariant() switch
    {
        "gif" => "GIF",
        "video" => UiText.Get("Vídeo MP4"),
        _ => record.Type.ToLowerInvariant() switch
        {
            "monitor" => "Monitor",
            "regiao" => UiText.Get("Região"),
            "janela" => UiText.Get("Janela"),
            "rolagem" => UiText.Get("Captura longa"),
            _ => UiText.Get("Imagem")
        }
    };

    private async Task LoadHistoryThumbnailAsync(Border thumbnail, string path, CaptureRecord record, CancellationToken token)
    {
        try
        {
            var image = await ExternalImageReader.DecodeAsync(() => TryLoadImage(path, out var result) ? result : null, token);
            if (image is not null && !token.IsCancellationRequested && !_servicesDisposed &&
                _captureService.History.Contains(record) && string.Equals(_captureService.ResolveFilePath(record), path, StringComparison.OrdinalIgnoreCase))
                thumbnail.Child = new Image { Source = image, Stretch = Stretch.UniformToFill };
        }
        catch (OperationCanceledException) { }
    }

    private static bool TryLoadImage(string path, out BitmapImage? image)
    {
        image = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dimensions = ExternalImageReader.CheckHeader(stream);
            var loaded = new BitmapImage();
            loaded.BeginInit();
            loaded.CacheOption = BitmapCacheOption.OnLoad;
            var scale = Math.Min(1d, Math.Min(240d / dimensions.Width, 240d / dimensions.Height));
            loaded.DecodePixelWidth = Math.Max(1, (int)Math.Ceiling(dimensions.Width * scale));
            loaded.DecodePixelHeight = Math.Max(1, (int)Math.Ceiling(dimensions.Height * scale));
            loaded.StreamSource = stream;
            loaded.EndInit();
            loaded.Freeze();
            image = loaded;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static Button HistoryButton(
        string iconKind,
        string toolTip,
        CaptureRecord record,
        RoutedEventHandler handler)
    {
        var button = new Button
        {
            Tag = record,
            ToolTip = toolTip,
            MinWidth = 28,
            Width = 28,
            Height = 30,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(2)
        };
        button.SetResourceReference(StyleProperty, "Lab.Pilot.ToolButton");
        var icon = new LabIcon { Kind = iconKind, Width = 15, Height = 15 };
        icon.SetResourceReference(LabIcon.ForegroundProperty, "Lab.text");
        button.Content = icon;
        button.Click += (sender, args) =>
        {
            args.Handled = true;
            handler(sender, args);
        };
        return button;
    }

    private static void AnimateHistoryCard(
        TranslateTransform transform,
        double target,
        FrameworkElement owner)
    {
        if (!SystemParameters.ClientAreaAnimation || LabMotion.GetReduced(owner))
        {
            transform.Y = target;
            return;
        }
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        });
    }

    private void ScrollCaptureHistoryPrevious_OnClick(object sender, RoutedEventArgs e) =>
        ScrollCaptureHistoryBy(-Math.Max(300, CaptureHistoryScroller.ViewportWidth * .82));

    private void ScrollCaptureHistoryNext_OnClick(object sender, RoutedEventArgs e) =>
        ScrollCaptureHistoryBy(Math.Max(300, CaptureHistoryScroller.ViewportWidth * .82));

    private void ScrollCaptureHistoryBy(double delta)
    {
        var start = CaptureHistoryScroller.HorizontalOffset;
        var target = Math.Clamp(
            start + delta,
            0,
            CaptureHistoryScroller.ScrollableWidth);
        BeginAnimation(CaptureHistoryOffsetProperty, null);
        if (!SystemParameters.ClientAreaAnimation || LabMotion.GetReduced(this))
        {
            CaptureHistoryOffset = target;
            return;
        }
        CaptureHistoryOffset = start;
        var animation = new DoubleAnimation
        {
            From = start,
            To = target,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.Completed += (_, _) =>
        {
            BeginAnimation(CaptureHistoryOffsetProperty, null);
            CaptureHistoryOffset = target;
        };
        BeginAnimation(CaptureHistoryOffsetProperty, animation);
    }

    private void CaptureHistoryScroller_OnScrollChanged(object sender, ScrollChangedEventArgs e) =>
        UpdateCaptureHistoryNavigationState();

    private void UpdateCaptureHistoryNavigationState()
    {
        if (CaptureHistoryPreviousButton is null || CaptureHistoryNextButton is null) return;
        CaptureHistoryPreviousButton.IsEnabled = CaptureHistoryScroller.HorizontalOffset > 1;
        CaptureHistoryNextButton.IsEnabled =
            CaptureHistoryScroller.HorizontalOffset < CaptureHistoryScroller.ScrollableWidth - 1;
    }

    private void CaptureHistoryFilter_OnChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshCaptureHistory();

    private void OpenHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CaptureRecord record } || !TryAuthorizeHistoryFile(record, out var path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception exception)
        { ShowAppDialog(exception.Message, UiText.Get("Histórico"), MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private void CopyHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CaptureRecord record })
        {
            return;
        }
        try
        {
            if (!TryAuthorizeHistoryFile(record, out var path)) return;
            CaptureService.CopyFileToClipboard(path);
            StatusText.Text = UiText.Format($"Captura copiada: {Path.GetFileName(path)}");
        }
        catch (Exception exception)
        {
            ShowAppDialog(
                exception.Message,
                UiText.Get("Histórico"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void EditHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CaptureRecord record })
        {
            return;
        }
        if (!TryAuthorizeHistoryFile(record, out var path)) return;
        if (await LoadCaptureWorkbenchImageAsync(path, record,
                $"{CaptureTypeLabel(record)} · {record.Width}×{record.Height}"))
        {
            ShowView(CaptureView, CaptureTabButton);
            CaptureInlineEditor.BringIntoView(); CaptureInlineEditor.Focus();
            StatusText.Text = UiText.Get("Imagem aberta na sessão única de edição");
        }
    }

    private async void DeleteHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        using var lease = TryReserveCaptureCommand();
        if (lease is null) return;
        if (_captureService.IsHistoryProtected) { ReviewStorage_OnClick(sender, e); return; }
        if (sender is not Button { Tag: CaptureRecord record })
        {
            return;
        }
        var choice = ShowAppDialog(
            UiText.Get("Sim: excluir também o arquivo local.\nNão: remover somente do histórico."),
            UiText.Get("Excluir captura"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (choice == MessageBoxResult.Cancel)
        {
            return;
        }
        if (choice == MessageBoxResult.Yes && !TryAuthorizeHistoryFile(record, out _)) return;
        try
        {
            await _captureService.DeleteAsync(record.Id, deleteFile: choice == MessageBoxResult.Yes);
            RefreshCaptureHistory();
        }
        catch (Exception exception)
        { ShowAppDialog(exception.Message, UiText.Get("Histórico"), MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private async void CleanCaptureHistory_OnClick(object sender, RoutedEventArgs e)
    {
        using var lease = TryReserveCaptureCommand();
        if (lease is null) return;
        if (_captureService.IsHistoryProtected) { ReviewStorage_OnClick(sender, e); return; }
        var days = ParseSelectedInt(CaptureRetentionBox, 90);
        if (days <= 0)
        {
            CaptureHistoryStatusText.Text = UiText.Get("Limpeza automática desativada");
            return;
        }
        var count = await _captureService.CleanOlderThanAsync(days, deleteFiles: false);
        CaptureHistoryStatusText.Text = UiText.Format($"{count:N0} entrada(s) antiga(s) removida(s)");
        RefreshCaptureHistory();
    }

    private void OpenCaptureFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var directory = CaptureService.ResolveDirectoryTemplate(
            Environment.ExpandEnvironmentVariables(_settings.Capture.OutputDirectoryTemplate),
            DateTimeOffset.Now);
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }
}
