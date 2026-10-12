using System.Windows;
using SlashText.Services;

namespace SlashText;

public partial class MainWindow
{
    private bool _recordingExitPromptOpen;

    private void RequestRecordingEmergencyExit()
    {
        var recorder = _recordingService;
        if (_recordingExitPromptOpen || recorder?.IsStopUnconfirmed != true) return;
        _recordingExitPromptOpen = true;
        try
        {
            ShowFromTray();
            var answer = ShowAppDialog(
                UiText.Get("A parada de tela e áudio ainda não foi confirmada. Encerrar o SlashDesk termina o processo que contém o gravador. Alterações não salvas serão perdidas. O vídeo temporário será mantido, mas pode estar incompleto. Deseja encerrar agora?") +
                "\n\n" + recorder.RecoveryFilePath,
                UiText.Get("Encerramento de emergência"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No,
                yesLabel: UiText.Get("Encerrar SlashDesk"));
            // A late callback may have released the backend while the dialog was open.
            if (answer == MessageBoxResult.Yes && ReferenceEquals(_recordingService, recorder) && recorder.IsStopUnconfirmed)
                RecordingEmergencyExit.TerminateCurrentProcess();
        }
        catch (Exception exception)
        {
            AppDiagnosticLog.WriteException("recording.emergency-exit-failed", exception);
            ShowAppDialog(UiText.Get("Não foi possível encerrar o processo. A parada segue sem confirmação. Use o Gerenciador de Tarefas para encerrar o SlashDesk."),
                UiText.Get("Encerramento de emergência"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _recordingExitPromptOpen = false; }
    }
}
