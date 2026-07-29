using System.Threading;
using System.Windows;
using SlashText.Services;

namespace SlashText;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
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
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
