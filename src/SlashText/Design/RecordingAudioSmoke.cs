using System.Buffers.Binary;
using System.IO;
using System.Media;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

internal static class RecordingAudioSmoke
{
    internal static async Task RunAsync(string output, bool native)
    {
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "fixture-data"), output, isCapturePilot: true));
        foreach (var theme in new[] { "Light", "Dark" })
        {
            ThemeService.Apply(theme);
            using var service = new ScreenRecordingService();
            var settings = new RecordingSettings { Audio = new() { CaptureComputer = false } };
            var toolbar = new RecordingControlWindow(service, "MP4", settings);
            toolbar.Show();
            try
            {
                await SettleAsync();
                Require(!toolbar.WaitForStartAsync().IsCompleted && service.State == ScreenRecordingState.Idle,
                    "Preparation never starts recording implicitly");
                var buttons = Descendants((FrameworkElement)toolbar.Content).OfType<Button>().ToArray();
                Require(buttons.Count(b => AutomationProperties.GetName(b).Contains("Microfone")) == 1 &&
                    buttons.Count(b => AutomationProperties.GetName(b).Contains("Áudio do PC")) == 1,
                    "Two independent accessible audio icons");
                var finish = buttons.Single(b => Equals(b.Content, "Iniciar"));
                var root = (FrameworkElement)toolbar.Content;
                Require(finish.TransformToAncestor(root).Transform(new Point()).X > root.ActualWidth / 2,
                    "Start remains on the right");
                Save(root, Path.Combine(output, theme + "-preparation.png"));
                var menu = buttons.Single(b => AutomationProperties.GetName(b) == "Escolher dispositivos e volumes");
                menu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await SettleAsync();
                var panel = (RecordingAudioPanel)typeof(RecordingControlWindow).GetField("_audioPanel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(toolbar)!;
                Save((FrameworkElement)VisualTreeHelper.GetParent(panel), Path.Combine(output, theme + "-audio-panel.png"));
                await toolbar.RequestStartAsync();
                Require(await toolbar.WaitForStartAsync() && !settings.Audio.CaptureComputer && !settings.Audio.CaptureMicrophone,
                    "Explicit start preserves video without audio");
                toolbar.BeginRecording();
                await SettleAsync();
                Save(root, Path.Combine(output, theme + "-starting.png"));
            }
            finally { toolbar.Close(); }

            var capture = new CaptureSettings { Recording = new() { Audio = new() {
                CaptureComputer = false, CaptureMicrophone = true, InputDeviceId = "saved-input", InputVolume = .6f } } };
            var dialog = new CaptureRuleDialog(capture);
            Require(dialog.TryBuildResult(out var result, out _) && result.Recording.Audio.CaptureMicrophone &&
                !result.Recording.Audio.CaptureComputer && result.Recording.Audio.InputDeviceId == "saved-input" &&
                result.Recording.Audio.InputVolume == .6f && capture.Recording.Audio.InputDeviceId == "saved-input",
                "Unified settings preserve all audio preferences without mutating the original");
            dialog.Close();
        }
        if (native) await NativeAsync(output);
        File.WriteAllText(Path.Combine(output, "result.txt"), "Recording audio WPF: OK\nNative: " + (native ? "executed (see native.json)" : "not requested"));
    }

    private static async Task NativeAsync(string output)
    {
        var devices = await Task.Run(RecordingAudioDevices.Load);
        var label = new TextBlock { Text = "SlashDesk · teste de vídeo e áudio", FontSize = 22,
            Foreground = Brushes.White, Margin = new Thickness(24) };
        var fixture = new Window { Title = "SlashDesk recording fixture", Content = label, Background = Brushes.DarkSlateGray,
            Width = 480, Height = 300, Left = 30, Top = 30, ShowInTaskbar = false };
        fixture.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        var frame = 0;
        timer.Tick += (_, _) => label.Text = "SlashDesk · teste de vídeo e áudio\nQuadro " + ++frame;
        timer.Start();
        var metrics = new List<object>();
        try
        {
            using var wave = Tone();
            using var sound = new SoundPlayer(wave);
            var target = new RecordingTarget(RecordingTargetKind.Window, new System.Drawing.Rectangle(30, 30, 480, 300),
                new WindowInteropHelper(fixture).Handle);
            foreach (var (name, pc, mic) in new[] { ("silent", false, false), ("computer", true, false),
                         ("microphone", false, true), ("both", true, true) })
            {
                var settings = new RecordingSettings { Audio = new() { CaptureComputer = pc, CaptureMicrophone = mic } };
                var unavailable = devices.Validate(settings.Audio);
                if (unavailable is not null) { metrics.Add(new { name, Skipped = unavailable }); continue; }
                using var service = new ScreenRecordingService();
                var completion = service.StartAsync(target,
                    new CaptureSettings { OutputDirectoryTemplate = output, FileNameTemplate = "native-" + name }, settings);
                var toolbar = new RecordingControlWindow(service, "MP4", settings);
                toolbar.BeginRecording(); toolbar.Show();
                try
                {
                    await WaitUntilAsync(() => service.State == ScreenRecordingState.Recording || completion.IsCompleted);
                    if (completion.IsCompleted) await completion;
                    if (pc) sound.PlayLooping();
                    await Task.Delay(1500);
                    Save((FrameworkElement)toolbar.Content, Path.Combine(output, name + "-recording.png"));
                    Require(await service.SetAudioMutedAsync(true, true), "Native mute applies: " + name);
                    await Task.Delay(1000);
                    service.Pause();
                    await WaitUntilAsync(() => service.State == ScreenRecordingState.Paused || completion.IsCompleted);
                    var elapsed = service.Elapsed;
                    await Task.Delay(1000);
                    Require((service.Elapsed - elapsed).TotalMilliseconds < 50, "Native pause freezes timer: " + name);
                    Require(!await service.SetAudioMutedAsync(!mic, !pc), "Paused recording rejects native audio changes: " + name);
                    Save((FrameworkElement)toolbar.Content, Path.Combine(output, name + "-paused.png"));
                    service.Resume();
                    await WaitUntilAsync(() => service.State == ScreenRecordingState.Recording || completion.IsCompleted);
                    if (pc || mic)
                        Require(await service.SetAudioMutedAsync(!mic, !pc), "Native restore applies after resume: " + name);
                    await Task.Delay(1500);
                    service.Stop();
                    var path = await completion.WaitAsync(TimeSpan.FromSeconds(25));
                    var tracks = Tracks(File.ReadAllBytes(path));
                    Require(tracks.Any(t => t.Kind == "vide"), "Native MP4 has video: " + name);
                    Require(tracks.Any(t => t.Kind == "soun") == (pc || mic), "Native MP4 audio track matches sources: " + name);
                    if (pc || mic)
                        Require(Math.Abs(tracks.Single(t => t.Kind == "vide").Seconds - tracks.Single(t => t.Kind == "soun").Seconds) < .6,
                            "Audio and video remain aligned after mute/pause: " + name);
                    Require(Math.Abs(tracks.Single(t => t.Kind == "vide").Seconds - service.Elapsed.TotalSeconds) < .8,
                        "MP4 timeline excludes pause: " + name);
                    metrics.Add(new { name, File = Path.GetFileName(path), Elapsed = service.Elapsed.TotalSeconds, Tracks = tracks,
                        Bytes = new FileInfo(path).Length });
                }
                finally
                {
                    sound.Stop(); toolbar.Close(); service.Stop();
                    try { await completion.WaitAsync(TimeSpan.FromSeconds(15)); } catch { }
                }
            }
        }
        finally { timer.Stop(); fixture.Close(); File.WriteAllText(Path.Combine(output, "native.json"),
            JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true })); }
    }

    private sealed record Track(string Kind, double Seconds);
    private static Track[] Tracks(byte[] bytes)
    {
        var tracks = new List<Track>();
        foreach (var moov in Boxes(bytes, 0, bytes.Length).Where(b => b.Kind == "moov"))
        foreach (var trak in Boxes(bytes, moov.Offset, moov.End).Where(b => b.Kind == "trak"))
        foreach (var mdia in Boxes(bytes, trak.Offset, trak.End).Where(b => b.Kind == "mdia"))
        {
            var boxes = Boxes(bytes, mdia.Offset, mdia.End).ToArray();
            var handler = boxes.Single(b => b.Kind == "hdlr");
            var header = boxes.Single(b => b.Kind == "mdhd");
            var version = bytes[header.Offset];
            var scale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(header.Offset + (version == 1 ? 20 : 12), 4));
            var duration = version == 1 ? BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(header.Offset + 24, 8)) :
                BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(header.Offset + 16, 4));
            tracks.Add(new(Encoding.ASCII.GetString(bytes, handler.Offset + 8, 4), duration / (double)scale));
        }
        return tracks.ToArray();
    }
    private static IEnumerable<(string Kind, int Offset, int End)> Boxes(byte[] bytes, int start, int end)
    {
        for (var offset = start; offset + 8 <= end;)
        {
            long length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            var header = 8;
            if (length == 1) { length = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(offset + 8, 8)); header = 16; }
            if (length == 0) length = end - offset;
            if (length < header || length > end - offset) throw new InvalidDataException("Invalid MP4 box");
            yield return (Encoding.ASCII.GetString(bytes, offset + 4, 4), offset + header, offset + (int)length);
            offset += (int)length;
        }
    }
    private static MemoryStream Tone()
    {
        const int rate = 44100;
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + rate * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(rate * 2);
            for (var i = 0; i < rate; i++) writer.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 2000));
        }
        stream.Position = 0; return stream;
    }
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(12);
        while (!condition()) { if (DateTime.UtcNow > limit) throw new TimeoutException("Native recording state timeout"); await Task.Delay(50); }
    }
    private static async Task SettleAsync() => await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Save(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
