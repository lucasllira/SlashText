using ScreenRecorderLib;
using SlashText.Models;

namespace SlashText.Services;

internal static class RecordingAudioConfiguration
{
    internal static AudioOptions Create(RecordingAudioSettings settings)
    {
        var options = new AudioOptions { IsAudioEnabled = settings.CaptureComputer || settings.CaptureMicrophone };
        // Empty endpoint IDs retain the Windows default selection; do not resolve devices for disabled sources.
        if (settings.CaptureComputer)
            options.AudioSources.Add(new LoopbackAudioSource(settings.OutputDeviceId) { Volume = settings.OutputVolume });
        if (settings.CaptureMicrophone)
            options.AudioSources.Add(new CaptureAudioSource(settings.InputDeviceId) { Volume = settings.InputVolume });
        return options;
    }

    internal static DynamicAudioOptions VolumeUpdate(IReadOnlyList<AudioSourceBase> sources, float inputVolume, float outputVolume)
    {
        var update = new DynamicAudioOptions();
        foreach (var source in sources)
        {
            // The library matches live sources by ID. Copy constructors retain it; new sources do not.
            // Leave the original configuration intact if the native update fails.
            update.AudioSources.Add(source switch
            {
                CaptureAudioSource capture => new CaptureAudioSource(capture)
                {
                    Volume = inputVolume,
                    ForceMono = capture.ForceMono,
                    InputDeviceMasterChannel = capture.InputDeviceMasterChannel
                },
                LoopbackAudioSource loopback => new LoopbackAudioSource(loopback) { Volume = outputVolume },
                _ => throw new NotSupportedException("Unexpected recording audio source.")
            });
        }
        return update;
    }
}
