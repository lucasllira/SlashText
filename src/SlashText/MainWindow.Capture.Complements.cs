using System.Windows;
using System.Windows.Controls;
using SlashText.Design;
using SlashText.Views;

namespace SlashText;

public partial class MainWindow
{
    private void CaptureHistorySearch_OnChanged(object sender, TextChangedEventArgs e) => RefreshCaptureHistory();
    private void ClearCaptureHistorySearch_OnClick(object sender, RoutedEventArgs e) => CaptureHistorySearchBox.Clear();
    private void OpenFullCaptureHistory_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CaptureHistoryWindow(_captureService.History, _captureService.ResolveFilePath,
            CaptureHistorySearchBox.Text, SelectedTag(CaptureHistoryFilterBox, "all")) { Owner = this };
        LabMotion.SetReduced(dialog, LabMotion.GetReduced(this)); ShowCaptureDialog(dialog);
        if (dialog.RequestedRecord is not { } record) return;
        var button = new Button { Tag = record };
        switch (dialog.RequestedAction)
        {
            case "open": OpenHistoryItem_OnClick(button, new RoutedEventArgs()); break;
            case "copy": CopyHistoryItem_OnClick(button, new RoutedEventArgs()); break;
            case "edit": EditHistoryItem_OnClick(button, new RoutedEventArgs()); break;
            case "delete": DeleteHistoryItem_OnClick(button, new RoutedEventArgs()); break;
        }
    }
}
