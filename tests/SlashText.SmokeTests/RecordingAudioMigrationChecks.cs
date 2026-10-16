using ScreenRecorderLib;
using SlashText.Models;
using SlashText.Services;

internal static class RecordingAudioMigrationChecks
{
    internal static void Run(string output)
    {
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Recording audio migration: " + name);
            checks.Add(name + ": PASS");
        }
        foreach (var computer in new[] { false, true })
        foreach (var microphone in new[] { false, true })
        foreach (var defaultDevices in new[] { false, true })
        {
            var settings = new RecordingAudioSettings
            {
                CaptureComputer = computer, CaptureMicrophone = microphone,
                InputDeviceId = defaultDevices ? "" : "input-fixture",
                OutputDeviceId = defaultDevices ? "" : "output-fixture",
                InputVolume = .4f, OutputVolume = .7f
            };
            var context = $"PC={computer}/mic={microphone}/default={defaultDevices}";
            var options = ScreenRecordingService.BuildOptions(
                new RecordingTarget(RecordingTargetKind.Window, new System.Drawing.Rectangle(0, 0, 640, 480), new IntPtr(1)),
                new RecordingSettings { Audio = settings }, Path.Combine(output, "unused-native.log")).AudioOptions;
            var sources = options.AudioSources.ToArray();
            Check(options.IsAudioEnabled == (computer || microphone) && sources.Length == (computer ? 1 : 0) + (microphone ? 1 : 0), context + " exact source count and audio track");
            Check(sources.OfType<CaptureAudioSource>().Count() == (microphone ? 1 : 0) &&
                sources.OfType<LoopbackAudioSource>().Count() == (computer ? 1 : 0), context + " no unselected or process sources");
            Check(sources.All(s => s.IsAudioCaptureEnabled) && sources.Select(s => s.ID).Distinct().Count() == sources.Length,
                context + " enabled sources have distinct identities");
            foreach (var source in sources)
            {
                Check(source is CaptureAudioSource capture
                    ? capture.DeviceName == settings.InputDeviceId && capture.Volume == .4f
                    : ((LoopbackAudioSource)source).DeviceName == settings.OutputDeviceId && source.Volume == .7f,
                    context + " selected endpoint and initial volume " + source.GetType().Name);
            }
            foreach (var (input, outputVolume) in new[] { (0f, 0f), (.4f, 0f), (0f, .7f), (.4f, .7f) })
            {
                var update = RecordingAudioConfiguration.VolumeUpdate(sources, input, outputVolume);
                Check(update.AudioSources.Select(s => s.ID).SequenceEqual(sources.Select(s => s.ID)), context + " mute/unmute retains identities");
                Check(update.AudioSources.All(s => s.IsAudioCaptureEnabled &&
                    s.Volume == (s is CaptureAudioSource ? input : outputVolume)), context + " independent per-source mute/volume");
                Check(update.AudioSources.Zip(sources).All(pair => !ReferenceEquals(pair.First, pair.Second) &&
                    pair.Second.Volume == (pair.Second is CaptureAudioSource ? .4f : .7f)), context + " failed update cannot mutate configured source");
            }
            Check(settings.InputVolume == .4f && settings.OutputVolume == .7f &&
                settings.CaptureMicrophone == microphone && settings.CaptureComputer == computer, context + " preferences unchanged");
        }
        var mono = new CaptureAudioSource("mono-fixture") { ForceMono = true, InputDeviceMasterChannel = 1, IsAudioCaptureEnabled = false };
        var monoUpdate = (CaptureAudioSource)RecordingAudioConfiguration.VolumeUpdate([mono], 0, 0).AudioSources.Single();
        Check(monoUpdate.ID == mono.ID && monoUpdate.ForceMono && monoUpdate.InputDeviceMasterChannel == 1 && !monoUpdate.IsAudioCaptureEnabled,
            "volume changes preserve source flags and mono channel");
        var devices = RecordingAudioDevices.Load();
        foreach (var list in new[] { devices.Inputs, devices.Outputs })
            Check(list.Error is null
                ? list.Devices.All(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Name)) &&
                    list.Devices.Select(d => d.Id).Distinct().Count() == list.Devices.Count
                : list.Devices.Count == 0, "Windows endpoint enumeration returns a valid list or a controlled error");
        Console.WriteLine($"Windows endpoints: inputCount={devices.Inputs.Devices.Count}, outputCount={devices.Outputs.Devices.Count}, " +
            $"inputError={devices.Inputs.Error is not null}, outputError={devices.Outputs.Error is not null}; no audio captured or device names logged.");
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "recording-audio-701.txt"), checks);
        Console.WriteLine($"Recording audio 7.0.1: {checks.Count} checks passed; read-only endpoint enumeration, no native recording.");
    }
}
