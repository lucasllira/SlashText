using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SlashText.Services;
using SlashText.Views;

namespace SlashText;

public partial class MainWindow
{
    private async void Settings_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_initialized || _settingsOperationInProgress || _settingsStore.IsProtected) return;
        var previousClose = _settings.CloseToTray;
        var previousSuggestions = _settings.ShowSuggestions;
        var previousStart = _settings.StartWithWindows;
        var requestedStart = StartWithWindowsCheckBox.IsChecked == true;
        var previousUpdateMonitoring = _settings.CheckUpdatesOnStartup;
        _settings.CloseToTray = CloseToTrayCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.ShowSuggestions = ShowSuggestionsCheckBox.IsChecked == true;
        _settings.CheckUpdatesOnStartup = CheckUpdatesCheckBox.IsChecked == true;
        _settingsOperationInProgress = true; SettingsCardsGrid.IsEnabled = false;

        try
        {
            if (!AppPaths.IsCapturePilot && previousStart != _settings.StartWithWindows)
            {
                StartupService.SetEnabled(_settings.StartWithWindows);
            }
            await SavePreferencesAsync();

            if (previousUpdateMonitoring != _settings.CheckUpdatesOnStartup)
            {
                if (_settings.CheckUpdatesOnStartup)
                {
                    StartUpdateMonitor();
                }
                else
                {
                    StopUpdateMonitor();
                }
            }

            if (!_settings.ShowSuggestions)
            {
                _suggestionWindow.Hide();
            }
            SettingsOperationText.Text = UiText.Get("Preferência salva.");
        }
        catch (Exception exception)
        {
            _settings.CloseToTray = previousClose;
            _settings.ShowSuggestions = previousSuggestions;
            _settings.StartWithWindows = previousStart;
            _settings.CheckUpdatesOnStartup = previousUpdateMonitoring;
            StartWithWindowsCheckBox.IsChecked = previousStart;
            CheckUpdatesCheckBox.IsChecked = previousUpdateMonitoring;
            CloseToTrayCheckBox.IsChecked = previousClose;
            ShowSuggestionsCheckBox.IsChecked = previousSuggestions;
            if (!AppPaths.IsCapturePilot && previousStart != requestedStart)
            {
                try { StartupService.SetEnabled(previousStart); }
                catch (Exception rollback) { SettingsOperationText.Text = UiText.Get("Não foi possível reverter a inicialização: ") + rollback.Message; }
            }
            SettingsOperationText.Text = UiText.Get("Não foi possível salvar a preferência.");
            ShowSettingsDialog(
                exception.Message,
                UiText.Get("Não foi possível salvar a configuração"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally { _settingsOperationInProgress = false; SettingsCardsGrid.IsEnabled = true; }
    }

    private async void ImportSnippets_OnClick(object sender, RoutedEventArgs e)
    {
        if (_settingsOperationInProgress || _shortcutOperationInProgress) return;
        if (!EnsureSnippetStorageAvailable())
        {
            return;
        }

        if (!CanDiscardShortcutDraft()) return;
        var source = ImportSourceBox.SelectedItem is ComboBoxItem { Tag: string sourceTag } &&
                     Enum.TryParse<SnippetImportSource>(sourceTag, out var parsed)
            ? parsed
            : SnippetImportSource.SlashDesk;
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = UiText.Format($"Importar atalhos do {ImportSourceName(source)}"),
            Filter = source switch
            {
                SnippetImportSource.SlashDesk => UiText.Get("Atalhos do SlashDesk|*.md"),
                SnippetImportSource.TextBlaze => UiText.Get("Exportação do Text Blaze|*.json"),
                SnippetImportSource.Espanso => UiText.Get("Configuração do Espanso|*.yml;*.yaml"),
                _ => UiText.Get("Todos os arquivos|*.*")
            },
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await RunSettingsMaintenanceAsync(UiText.Get("Importando e protegendo os atalhos…"), async () =>
            {
            _shortcutOperationInProgress = true; ShortcutEditorPanel.IsEnabled = false;
            RefreshShortcutDraftState();
            var token = _settingsCancellation?.Token ?? default;
            var result = await _snippetImportService.ImportAsync(picker.FileName, source, token);
            if (result.Snippets.Count == 0)
            {
                ShowSettingsDialog(
                    UiText.Get("Nenhum atalho compatível foi encontrado no arquivo."),
                    UiText.Get("Importação concluída"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                StatusText.Text = SettingsOperationText.Text = UiText.Get("Nenhum atalho compatível encontrado");
                return;
            }

            var conflicts = result.Snippets.Count(incoming =>
                _snippets.Any(current =>
                    current.Trigger.Equals(incoming.Trigger, StringComparison.OrdinalIgnoreCase)));
            var mode = ShowSettingsDialog(
                UiText.Format($"{result.Snippets.Count} atalho(s) compatível(is) encontrado(s).") +
                (conflicts == 0
                    ? UiText.Get("\n\nDeseja adicionar esses atalhos aos atuais?")
                    : UiText.Format($"\n\n{conflicts} conflito(s) de comando. Escolha substituir os conflitos ou manter os atuais e importar somente os demais.")),
                UiText.Format($"Importar do {ImportSourceName(source)}"),
                conflicts == 0 ? MessageBoxButton.YesNo : MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question, conflicts == 0 ? UiText.Get("Importar atalhos") : UiText.Get("Substituir conflitos"), UiText.Get("Manter atuais"));
            if (mode is MessageBoxResult.Cancel or MessageBoxResult.None ||
                (conflicts == 0 && mode == MessageBoxResult.No))
            {
                StatusText.Text = SettingsOperationText.Text = UiText.Get("Importação cancelada. Os atalhos atuais foram preservados.");
                return;
            }

            await _usageService.RunMaintenanceAsync(() => _backupService.CreateManualSnapshot(token), token);
            var replaceConflicts = mode == MessageBoxResult.Yes;
            var incomingTriggers = result.Snippets
                .Select(item => item.Trigger)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var merged = _snippets
                .Where(current => !replaceConflicts || !incomingTriggers.Contains(current.Trigger))
                .ToList();
            foreach (var incoming in result.Snippets)
            {
                if (replaceConflicts ||
                    !merged.Any(current =>
                        current.Trigger.Equals(incoming.Trigger, StringComparison.OrdinalIgnoreCase)))
                {
                    merged.Add(incoming);
                }
            }

            _shortcutOperationInProgress = true;
            ShortcutEditorPanel.IsEnabled = false;
            token.ThrowIfCancellationRequested();
            await _repository.SaveAsync(merged, token);
            _settingsProgress?.PreventCancellation();
            ReplaceList(merged);
            _shortcutOperationInProgress = false;
            _shortcutDraftBaseline = null; _selected = null;
            if (_snippets.Count > 0) SelectSnippet(_snippets[0]); else BeginNewSnippet();
            _keyboardHook.UpdateSnippets(_snippets);
            RefreshStatistics();
            var warningSummary = result.Warnings.Count == 0
                ? string.Empty
                : UiText.Format($"\n\n{result.Warnings.Count} aviso(s) de compatibilidade. Os primeiros:\n• ") +
                  string.Join("\n• ", result.Warnings.Take(4));
            ShowSettingsDialog(
                UiText.Format($"{result.Snippets.Count} atalho(s) processado(s). O snippets.md foi atualizado e o estado anterior foi salvo em backup.{warningSummary}"),
                UiText.Get("Importação concluída"),
                MessageBoxButton.OK,
                result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            StatusText.Text = UiText.Format($"Importação do {ImportSourceName(source)} concluída");
            SettingsOperationText.Text = StatusText.Text;
            RefreshBackupSummary();
            }, cancellable: true);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = SettingsOperationText.Text = UiText.Get("Operação cancelada. Os dados atuais foram preservados.");
        }
        catch (Exception exception)
        {
            SettingsOperationText.Text = UiText.Get("Não foi possível importar: ") + exception.Message;
            ShowSettingsDialog(
                exception.Message,
                UiText.Get("Não foi possível importar"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _shortcutOperationInProgress = false;
            ShortcutEditorPanel.IsEnabled = true;
            RefreshShortcutDraftState();
        }
    }

    private async void CreateBackup_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string? path = null;
            await RunSettingsMaintenanceAsync(UiText.Get("Criando e verificando o backup…"), () =>
                _usageService.RunMaintenanceAsync(() => path = _backupService.CreateManualSnapshot()));
            RefreshBackupSummary();
            StatusText.Text = UiText.Format($"Backup criado: {Path.GetFileName(path)}");
            SettingsOperationText.Text = StatusText.Text;
        }
        catch (Exception exception)
        {
            SettingsOperationText.Text = UiText.Get("Não foi possível criar o backup: ") + exception.Message;
            ShowSettingsDialog(
                exception.Message,
                UiText.Get("Não foi possível criar o backup"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void AnalyzeAssets_OnClick(object sender, RoutedEventArgs e)
    {
        if (!EnsureSnippetStorageAvailable())
        {
            return;
        }

        try
        {
            await RunSettingsMaintenanceAsync(UiText.Get("Analisando imagens dos atalhos…"), async () =>
            {
            var snippets = _snippets.ToList();
            var report = await Task.Run(() => _assetMaintenanceService.Analyze(snippets));
            if (report.UnsafeReferenceCount > 0)
            {
                ShowSettingsDialog(
                    UiText.Format($"A análise encontrou {report.UnsafeReferenceCount} referência(s) local(is) inválida(s). ") +
                    UiText.Get("Por segurança, nenhuma imagem pode ser removida até que esses atalhos sejam corrigidos."),
                    UiText.Get("Limpeza de imagens bloqueada"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                StatusText.Text = SettingsOperationText.Text = UiText.Get("Limpeza bloqueada por referências inválidas");
                return;
            }

            if (report.Orphans.Count == 0)
            {
                ShowSettingsDialog(
                    UiText.Get("Nenhuma imagem órfã foi encontrada. Os arquivos em uso foram preservados."),
                    UiText.Get("Análise concluída"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                StatusText.Text = SettingsOperationText.Text = UiText.Get("Nenhuma imagem órfã encontrada");
                return;
            }

            var size = report.OrphanSizeBytes < 1024
                ? $"{report.OrphanSizeBytes} B"
                : $"{report.OrphanSizeBytes / 1024d:N1} KB";
            var preview = string.Join(
                "\n• ",
                report.Orphans.Take(10).Select(item => item.RelativePath));
            var remaining = report.Orphans.Count > 10
                ? UiText.Format($"\n• ... e mais {report.Orphans.Count - 10}")
                : string.Empty;
            var confirmation = ShowSettingsDialog(
                UiText.Format($"{report.Orphans.Count} imagem(ns) sem referência ({size}):\n\n• {preview}{remaining}\n\n") +
                UiText.Get("Deseja criar um backup completo e remover somente esses arquivos?"),
                UiText.Get("Revisar imagens órfãs"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning, UiText.Get("Criar backup e remover"));
            if (confirmation != MessageBoxResult.Yes)
            {
                StatusText.Text = SettingsOperationText.Text = UiText.Get("Limpeza de imagens cancelada");
                return;
            }

            AssetCleanupResult? result = null;
            await _usageService.RunMaintenanceAsync(() => result = _assetMaintenanceService.DeleteOrphansAfterBackup(report, _backupService.CreateManualSnapshot));
            RefreshBackupSummary();
            StatusText.Text = UiText.Format($"{result!.DeletedCount} imagem(ns) órfã(s) removida(s)");
            SettingsOperationText.Text = StatusText.Text;
            ShowSettingsDialog(
                UiText.Format($"{result.DeletedCount} imagem(ns) removida(s).\n\n") +
                UiText.Format($"Backup criado antes da limpeza: {Path.GetFileName(result.BackupPath)}"),
                UiText.Get("Limpeza concluída"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            });
        }
        catch (Exception exception)
        {
            SettingsOperationText.Text = UiText.Get("Não foi possível analisar imagens: ") + exception.Message;
            ShowSettingsDialog(
                exception.Message,
                UiText.Get("Não foi possível analisar as imagens"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void RestoreBackup_OnClick(object sender, RoutedEventArgs e)
    {
        if (_settingsOperationInProgress || _shortcutOperationInProgress) return;
        if (!CanDiscardShortcutDraft()) return;
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = UiText.Get("Restaurar backup do SlashDesk"),
            InitialDirectory = AppPaths.BackupsDirectory,
            Filter = UiText.Get("Backup do SlashDesk|SlashDesk-backup-*.zip|Arquivo ZIP|*.zip"),
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true ||
            ShowSettingsDialog(
                UiText.Get("Restaurar este backup substituirá os dados incluídos nele, inclusive Meus emojis quando presentes. Um backup de segurança será criado antes de continuar. Salve suas edições de imagem antes de continuar: se a recuperação ficar pendente, o SlashDesk precisará fechar."),
                UiText.Get("Restaurar backup"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning, UiText.Get("Restaurar backup")) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await RunSettingsMaintenanceAsync(UiText.Get("Verificando e restaurando o backup…"), async () =>
            {
            _shortcutOperationInProgress = true; ShortcutEditorPanel.IsEnabled = false;
            var token = _settingsCancellation?.Token ?? default;
            await _usageService.RunMaintenanceAsync(() => _backupService.RestoreSnapshot(picker.FileName, token), token);
            _settingsProgress?.PreventCancellation();
            CaptureStampCatalog.InvalidateCurrent();
            _settings = await _settingsStore.LoadAsync();
            ThemeService.Apply(_settings.Theme);
            SyncSettingsControls();
            var restoredSnippets = await _repository.LoadAsync();
            ReplaceList(restoredSnippets);
            _shortcutDraftBaseline = null; _selected = null;
            _snippetStorageAvailable = true;
            _shortcutOperationInProgress = false;
            if (_snippets.Count > 0) SelectSnippet(_snippets[0]); else BeginNewSnippet();
            _keyboardHook.UpdateSnippets(_snippets);
            await _usageService.LoadAsync();
            await _captureService.LoadAsync();
            RefreshStorageProtection();
            // Preserve edited pixels, but a restore must not retain authority to overwrite the old path.
            _captureWorkbenchPath = null; _captureWorkbenchRecord = null;
            RefreshCaptureHistory();
            RefreshStatistics();
            RefreshBackupSummary();
            ShowSettingsDialog(
                UiText.Get("Backup restaurado. As preferências completas serão aplicadas na próxima abertura do SlashDesk."),
                UiText.Get("Restauração concluída"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SettingsOperationText.Text = UiText.Get("Backup restaurado. Reabra o app para aplicar todas as preferências.");
            }, cancellable: true);
        }
        catch (RestoreRecoveryRequiredException)
        {
            _restoreRecoveryBlocked = true;
            _settingsOperationInProgress = true;
            _shortcutOperationInProgress = true;
            ((FrameworkElement)Content).IsEnabled = false;
            _keyboardHook.Dispose(); _quickAccentService.Enabled = false;
            StopUpdateMonitor(); _captureShortcuts.Dispose();
            _suggestionWindow.Hide(); _quickAccentWindow.Hide();
            var dialog = RestoreRecoveryDialog.Create(blocked: true);
            dialog.Owner = this; dialog.EnableBackdrop(); dialog.ShowDialog();
            try
            {
                if (dialog.Choice == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(AppPaths.BackupsDirectory) { UseShellExecute = true });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                // Close safely even when Explorer cannot open the preserved material.
            }
            finally { _exitRequested = true; Close(); }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = SettingsOperationText.Text = UiText.Get("Operação cancelada. Os dados atuais foram preservados.");
        }
        catch (Exception exception)
        {
            SettingsOperationText.Text = UiText.Get("Não foi possível restaurar: ") + exception.Message;
            ShowSettingsDialog(
                exception.Message,
                UiText.Get("Não foi possível restaurar"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _shortcutOperationInProgress = false; ShortcutEditorPanel.IsEnabled = true;
            RefreshShortcutDraftState();
        }
    }

    private void OpenBackupFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.BackupsDirectory);
        Process.Start(new ProcessStartInfo(AppPaths.BackupsDirectory) { UseShellExecute = true });
    }

    private void RefreshBackupSummary()
    {
        if (BackupSummaryText is null)
        {
            return;
        }

        var snapshots = _backupService.ListSnapshots();
        BackupSummaryText.Text = snapshots.Count == 0
            ? UiText.Get("Nenhum backup criado ainda.")
            : UiText.Format($"{snapshots.Count} backup(s) · último em {snapshots[0].LastWriteTime:g}");
    }

    private static string ImportSourceName(SnippetImportSource source) => source switch
    {
        SnippetImportSource.SlashDesk => "SlashDesk",
        SnippetImportSource.TextBlaze => "Text Blaze",
        SnippetImportSource.Espanso => "Espanso",
        _ => source.ToString()
    };

    private async void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _settingsOperationInProgress || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string theme })
        {
            return;
        }

        var previous = _settings.Theme;
        _settings.Theme = theme;
        _settingsOperationInProgress = true; SettingsCardsGrid.IsEnabled = false;
        try
        {
        ThemeService.Apply(theme);
        if (_trayIcon?.ContextMenuStrip is { } trayMenu)
        {
            ApplyTrayTheme(trayMenu);
        }
        await SavePreferencesAsync();
        ShowView(SettingsView, SettingsTabButton);
        SettingsOperationText.Text = UiText.Get("Tema salvo.");
        }
        catch (Exception exception)
        {
            _settings.Theme = previous; ThemeService.Apply(previous); SyncSettingsControls();
            SettingsOperationText.Text = UiText.Get("Não foi possível salvar o tema: ") + exception.Message;
        }
        finally { _settingsOperationInProgress = false; SettingsCardsGrid.IsEnabled = true; }
    }
}
