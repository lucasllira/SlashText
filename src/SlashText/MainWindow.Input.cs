using System.Windows;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText;

public partial class MainWindow
{
    private void StartMonitoring()
    {
        _keyboardHook.UpdateSnippets(_snippets);
        if (!_keyboardHook.IsRunning)
        {
            _keyboardHook.Start();
        }

        MonitorStatusText.Text = UiText.Get("Monitoramento ativo");
        ShellStatusText.Text = UiText.Get("Monitoramento ativo");
    }

    private void QuickAccentService_OnChanged(object? sender, QuickAccentChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!e.Visible)
            {
                _quickAccentWindow.Hide();
                return;
            }

            _quickAccentWindow.UpdateChoices(
                e.Choices,
                e.SelectedIndex,
                _settings.QuickAccentToolbarPosition,
                _settings.QuickAccentShowUnicode);
        }));
    }

    private void QuickAccentService_OnCharacterInserted(
        object? sender,
        QuickAccentCharacterInsertedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (_restoreRecoveryBlocked || _servicesDisposed) return;
            var saved = await TryUpdateAuxiliaryUsageAsync(() => _usageService.RecordQuickAccentAsync(e.Character));
            if (_servicesDisposed || _restoreRecoveryBlocked) return;
            StatusText.Text = UiText.Get("Acento inserido") + AuxiliaryUsageStatusSuffix(saved);
        }));
    }

    private void KeyboardHook_OnSuggestionsChanged(object? sender, SnippetSuggestionsEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_restoreRecoveryBlocked || _servicesDisposed) return;
            if (!_settings.ShowSuggestions)
            {
                _suggestionWindow.Hide();
                return;
            }

            _suggestionWindow.UpdateSuggestions(
                e.Snippets,
                e.ScreenPosition,
                e.SelectedIndex);
        }));
    }

    private void SuggestionWindow_OnSnippetChosen(Snippet snippet) =>
        _keyboardHook.ConfirmSuggestion(snippet, SuggestionConfirmation.Click);

    private void KeyboardHook_OnExpansionRequested(
        object? sender,
        SnippetExpansionRequestedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (_restoreRecoveryBlocked || _servicesDisposed) return;
            using var expansionLease = _expansionRequestGate.TryEnter();
            if (expansionLease is null)
            {
                SafeDiagnosticLog.Write("expansion.ignored_busy");
                StatusText.Text = UiText.Get("Outra expansão ainda está em andamento");
                return;
            }
            int inserted;
            try
            {
                SafeDiagnosticLog.Write("expansion.started", new Dictionary<string, object?>
                {
                    ["targetHandleValid"] = e.TargetWindow != IntPtr.Zero,
                    ["typedCharacterCount"] = e.TypedCharacterCount,
                    ["confirmation"] = e.Confirmation.ToString()
                });
                _keyboardHook.ResetBuffer(BufferResetReason.ExpansionStarted);
                _suggestionWindow.Hide();
                IReadOnlyDictionary<string, string> values =
                    new Dictionary<string, string>();
                var fields = _expansionService.GetFillableFields(e.Snippet);

                if (fields.Count > 0)
                {
                    var form = VariableInputWindow.ShowForTarget(fields, e.TargetWindow);
                    if (form.DialogResult != true)
                    {
                        _keyboardHook.ResetBuffer(BufferResetReason.SuggestionCancelled);
                        StatusText.Text = UiText.Get("Expansão cancelada");
                        return;
                    }

                    values = form.Values;
                }

                inserted = await _expansionService.ExpandAsync(
                    e.Snippet,
                    values,
                    e.TargetWindow,
                    e.TypedCharacterCount,
                    CancellationToken.None);
            }
            catch (ExpansionBusyException)
            {
                StatusText.Text = UiText.Get("Outra expansão ainda está em andamento");
                return;
            }
            catch (Exception exception)
            {
                SafeDiagnosticLog.Write("expansion.failed", new Dictionary<string, object?>
                {
                    ["exceptionType"] = exception.GetType().Name,
                    ["reason"] = exception is OperationCanceledException ? "target_changed" : "operation_failed"
                });
                StatusText.Text = UiText.Format($"Falha ao inserir {e.Snippet.Trigger}");
                ShowAppDialog(
                    exception.Message,
                    UiText.Get("Não foi possível inserir o texto"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            finally
            {
                _keyboardHook.ResetBuffer(BufferResetReason.ExpansionFinished);
            }
            await CompleteSuccessfulExpansionAsync(e.Snippet, inserted);
        }));
    }
}
