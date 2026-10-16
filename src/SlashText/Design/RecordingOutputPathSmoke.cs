using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenRecorderLib;
using SlashText.Models;
using SlashText.Services;

namespace SlashText.Design;

// Explicit helper mode: real native video of its own generated window only.
// No microphone, loopback, personal screen, clipboard or network share access.
internal static class RecordingOutputPathSmoke
{
    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "fixture"), output, isCapturePilot: true));
        var text = new TextBlock { FontSize = 22, Foreground = Brushes.White, Margin = new Thickness(20) };
        var window = new Window { Content = text, Title = "SlashDesk #154 · janela fictícia",
            Width = 480, Height = 300, Left = 30, Top = 30, Background = Brushes.DarkSlateBlue, ShowInTaskbar = false };
        window.Show();
        var ticks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => text.Text = "Teste fictício #154 · sem áudio\nQuadro " + ++ticks;
        timer.Start();
        var results = new List<object>();
        try
        {
            await Task.Delay(300);
            var target = new RecordingTarget(RecordingTargetKind.Window,
                new System.Drawing.Rectangle(30, 30, 480, 300), new WindowInteropHelper(window).Handle);
            foreach (var unicode in new[] { false, true })
            foreach (var length in new[] { 260, 261, 350, 259 })
            {
                var folder = Path.Combine(output, (unicode ? "áéç😀-" : "ascii-") + length);
                var nameLength = length - 44 - folder.Length - 1 - 4;
                if (nameLength < 4) throw new InvalidOperationException("Native fixture needs a shorter output root");
                var capture = new CaptureSettings { OutputDirectoryTemplate = folder, FileNameTemplate = new string('v', nameLength) };
                var settings = new RecordingSettings { IncludeCursor = false,
                    Audio = new() { CaptureComputer = false, CaptureMicrophone = false } };
                var factory = new Factory();
                using var service = new ScreenRecordingService(factory, TimeSpan.FromSeconds(8));
                Task<string>? completion = null;
                try
                {
                    if (length >= 260)
                    {
                        var rejected = false;
                        try { completion = service.StartAsync(target, capture, settings); }
                        catch (InvalidOperationException e) { rejected = e.Message == UiText.Get(
                            "O caminho da gravação MP4 é muito longo, incluindo o arquivo temporário. Escolha uma pasta mais próxima da raiz do disco ou um nome de arquivo mais curto e tente novamente."); }
                        Require(rejected && factory.Created == 0 && !service.IsRecording && !Directory.Exists(folder),
                            "unsupported native path rejected before backend and directory creation");
                        results.Add(new { length, unicode, status = "REFUSED_BEFORE_NATIVE", backendCreated = false });
                        continue;
                    }
                    completion = service.StartAsync(target, capture, settings);
                    var wait = Stopwatch.StartNew();
                    while (service.State != ScreenRecordingState.Recording)
                    { if (completion.IsCompleted) await completion; if (wait.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Native path fixture startup"); await Task.Delay(30); }
                    Require(service.RecoveryFilePath.Length == length && factory.Created == 1, "exact native temporary length");
                    await Task.Delay(1200); service.Stop();
                    var path = await completion.WaitAsync(TimeSpan.FromSeconds(20));
                    var tracks = RecordingAudioSmoke.Tracks(File.ReadAllBytes(path));
                    Require(tracks.Count(t => t.Kind == "vide") == 1 && !tracks.Any(t => t.Kind == "soun") &&
                        tracks.Single(t => t.Kind == "vide").Seconds > .5, "real video track, no audio and positive duration");
                    using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    Require(file.Length > 32 && !service.IsRecording, "native file released");
                    results.Add(new { length, unicode, status = "PASS", finalLength = path.Length,
                        bytes = file.Length, tracks, fileReleased = true });
                }
                finally
                {
                    service.Stop();
                    if (completion is not null) try { await completion.WaitAsync(TimeSpan.FromSeconds(20)); } catch { }
                }
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { status = "PASS",
                source = "own generated opaque window", computerAudio = false, microphone = false, results },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { timer.Stop(); window.Close(); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Factory : IScreenRecorderBackendFactory
    {
        internal int Created;
        public IScreenRecorderBackend Create(RecorderOptions options)
        {
            Require(options.AudioOptions.AudioSources.Count == 0 && options.AudioOptions.IsAudioEnabled == false, "native fixture cannot capture audio");
            Created++;
            return new ScreenRecorderBackendFactory().Create(options);
        }
    }
}
