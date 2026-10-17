using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Design;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;
using Button = System.Windows.Controls.Button;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;

namespace SlashText;

public partial class MainWindow
{
    private async void OpenCaptureImage_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = UiText.Get("Abrir imagem no editor"),
            Filter = UiText.Get("Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos|*.*")
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (await LoadCaptureWorkbenchImageAsync(dialog.FileName, null,
                UiText.Format($"Imagem · {Path.GetFileName(dialog.FileName)}")))
        {
            StatusText.Text = UiText.Format($"Imagem aberta: {Path.GetFileName(dialog.FileName)}");
        }
    }

    private async Task<bool> LoadCaptureWorkbenchImageAsync(
        string path,
        CaptureRecord? record,
        string details, bool showProgress = true)
    {
        if (record is not null && !string.Equals(_captureService.ResolveFilePath(record), path, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }
        if (_captureWorkbenchPath == path && CaptureInlineEditor.HasImage) return true;
        if (!CanReplaceCaptureSession()) return false;
        try
        {
            _captureImageLoadCancellation?.Cancel();
            using var cancellation = new CancellationTokenSource();
            _captureImageLoadCancellation = cancellation;
            var originalDocument = CaptureInlineEditor.Document;
            var originalRevision = originalDocument?.Revision;
            var progress = showProgress ? new ExternalOperationWindow(UiText.Get("Abrindo imagem…"), cancellation) { Owner = this } : null;
            progress?.Show();
            try
            {
                using var bitmap = await ExternalImageReader.DecodeAsync(() => ExternalImageReader.Load(path, cancellation.Token), cancellation.Token);
                if (cancellation.IsCancellationRequested || _servicesDisposed || _settingsOperationInProgress ||
                    !ReferenceEquals(originalDocument, CaptureInlineEditor.Document) || originalRevision != CaptureInlineEditor.Document?.Revision ||
                    (record is not null && (!string.Equals(_captureService.ResolveFilePath(record), path, StringComparison.OrdinalIgnoreCase) || !_captureService.History.Contains(record)))) return false;
                CaptureInlineEditor.LoadImage(bitmap);
            }
            finally
            {
                progress?.Complete();
                if (ReferenceEquals(_captureImageLoadCancellation, cancellation)) _captureImageLoadCancellation = null;
            }
            CapturePreviewEmptyPanel.Visibility = Visibility.Collapsed;
            CaptureWorkbenchToolbar.Visibility = Visibility.Visible;
            CaptureWorkbenchFooter.Visibility = Visibility.Visible;
            CaptureEditorContext.Visibility = Visibility.Visible;
            _captureMediaMode = CaptureMediaMode.Image;
            CaptureImageMediaButton.IsChecked = true;
            UpdateCaptureLauncherSummary();
            CapturePreviewDetailsText.Text = details;
            _captureWorkbenchPath = path;
            _captureWorkbenchRecord = record;
            UpdateCaptureWorkbenchToolState(null);
            if (CaptureWorkbenchToolbar.IsLoaded)
            {
                LabMotion.PlayEntrance(CaptureWorkbenchToolbar);
            }
            return true;
        }
        catch (OperationCanceledException) { StatusText.Text = UiText.Get("Operação cancelada. Os dados atuais foram preservados."); return false; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            StatusText.Text = UiText.Get("Não foi possível abrir a imagem selecionada") + ": " + exception.Message;
            return false;
        }
    }

    private void SelectCaptureWorkbenchTool_OnClick(object sender, RoutedEventArgs e)
    {
        if (!CaptureInlineEditor.HasImage)
        {
            ShowAppDialog(UiText.Get("Abra uma imagem ou faça uma captura antes de editar."), UiText.Get("Edição rápida"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CaptureAnnotationKind? selected = null;
        if (sender is Button { CommandParameter: string tool } &&
            Enum.TryParse(tool, true, out CaptureAnnotationKind parsed))
        {
            selected = parsed;
        }
        CaptureInlineEditor.SelectTool(selected);
        UpdateCaptureWorkbenchToolState(selected);
        StatusText.Text = selected is null
            ? UiText.Get("Seleção ativada")
            : UiText.Format($"{CaptureWorkbenchToolLabel(selected.Value)} ativado — arraste sobre a imagem");
    }

    private static string CaptureWorkbenchToolLabel(CaptureAnnotationKind tool) => tool switch
    {
        CaptureAnnotationKind.Pencil => UiText.Get("Caneta"),
        CaptureAnnotationKind.Highlighter => UiText.Get("Marca-texto"),
        CaptureAnnotationKind.Arrow => UiText.Get("Seta"),
        CaptureAnnotationKind.Rectangle => UiText.Get("Retângulo"),
        CaptureAnnotationKind.Text => UiText.Get("Texto"),
        CaptureAnnotationKind.Stamp => "Emoji",
        CaptureAnnotationKind.Ellipse => UiText.Get("Elipse"),
        CaptureAnnotationKind.Line => UiText.Get("Linha"),
        CaptureAnnotationKind.Number => UiText.Get("Número"),
        CaptureAnnotationKind.Blur => UiText.Get("Desfocar"),
        CaptureAnnotationKind.Pixelate => UiText.Get("Pixelizar"),
        _ => UiText.Get("Ferramenta")
    };

    private void UpdateCaptureWorkbenchToolState(CaptureAnnotationKind? selected)
    {
        CaptureSelectToolButton.Tag = selected is null && !CaptureInlineEditor.IsCropTool ? "Selected" : null;
        CapturePencilToolButton.Tag = selected == CaptureAnnotationKind.Pencil ? "Selected" : null;
        CaptureHighlighterToolButton.Tag = selected == CaptureAnnotationKind.Highlighter ? "Selected" : null;
        CaptureArrowToolButton.Tag = selected == CaptureAnnotationKind.Arrow ? "Selected" : null;
        CaptureRectangleToolButton.Tag = selected == CaptureAnnotationKind.Rectangle ? "Selected" : null;
        CaptureTextToolButton.Tag = selected == CaptureAnnotationKind.Text ? "Selected" : null;
        CaptureStampToolButton.Tag = selected == CaptureAnnotationKind.Stamp ? "Selected" : null;
        CaptureEllipseToolButton.Tag = selected == CaptureAnnotationKind.Ellipse ? "Selected" : null;
        CaptureLineToolButton.Tag = selected == CaptureAnnotationKind.Line ? "Selected" : null;
        CaptureNumberToolButton.Tag = selected == CaptureAnnotationKind.Number ? "Selected" : null;
        CaptureBlurToolButton.Tag = selected == CaptureAnnotationKind.Blur ? "Selected" : null;
        CapturePixelateToolButton.Tag = selected == CaptureAnnotationKind.Pixelate ? "Selected" : null;
        CaptureCropToolButton.Tag = CaptureInlineEditor.IsCropTool ? "Selected" : null;
    }

    private void CaptureInlineEditor_OnStateChanged(object? sender, EventArgs e)
    {
        if (CaptureWorkbenchUndoButton is null || CaptureWorkbenchRedoButton is null) return;
        var ink = DrawingColor.FromArgb(CaptureInlineEditor.InkArgb);
        CaptureWorkbenchColorSwatch.Background = new SolidColorBrush(Color.FromArgb(ink.A, ink.R, ink.G, ink.B));
        var size = CaptureInlineEditor.InkThickness.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        var sizeItem = CaptureWorkbenchThicknessBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == size);
        if (sizeItem is null)
        {
            sizeItem = new ComboBoxItem { Content = size, Tag = size };
            var index = CaptureWorkbenchThicknessBox.Items.OfType<ComboBoxItem>().Count(i => int.Parse(i.Tag!.ToString()!) < (int)CaptureInlineEditor.InkThickness);
            CaptureWorkbenchThicknessBox.Items.Insert(index, sizeItem);
        }
        CaptureWorkbenchThicknessBox.SelectedItem = sizeItem;
        CaptureWorkbenchUndoButton.IsEnabled = CaptureInlineEditor.CanUndo;
        CaptureWorkbenchRedoButton.IsEnabled = CaptureInlineEditor.CanRedo;
        CaptureEditorContext.Refresh();
        if (_captureEditorExpanded) Dispatcher.BeginInvoke(new Action(() => UpdateCaptureEditorViewport()));
        UpdateCaptureWorkbenchToolState(CaptureInlineEditor.SelectedTool);
        var ready = CaptureInlineEditor.HasImage && !CaptureInlineEditor.HasPendingCrop && !_captureCommitInProgress;
        CaptureCopyImageButton.IsEnabled = ready;
        CaptureSaveImageButton.IsEnabled = ready;
        CaptureCompleteImageButton.IsEnabled = ready;
        CaptureExpandEditorButton.IsEnabled = CaptureInlineEditor.HasImage;
        CaptureDiscardChangesButton.IsEnabled = CaptureInlineEditor.HasUnsavedChanges && !_captureCommitInProgress;
        CaptureEditorSessionText.Text = CaptureInlineEditor.Document is { } document
            ? UiText.Format($"{document.Dimensions.Width} × {document.Dimensions.Height} px · {document.OperationCount} edição(ões) · ") +
              (document.HasUnsavedChanges ? UiText.Get("Alterações não salvas") : UiText.Get("Sem alterações pendentes"))
            : UiText.Get("Uma sessão · edição não destrutiva");
        foreach (ComboBoxItem item in CaptureWorkbenchZoomBox.Items)
            if (item.Tag is string value && double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var zoom) && Math.Abs(zoom - CaptureInlineEditor.Zoom) < .001)
            { CaptureWorkbenchZoomBox.SelectedItem = item; break; }
    }

    private void CaptureWorkbenchUndo_OnClick(object sender, RoutedEventArgs e) =>
        CaptureInlineEditor.Undo();

    private void CaptureWorkbenchRedo_OnClick(object sender, RoutedEventArgs e) =>
        CaptureInlineEditor.Redo();

    private void CaptureWorkbenchThickness_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptureWorkbenchThicknessBox?.SelectedItem is ComboBoxItem { Tag: string value } &&
            float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var thickness))
        {
            CaptureInlineEditor?.SetThickness(thickness);
        }
    }

    private void CycleCaptureWorkbenchColor_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button anchor) CaptureInkPicker.Show(anchor, CaptureInlineEditor);
    }

    private void OpenAdvancedCaptureEditor_OnClick(object sender, RoutedEventArgs e)
    {
        if (!CaptureInlineEditor.HasImage)
        {
            ShowAppDialog(UiText.Get("Abra uma imagem ou faça uma captura antes de editar."), UiText.Get("Editor avançado"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetCaptureEditorExpanded(!_captureEditorExpanded);
    }

    private void CaptureMoreTools_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button anchor) CaptureEditorContext.ShowMoreTools(anchor);
    }

    private void CaptureCropTool_OnClick(object sender, RoutedEventArgs e) => CaptureInlineEditor.SelectCrop();
    private void CaptureResizeTool_OnClick(object sender, RoutedEventArgs e) => CaptureEditorContext.ShowResize();

    private void UpdateCaptureToolbarLayout()
    {
        if (CaptureOutputCommands is null || CaptureToolCommands is null) return;
        // Full tools need their own row on smaller viewports; never hide them again.
        var compact = CaptureWorkbenchToolbar.ActualWidth < (_captureEditorExpanded ? 1250 : 1060);
        Grid.SetRow(CaptureOutputCommands, compact ? 1 : 0);
        Grid.SetColumn(CaptureOutputCommands, compact ? 0 : 1);
        Grid.SetColumnSpan(CaptureOutputCommands, compact ? 2 : 1);
        Grid.SetColumnSpan(CaptureToolCommands, compact ? 2 : 1);
        CaptureOutputCommands.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private void CaptureWorkbenchToolbar_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCaptureToolbarLayout();
    }

    private void SetCaptureEditorExpanded(bool expanded)
    {
        if (_captureEditorExpanded == expanded) return;
        _captureEditorExpanded = expanded;
        CaptureMoreToolsButton.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        foreach (var tool in new[] { CaptureEllipseToolButton, CaptureLineToolButton, CaptureNumberToolButton,
                     CaptureBlurToolButton, CapturePixelateToolButton, CaptureCropToolButton, CaptureResizeToolButton })
            tool.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        UpdateCaptureToolbarLayout();
        CaptureExpandEditorButton.Content = expanded ? UiText.Get("Voltar ao painel · Esc") : UiText.Get("Expandir editor");
        if (expanded)
        {
            _capturePageVisibility.Clear();
            _capturePageRowHeights = CapturePageLayout.RowDefinitions.Select(r => r.Height).ToArray();
            foreach (UIElement child in CapturePageLayout.Children)
                if (Grid.GetRow(child) != 8) { _capturePageVisibility[child] = child.Visibility; child.Visibility = Visibility.Collapsed; }
            for (var i = 0; i < CapturePageLayout.RowDefinitions.Count; i++)
                if (i != 8) CapturePageLayout.RowDefinitions[i].Height = new GridLength(0);
        }
        else
        {
            foreach (var pair in _capturePageVisibility) pair.Key.Visibility = pair.Value;
            if (_capturePageRowHeights is not null)
                for (var i = 0; i < _capturePageRowHeights.Length; i++) CapturePageLayout.RowDefinitions[i].Height = _capturePageRowHeights[i];
        }
        CaptureShellHeader.Visibility = CaptureShellNavigation.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        ShellRoot.RowDefinitions[1].Height = new GridLength(expanded ? 0 : 66);
        ShellRoot.RowDefinitions[2].Height = new GridLength(expanded ? 0 : 52);
        UpdateCaptureEditorViewport(animate: true);
        if (expanded) Dispatcher.BeginInvoke(new Action(() => UpdateCaptureEditorViewport(animate: true)), DispatcherPriority.ApplicationIdle);
        CaptureInlineEditor.Focus();
        CaptureInlineEditor.BringIntoView();
    }

    private void UpdateCaptureEditorViewport(bool animate = false)
    {
        if (CaptureEditorViewport is null) return;
        // Desired size excludes the unused stretch space of the page. ActualHeight
        // would count that blank area as chrome and keep the viewport at its minimum.
        var outside = Math.Max(0, CapturePageLayout.DesiredSize.Height - CaptureEditorViewport.DesiredSize.Height);
        var height = _captureEditorExpanded
            ? Math.Max(240, CaptureView.ActualHeight > 0 ? CaptureView.ActualHeight - outside - 2 : Height - 330)
            : 480;
        if (animate && LabMotion.Allowed(CaptureEditorViewport))
            CaptureEditorViewport.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(height, TimeSpan.FromMilliseconds(220))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        else { CaptureEditorViewport.BeginAnimation(FrameworkElement.HeightProperty, null); CaptureEditorViewport.Height = height; }
    }

    private bool CanReplaceCaptureSession()
    {
        if (_pendingCaptureEdit is not null || _captureCommitInProgress)
        { StatusText.Text = UiText.Get("Conclua ou cancele a captura atual antes de abrir outra imagem."); return false; }
        if (!CaptureInlineEditor.HasUnsavedChanges && !CaptureInlineEditor.HasPendingCrop) return true;
        return ShowAppDialog(UiText.Get("Há alterações ou uma seleção de recorte pendentes. Descartá-las e abrir outra imagem?"),
            UiText.Get("Editor de imagem"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    internal void PrepareCaptureEvidence(DrawingBitmap source, CaptureAnnotationKind tool, bool expanded)
    {
        ShowView(CaptureView, CaptureTabButton);
        if (!CaptureInlineEditor.HasImage)
        {
            CaptureInlineEditor.LoadImage(source);
            CaptureInlineEditor.InsertStamp("⭐", new Point(300, 180));
        }
        CapturePreviewEmptyPanel.Visibility = Visibility.Collapsed;
        CaptureWorkbenchToolbar.Visibility = CaptureWorkbenchFooter.Visibility = CaptureEditorContext.Visibility = Visibility.Visible;
        CapturePreviewDetailsText.Text = UiText.Get("Imagem de teste local · sessão única");
        CaptureInlineEditor.SelectTool(tool);
        SetCaptureEditorExpanded(expanded);
    }

    internal CaptureEditorDocument? CaptureEvidenceDocument => CaptureInlineEditor.Document;
    internal void ResizeCaptureEvidenceViewport() => UpdateCaptureEditorViewport();
    internal void DisposeCaptureEvidence() => CaptureInlineEditor.Dispose();

    private void CaptureDiscardChanges_OnClick(object sender, RoutedEventArgs e)
    {
        if (ShowAppDialog(UiText.Get("Voltar à última versão salva nesta sessão?"), UiText.Get("Descartar alterações"), MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes) CaptureInlineEditor.DiscardChanges();
    }

    private async Task<CaptureImageEditResult?> EditNewCaptureInWorkbenchAsync(DrawingBitmap source, CaptureAnnotationKind initialTool)
    {
        if (!CanReplaceCaptureSession()) return null;
        CaptureInlineEditor.LoadImage(source);
        _captureWorkbenchPath = null; _captureWorkbenchRecord = null;
        _captureMediaMode = CaptureMediaMode.Image; CaptureImageMediaButton.IsChecked = true;
        UpdateCaptureLauncherSummary();
        CapturePreviewEmptyPanel.Visibility = Visibility.Collapsed;
        CaptureWorkbenchToolbar.Visibility = CaptureWorkbenchFooter.Visibility = CaptureEditorContext.Visibility = Visibility.Visible;
        CaptureInlineEditor.SelectTool(initialTool);
        CapturePreviewDetailsText.Text = UiText.Get("Nova captura · conclua para aplicar a regra");
        _pendingCaptureEdit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CaptureCancelPendingButton.Visibility = Visibility.Visible;
        ShowFromTray(); ShowView(CaptureView, CaptureTabButton); CaptureInlineEditor.BringIntoView(); CaptureInlineEditor.Focus();
        try { return await _pendingCaptureEdit.Task; }
        finally { _pendingCaptureEdit = null; CaptureCancelPendingButton.Visibility = Visibility.Collapsed; }
    }

    private void CaptureCancelPending_OnClick(object sender, RoutedEventArgs e)
    {
        _pendingCaptureEdit?.TrySetResult(null);
        CaptureInlineEditor.Clear();
        CapturePreviewEmptyPanel.Visibility = Visibility.Visible;
        CaptureWorkbenchToolbar.Visibility = CaptureWorkbenchFooter.Visibility = CaptureEditorContext.Visibility = Visibility.Collapsed;
        StatusText.Text = UiText.Get("Captura cancelada sem salvar");
    }

    private void AcceptCompletedCapture(CaptureRecord result)
    {
        _captureCommitInProgress = false;
        if (_completedCaptureDocument is not null && ReferenceEquals(_completedCaptureDocument, CaptureInlineEditor.Document))
        {
            _captureWorkbenchRecord = result;
            _captureWorkbenchPath = _captureService.ResolveFilePath(result);
            if (!string.IsNullOrWhiteSpace(_captureWorkbenchPath)) CaptureInlineEditor.MarkSaved();
        }
        else if (!CaptureInlineEditor.HasUnsavedChanges && !string.IsNullOrWhiteSpace(result.FilePath))
            _ = LoadCaptureWorkbenchImageAsync(_captureService.ResolveFilePath(result), result, $"{CaptureTypeLabel(result)} · {result.Width}×{result.Height}", showProgress: false);
        _completedCaptureDocument = null;
        CaptureInlineEditor_OnStateChanged(this, EventArgs.Empty);
    }

    private Microsoft.Win32.SaveFileDialog CreateCapturePreviewSaveDialog(string title)
    {
        var sourcePath = _captureWorkbenchPath ?? "captura.png";
        var extension = Path.GetExtension(sourcePath);
        var jpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        extension = jpeg ? ".jpg" : ".png";
        return new Microsoft.Win32.SaveFileDialog
        {
            Title = title,
            FileName = Path.GetFileNameWithoutExtension(sourcePath) + "-editada" + extension,
            DefaultExt = extension,
            Filter = jpeg
                ? "JPEG|*.jpg|PNG|*.png"
                : "PNG|*.png|JPEG|*.jpg"
        };
    }

    private async Task<bool> CommitCaptureWorkbenchImageAsync(DrawingBitmap bitmap, string path)
    {
        _captureCommitInProgress = true;
        CaptureEditorCard.IsEnabled = false;
        CaptureInlineEditor_OnStateChanged(this, EventArgs.Empty);
        try
        {
            var record = await _captureService.SaveEditedImageAsync(bitmap, path, _settings.Capture, _captureWorkbenchRecord);
            _captureWorkbenchPath = path;
            _captureWorkbenchRecord = record;
            CaptureInlineEditor.MarkSaved();
            CapturePreviewDetailsText.Text = $"{CaptureTypeLabel(record)} · {record.Width}×{record.Height}";
            RefreshCaptureHistory();
            StatusText.Text = UiText.Format($"Edição salva e atualizada nos Recentes: {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            ShowAppDialog(exception.Message, UiText.Get("Salvar captura"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            _captureCommitInProgress = false;
            CaptureEditorCard.IsEnabled = true;
            CaptureInlineEditor_OnStateChanged(this, EventArgs.Empty);
        }
    }

    private void CopyCapturePreview_OnClick(object sender, RoutedEventArgs e)
    {
        if (CaptureInlineEditor.HasPendingCrop || _captureCommitInProgress) return;
        if (!CaptureInlineEditor.HasImage)
        {
            ShowAppDialog(UiText.Get("Não há uma imagem no editor."), UiText.Get("Copiar"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        using var rendered = CaptureInlineEditor.Render();
        try
        {
            Clipboard.SetImage(ToBitmapSource(rendered));
            StatusText.Text = UiText.Get("Imagem copiada para a área de transferência");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        { StatusText.Text = UiText.Get("A área de transferência está ocupada. Tente copiar novamente."); }
    }

    private async void SaveCapturePreview_OnClick(object sender, RoutedEventArgs e)
    {
        if (CaptureInlineEditor.HasPendingCrop || _captureCommitInProgress) return;
        if (!CaptureInlineEditor.HasImage)
        {
            ShowAppDialog(UiText.Get("Não há uma imagem no editor."), UiText.Get("Salvar"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = CreateCapturePreviewSaveDialog(UiText.Get("Salvar uma cópia"));
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        using var rendered = CaptureInlineEditor.Render();
        await CommitCaptureWorkbenchImageAsync(rendered, dialog.FileName);
    }

    private async void CompleteCapturePreview_OnClick(object sender, RoutedEventArgs e)
    {
        if (CaptureInlineEditor.HasPendingCrop || _captureCommitInProgress) return;
        if (_pendingCaptureEdit is not null)
        {
            _completedCaptureDocument = CaptureInlineEditor.Document;
            _captureCommitInProgress = true;
            CaptureEditorCard.IsEnabled = false;
            _pendingCaptureEdit.TrySetResult(new CaptureImageEditResult(CaptureInlineEditor.Render(), CaptureEditorOutput.Default));
            CaptureInlineEditor_OnStateChanged(this, EventArgs.Empty);
            return;
        }
        if (!CaptureInlineEditor.HasImage)
        {
            ShowAppDialog(UiText.Get("Abra uma imagem ou faça uma captura antes de concluir."), UiText.Get("Concluir"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        using var rendered = CaptureInlineEditor.Render();
        var saved = false;
        if (_captureWorkbenchRecord?.MediaKind.Equals("image", StringComparison.OrdinalIgnoreCase) == true &&
            !string.IsNullOrWhiteSpace(_captureWorkbenchPath) &&
            File.Exists(_captureWorkbenchPath))
        {
            if (!await CommitCaptureWorkbenchImageAsync(rendered, _captureWorkbenchPath)) return;
            saved = true;
        }
        else if (_settings.Capture.SaveAutomatically)
        {
            var dialog = CreateCapturePreviewSaveDialog(UiText.Get("Salvar edição"));
            if (dialog.ShowDialog(this) != true || !await CommitCaptureWorkbenchImageAsync(rendered, dialog.FileName)) return;
            saved = true;
        }
        if (_settings.Capture.CopyToClipboard)
        {
            try { Clipboard.SetImage(ToBitmapSource(rendered)); }
            catch (System.Runtime.InteropServices.ExternalException)
            { StatusText.Text = saved ? UiText.Get("Edição salva; a área de transferência está ocupada. Tente copiar novamente.") : UiText.Get("A área de transferência está ocupada. Tente copiar novamente."); return; }
        }
        StatusText.Text = saved
            ? UiText.Get("Edição concluída e atualizada nos Recentes")
            : _settings.Capture.CopyToClipboard ? UiText.Get("Captura copiada; a sessão permanece disponível") : UiText.Get("Resultado mantido apenas na sessão — copiar e salvar estão desativados na regra");
    }

    private void CaptureWorkbenchZoom_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptureWorkbenchZoomBox?.SelectedItem is not ComboBoxItem { Tag: string value } ||
            !double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var zoom))
        {
            return;
        }
        _captureWorkbenchZoom = zoom;
        if (CaptureInlineEditor is not null)
        {
            CaptureInlineEditor.SetZoom(zoom);
        }
    }

    private static BitmapSource ToBitmapSource(DrawingBitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void CaptureFormat_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptureQualityBox is not null && CaptureFormatBox is not null)
        {
            CaptureQualityBox.IsEnabled =
                SelectedTag(CaptureFormatBox, "PNG").Equals(
                    "JPEG",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
