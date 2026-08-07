using System.Threading;
using System.Windows;
using SlashText.Services;

namespace SlashText;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
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
            AppPaths.EnsureDataLayout();
            AppDiagnosticLog.Write(
                "application.portable-smoke",
                ("is64BitProcess", Environment.Is64BitProcess),
                ("dataDirectory", AppPaths.DataDirectory));
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

        AppPaths.EnsureDataLayout();
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

    protected override void OnExit(ExitEventArgs e)
    {
        AppDiagnosticLog.Write("application.exit", ("exitCode", e.ApplicationExitCode));
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
