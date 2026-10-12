using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenRecorderLib;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

// Inert backend only: no desktop, microphone, loopback or clipboard access.
internal static class RecordingRecoverySmoke
{
    internal static async Task RunAsync(string output)
    {
        foreach (var language in new[] { "pt-BR", "en", "es" })
        foreach (var theme in new[] { "Light", "Dark" })
        {
            UiText.Apply(language); ThemeService.Apply(theme);
            var backend = new Backend();
            using var service = new ScreenRecordingService(backend, TimeSpan.FromMilliseconds(150));
            var settings = new RecordingSettings { Audio = new() { CaptureComputer = false, CaptureMicrophone = false } };
            var toolbar = new RecordingControlWindow(service, "MP4", settings);
            toolbar.Show(); toolbar.BeginRecording();
            var task = service.StartAsync(new RecordingTarget(RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 100, 100), new IntPtr(1)),
                new CaptureSettings { OutputDirectoryTemplate = output, FileNameTemplate = "safety-fixture" }, settings);
            service.Stop();
            try
            {
                var watch = Stopwatch.StartNew();
                while (!toolbar.RecoveryVisible)
                {
                    if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Recovery toolbar");
                    await Task.Delay(30);
                }
                Require(service.IsStopUnconfirmed && !task.IsCompleted, "live uncertain session");
                toolbar.Close();
                Require(toolbar.IsVisible, "system close cannot hide uncertainty");
                var root = (FrameworkElement)toolbar.Content;
                root.UpdateLayout();
                var exit = Descendants(root).OfType<Button>().Single(b => Equals(b.Content, UiText.Get("Encerrar SlashDesk")));
                Require(exit.IsVisible && exit.IsEnabled && exit.ActualHeight >= 32, "accessible recovery action");
                var requests = 0;
                toolbar.EmergencyExitRequested += (_, _) => requests++;
                exit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(requests == 1 && toolbar.IsVisible, "exit requires application confirmation");
                var confirm = new SettingsDialogWindow(UiText.Get("Encerramento de emergência"),
                    UiText.Get("Encerramento de emergência"), MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    yesLabel: UiText.Get("Encerrar SlashDesk"));
                Require(Equals(confirm.DefaultButton.Content, UiText.Get("Cancelar")) &&
                    confirm.Actions.Single(b => Equals(b.Content, UiText.Get("Encerrar SlashDesk"))).IsDefault == false,
                    "emergency confirmation defaults to cancel");
                confirm.Close();
                var bounds = exit.TransformToAncestor(root).TransformBounds(new Rect(exit.RenderSize));
                Require(bounds.Bottom <= root.ActualHeight && bounds.Right <= root.ActualWidth, "recovery action fits");
                var image = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                image.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var file = File.Create(Path.Combine(output, $"safety-{language}-{theme}.png")); encoder.Save(file);
            }
            finally
            {
                backend.Complete();
                try { await task.WaitAsync(TimeSpan.FromSeconds(3)); } catch (TimeoutException) { Require(task.IsCompleted, "late cleanup finished"); }
                Require(!service.IsRecording && backend.DisposeCalls == 1, "confirmed cleanup closes control");
                toolbar.Close(); Require(!toolbar.IsVisible, "control closes after confirmed release");
            }
        }
        UiText.Apply("pt-BR"); ThemeService.Apply("Light");
        File.WriteAllText(Path.Combine(output, "recovery-result.txt"), "PASS: recovery state, close guard, exit intent, bounds and late cleanup in 3 languages and 2 themes. No real capture.");
    }

    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var item in Descendants(VisualTreeHelper.GetChild(root, i))) yield return item;
    }
    private sealed class Backend : IScreenRecorderBackendFactory, IScreenRecorderBackend
    {
        public event EventHandler<RecordingCompleteEventArgs>? Completed;
        public event EventHandler<RecordingFailedEventArgs>? Failed { add { } remove { } }
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        private string _path = "";
        internal int DisposeCalls;
        public IScreenRecorderBackend Create(RecorderOptions options) => this;
        public void Record(string path) { _path = path; File.WriteAllText(path, "inert UI fixture"); StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording)); }
        internal void Complete() => Completed?.Invoke(this, new RecordingCompleteEventArgs(_path, new List<FrameData>()));
        public void Stop() { }
        public void Pause() { }
        public void Resume() { }
        public bool SetAudioVolumes(float inputVolume, float outputVolume) => false;
        public void Dispose() => DisposeCalls++;
    }
}
