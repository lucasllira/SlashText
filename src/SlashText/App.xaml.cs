using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SlashText.Services;

namespace SlashText;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private bool _helperMode;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (PortableUpdateService.TryRunHelper(e.Args, out var helperExitCode))
        {
            _helperMode = true;
            Shutdown(helperExitCode);
            return;
        }
        try
        {
            PortableUpdateService.ConfirmAndScheduleCleanup(e.Args);
        }
        catch
        {
            // Sem confirmação, o auxiliar restaura o executável anterior.
            _helperMode = true;
            Shutdown(13);
            return;
        }

        var dataEnvironment = AppDataEnvironment.Detect();
        AppPaths.Initialize(dataEnvironment);
        if (!EnsurePortableLocationIsWritable(dataEnvironment))
        {
            Shutdown(2);
            return;
        }

        DataMigrationResult migration;
        try
        {
            migration = AppPaths.EnsureDataLayout();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Não foi possível preparar os dados do SlashDesk. A origem anterior " +
                "foi preservada e nenhum dado foi ativado parcialmente.\n\n" + exception.Message,
                "SlashDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(3);
            return;
        }

        AppDiagnosticLog.Initialize();
        DispatcherUnhandledException += (_, args) =>
            AppDiagnosticLog.WriteException("exception.wpf-dispatcher", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                AppDiagnosticLog.WriteException("exception.app-domain", exception);
            }
            else
            {
                AppDiagnosticLog.Write(
                    "exception.app-domain",
                    ("exceptionType", args.ExceptionObject?.GetType().FullName ?? "unknown"));
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppDiagnosticLog.WriteException("exception.unobserved-task", args.Exception);
            args.SetObserved();
        };

        if (e.Args.Contains("--portable-smoke", StringComparer.OrdinalIgnoreCase))
        {
            AppDiagnosticLog.Write(
                "application.portable-smoke",
                ("is64BitProcess", Environment.Is64BitProcess),
                ("distributionMode", AppPaths.Mode.ToString()),
                ("dataDirectory", AppPaths.DataDirectory),
                ("migrated", migration.Migrated));
            Shutdown(0);
            return;
        }

        // Mantém o identificador legado para impedir que SlashText e SlashDesk
        // monitorem o teclado ao mesmo tempo durante uma atualização.
        _singleInstance = new Mutex(true, "SlashText.SingleInstance", out var created);
        if (!created)
        {
            MessageBox.Show(
                "O SlashDesk já está em execução na bandeja do Windows.",
                "SlashDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AppDiagnosticLog.Write(
            "storage.selected",
            ("distributionMode", AppPaths.Mode.ToString()),
            ("dataDirectory", AppPaths.DataDirectory),
            ("migrationSource", migration.SourceDirectory),
            ("migrated", migration.Migrated),
            ("competingSourcePreserved", migration.CompetingSourcePreserved),
            ("migrationWarnings", migration.Warnings.Count));
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase))
        {
            window.Hide();
        }

        base.OnStartup(e);
    }

    private static bool EnsurePortableLocationIsWritable(AppDataEnvironment environment)
    {
        if (environment.TryProbePortableWrite(out var error))
        {
            return true;
        }

        var choice = MessageBox.Show(
            "A versão portátil precisa estar em uma pasta gravável para preservar " +
            "SlashDeskData. Deseja escolher outra pasta?\n\n" + error,
            "Pasta portátil sem permissão",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (choice != MessageBoxResult.Yes)
        {
            return false;
        }

        using var picker = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Escolha uma pasta gravável para o SlashDesk portátil",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return false;
        }

        var destination = Path.Combine(picker.SelectedPath, "SlashDesk.exe");
        if (File.Exists(destination))
        {
            MessageBox.Show(
                "A pasta escolhida já contém SlashDesk.exe. Escolha uma pasta vazia " +
                "para evitar substituir outra instalação.",
                "SlashDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        try
        {
            var current = Environment.ProcessPath
                ?? throw new InvalidOperationException("Não foi possível localizar SlashDesk.exe.");
            File.Copy(current, destination, overwrite: false);
            Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true });
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Não foi possível preparar a nova pasta. Nenhum dado foi movido.\n\n" +
                exception.Message,
                "SlashDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_helperMode)
        {
            AppDiagnosticLog.Write("application.exit", ("exitCode", e.ApplicationExitCode));
        }
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
