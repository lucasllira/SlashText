using SlashText.Models;
using SlashText.Services;
using System.Reflection;
using ScreenRecorderLib;

internal static partial class SmokeSuite
{
    private static async Task RunCaptureSettingsChecksAsync(string root)
    {
        var captureDefaults = new CaptureSettings();
        Require(
            captureDefaults.Recording.VideoFps == 30 &&
            captureDefaults.Recording.GifFps == 10 &&
            captureDefaults.Recording.GifQuality == 128 &&
            captureDefaults.HistoryRetentionDays == 90 &&
            captureDefaults.ScrollingShortcut == "Ctrl+Shift+WheelDown" &&
            !captureDefaults.OpenEditorForMonitorAndWindow,
            "padrões seguros de gravação, histórico e captura direta");
        Require(
            !captureDefaults.HideSlashDeskDuringCapture &&
            !captureDefaults.ShouldHideSlashDesk(windowIsVisible: true) &&
            !captureDefaults.ShouldHideSlashDesk(windowIsVisible: false),
            "padrão permite capturar a própria janela do SlashDesk");
        var hiddenSlashDeskSettings = new AppSettings();
        hiddenSlashDeskSettings.Capture.HideSlashDeskDuringCapture = true;
        var hiddenSlashDeskJson =
            System.Text.Json.JsonSerializer.Serialize(hiddenSlashDeskSettings);
        var reloadedHiddenSlashDesk =
            System.Text.Json.JsonSerializer.Deserialize<AppSettings>(hiddenSlashDeskJson);
        Require(
            reloadedHiddenSlashDesk is not null &&
            reloadedHiddenSlashDesk.Capture.HideSlashDeskDuringCapture &&
            reloadedHiddenSlashDesk.Capture.ShouldHideSlashDesk(windowIsVisible: true) &&
            !reloadedHiddenSlashDesk.Capture.ShouldHideSlashDesk(windowIsVisible: false),
            "preferência para ocultar o SlashDesk persiste no JSON");
        var editorCaptureSettings = new AppSettings();
        editorCaptureSettings.Capture.OpenEditorForMonitorAndWindow = true;
        var editorCaptureJson =
            System.Text.Json.JsonSerializer.Serialize(editorCaptureSettings);
        var reloadedEditorCapture =
            System.Text.Json.JsonSerializer.Deserialize<AppSettings>(editorCaptureJson);
        Require(
            reloadedEditorCapture is not null &&
            reloadedEditorCapture.Capture.OpenEditorForMonitorAndWindow,
            "preferência existente pelo modo editor persiste no JSON");
        using var scrollingDocument = new System.Drawing.Bitmap(96, 280);
        for (var y = 0; y < scrollingDocument.Height; y++)
        {
            for (var x = 0; x < scrollingDocument.Width; x++)
            {
                scrollingDocument.SetPixel(
                    x,
                    y,
                    System.Drawing.Color.FromArgb(
                        (x * 17 + y * 31) % 256,
                        (x * 29 + y * 13) % 256,
                        (x * 7 + y * 43) % 256));
            }
        }
        using var firstScrollingFrame = scrollingDocument.Clone(
            new System.Drawing.Rectangle(0, 0, 96, 120),
            scrollingDocument.PixelFormat);
        using var repeatedScrollingFrame = new System.Drawing.Bitmap(firstScrollingFrame);
        using var secondScrollingFrame = scrollingDocument.Clone(
            new System.Drawing.Rectangle(0, 75, 96, 120),
            scrollingDocument.PixelFormat);
        Require(
            CaptureService.AreFramesVisuallyEquivalent(
                firstScrollingFrame,
                repeatedScrollingFrame),
            "rolagem detecta o fim quando o quadro não muda");
        Require(
            !CaptureService.AreFramesVisuallyEquivalent(
                firstScrollingFrame,
                secondScrollingFrame),
            "rolagem continua quando há novo conteúdo");
        Require(
            CaptureService.EstimateVerticalScrollDelta(
                firstScrollingFrame,
                secondScrollingFrame,
                fallbackDelta: 100) == 75,
            "rolagem mede o deslocamento real e evita duplicação");
        using var browserFrameBefore = new System.Drawing.Bitmap(240, 160);
        using var browserFrameAfter = new System.Drawing.Bitmap(240, 160);
        using (var beforeGraphics = System.Drawing.Graphics.FromImage(browserFrameBefore))
        using (var afterGraphics = System.Drawing.Graphics.FromImage(browserFrameAfter))
        {
            beforeGraphics.Clear(System.Drawing.Color.FromArgb(13, 17, 23));
            afterGraphics.Clear(System.Drawing.Color.FromArgb(13, 17, 23));
            using var chromeBrush = new System.Drawing.SolidBrush(
                System.Drawing.Color.FromArgb(32, 36, 42));
            beforeGraphics.FillRectangle(chromeBrush, 0, 0, 240, 28);
            afterGraphics.FillRectangle(chromeBrush, 0, 0, 240, 28);
            using var chromeLine = new System.Drawing.Pen(
                System.Drawing.Color.FromArgb(110, 118, 129));
            beforeGraphics.DrawLine(chromeLine, 8, 14, 232, 14);
            afterGraphics.DrawLine(chromeLine, 8, 14, 232, 14);
        }
        for (var y = 28; y < 160; y++)
        {
            for (var x = 40; x < 200; x++)
            {
                var beforeDocumentY = y - 28;
                var afterDocumentY = beforeDocumentY + 75;
                browserFrameBefore.SetPixel(
                    x,
                    y,
                    System.Drawing.Color.FromArgb(
                        (x * 11 + beforeDocumentY * 19) % 256,
                        (x * 23 + beforeDocumentY * 7) % 256,
                        (x * 5 + beforeDocumentY * 29) % 256));
                browserFrameAfter.SetPixel(
                    x,
                    y,
                    System.Drawing.Color.FromArgb(
                        (x * 11 + afterDocumentY * 19) % 256,
                        (x * 23 + afterDocumentY * 7) % 256,
                        (x * 5 + afterDocumentY * 29) % 256));
            }
        }
        Require(
            !CaptureService.AreFramesVisuallyEquivalent(
                browserFrameBefore,
                browserFrameAfter),
            "rolagem não confunde barras e fundos estáticos com fim da página");
        Require(
            CaptureService.EstimateVerticalScrollDelta(
                browserFrameBefore,
                browserFrameAfter,
                fallbackDelta: 133) == 75,
            "rolagem mede conteúdo mesmo com barra fixa do navegador");

        Require(
            RecordingPresetCatalog.GifFps.Select(item => item.Value).SequenceEqual([10, 20, 30]) &&
            RecordingPresetCatalog.GifQuality.Select(item => item.Value).SequenceEqual([32, 64, 128, 256]) &&
            RecordingPresetCatalog.Mp4Quality.Select(item => item.Value)
                .SequenceEqual(["Baixa", "Média", "Alta", "Muito alta"]),
            "catálogo expõe somente presets seguros e consistentes");
        var migratedRecording = new RecordingSettings
        {
            GifFps = 17,
            GifQuality = 80,
            GifDurationSeconds = 27,
            GifWidth = 1440,
            VideoQuality = "Máxima"
        };
        RecordingPresetCatalog.Normalize(migratedRecording);
        Require(
            migratedRecording.GifFps == 20 &&
            migratedRecording.GifQuality == 64 &&
            migratedRecording.GifDurationSeconds == 27 &&
            migratedRecording.GifWidth == 1440 &&
            migratedRecording.VideoQuality == "Muito alta",
            "configuração antiga migra para preset próximo sem perder campos legados");
        Require(
            RecordingPresetCatalog.NormalizeGifFps(5) == 10 &&
            RecordingPresetCatalog.NormalizeGifFps(15) == 20 &&
            RecordingPresetCatalog.NormalizeGifFps(60) == 30 &&
            RecordingPresetCatalog.NormalizeGifFps(4) == 10 &&
            RecordingPresetCatalog.NormalizeGifFps(17) == 20 &&
            RecordingPresetCatalog.NormalizeGifFps(29) == 30,
            "FPS legado migra para 10, 20 ou 30 conforme a regra definida");
        foreach (var preset in RecordingPresetCatalog.GifFps)
        {
            Require(
                preset.Description.StartsWith($"{preset.Value} FPS.", StringComparison.Ordinal) &&
                !preset.Description.Contains("5 FPS", StringComparison.Ordinal) &&
                !preset.Description.Contains("15 FPS", StringComparison.Ordinal) &&
                !preset.Description.Contains("60 FPS", StringComparison.Ordinal),
                $"descrição do preset GIF {preset.Value} corresponde ao FPS aplicado");
        }
        RequireThrows<ArgumentOutOfRangeException>(
            () => GifRecordingService.ValidateSettings(
                new RecordingSettings { GifFps = 12, GifQuality = 128 }),
            "GIF bloqueia FPS arbitrário");

        var settingsPath = Path.Combine(root, "legacy-settings.json");
        var settingsStore = new JsonFileStore<AppSettings>(settingsPath);
        await File.WriteAllTextAsync(settingsPath,
            """{"capture":{"recording":{"gifFps":17,"gifQuality":80,"gifDurationSeconds":27,"gifWidth":1440,"videoQuality":"Máxima"}}}""");
        var loadedLegacy = await settingsStore.LoadAsync();
        RecordingPresetCatalog.Normalize(loadedLegacy.Capture.Recording);
        await settingsStore.SaveAsync(loadedLegacy);
        var persistedLegacy = await settingsStore.LoadAsync();
        Require(
            persistedLegacy.Capture.Recording.GifFps == 20 &&
            persistedLegacy.Capture.Recording.GifQuality == 64 &&
            persistedLegacy.Capture.Recording.GifDurationSeconds == 27 &&
            persistedLegacy.Capture.Recording.GifWidth == 1440,
            "migração de GIF antigo persiste sem quebrar inicialização");
        Require(
            ScreenRecordingService.CreateMediaPath(
                    new CaptureSettings
                    {
                        OutputDirectoryTemplate = root,
                        FileNameTemplate = "{type}_{date}_{time}"
                    },
                    "video",
                    ".mp4")
                .EndsWith(".mp4", StringComparison.OrdinalIgnoreCase),
            "nome local para gravação MP4");
        var validMp4 = Path.Combine(root, "valid.mp4");
        await WriteValidMp4Async(validMp4);
        ScreenRecordingService.ValidateMp4File(validMp4);
        var invalidMp4 = Path.Combine(root, "invalid.mp4");
        await File.WriteAllBytesAsync(invalidMp4, [0, 1, 2, 3]);
        RequireThrows<InvalidDataException>(
            () => ScreenRecordingService.ValidateMp4File(invalidMp4),
            "MP4 vazio ou sem contêiner não entra no histórico");
        var recorderOptions = ScreenRecordingService.BuildOptions(
            new RecordingTarget(
                RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 1280, 720),
                new IntPtr(1)),
            new RecordingSettings { VideoFps = 30, VideoQuality = "Alta" },
            Path.Combine(root, "screenrecorderlib.log"));
        Require(
            recorderOptions.VideoEncoderOptions.IsHardwareEncodingEnabled &&
            !recorderOptions.VideoEncoderOptions.IsFixedFramerate &&
            !recorderOptions.VideoEncoderOptions.IsThrottlingDisabled &&
            recorderOptions.VideoEncoderOptions.Encoder is H264VideoEncoder h264 &&
            h264.BitrateMode == H264BitrateControlMode.Quality &&
            !recorderOptions.LogOptions.IsLogEnabled &&
            recorderOptions.LogOptions.LogSeverityLevel == LogLevel.Error,
            "MP4 prefere hardware com fallback Media Foundation, sem quadros fixos e sem persistir log nativo");
        var mp4TechnicalValues = new Dictionary<string, (int Bitrate, int Quality)>
        {
            ["Baixa"] = (2_500_000, 55),
            ["Média"] = (5_000_000, 70),
            ["Alta"] = (9_000_000, 85),
            ["Muito alta"] = (16_000_000, 95)
        };
        RecordingAudioMigrationChecks.Run(root);
        var savedAudio = System.Text.Json.JsonSerializer.Deserialize<RecordingSettings>(
            System.Text.Json.JsonSerializer.Serialize(new RecordingSettings { Audio = new() {
                CaptureComputer = false, CaptureMicrophone = true, InputDeviceId = "mic-saved", InputVolume = .6f } }))!;
        Require(!savedAudio.Audio.CaptureComputer && savedAudio.Audio.CaptureMicrophone &&
                savedAudio.Audio.InputDeviceId == "mic-saved" && savedAudio.Audio.InputVolume == .6f,
            "preferências de áudio sobrevivem ao JSON");
        var oldRecording = System.Text.Json.JsonSerializer.Deserialize<RecordingSettings>("{\"VideoFps\":24}")!;
        Require(oldRecording.VideoFps == 24 && oldRecording.Audio.CaptureComputer && !oldRecording.Audio.CaptureMicrophone,
            "configuração anterior migra com PC ligado e microfone desligado");
        var unavailableAudio = new RecordingAudioDevices(new([], null), new([], null));
        Require(unavailableAudio.Validate(new() { CaptureComputer = false, CaptureMicrophone = false }) is null &&
                unavailableAudio.Validate(new() { CaptureComputer = true }) is not null,
            "sem dispositivos permite vídeo silencioso e impede fingir áudio ativo");
        var disconnectedAudio = new RecordingAudioDeviceList([new("available", "Disponível")], null);
        Require(disconnectedAudio.Validate(true, "removed", "o microfone") is not null &&
                disconnectedAudio.Validate(true, string.Empty, "o microfone") is null &&
                disconnectedAudio.Validate(false, "removed", "o microfone") is null,
            "dispositivo desconectado não troca silenciosamente pelo padrão");
        foreach (var preset in RecordingPresetCatalog.Mp4Quality)
        {
            var options = ScreenRecordingService.BuildOptions(
                new RecordingTarget(
                    RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 640, 480),
                    new IntPtr(1)),
                new RecordingSettings { VideoFps = 30, VideoQuality = preset.Value },
                Path.Combine(root, $"mp4-{preset.Value}.log"));
            var expected = mp4TechnicalValues[preset.Value];
            Require(
                options.VideoEncoderOptions.Bitrate == expected.Bitrate &&
                options.VideoEncoderOptions.Quality == expected.Quality &&
                preset.Description.Contains(expected.Quality.ToString(), StringComparison.Ordinal),
                $"preset MP4 {preset.Name} aplica os valores descritos");
        }

        using (var paletteBitmap = new System.Drawing.Bitmap(48, 48))
        {
            using (var graphics = System.Drawing.Graphics.FromImage(paletteBitmap))
            using (var background = new System.Drawing.SolidBrush(
                       System.Drawing.Color.FromArgb(15, 26, 35)))
            {
                graphics.FillRectangle(background, 0, 0, 48, 36);
            }
            for (var y = 36; y < paletteBitmap.Height; y++)
            {
                for (var x = 0; x < paletteBitmap.Width; x++)
                {
                    paletteBitmap.SetPixel(
                        x,
                        y,
                        System.Drawing.Color.FromArgb(
                            (x * 17 + y * 5) % 256,
                            (x * 7 + y * 19) % 256,
                            (x * 23 + y * 11) % 256));
                }
            }

            var paletteSource = GifRecordingService.ToBitmapSource(paletteBitmap);
            var paletteSizes = new List<int>();
            foreach (var preset in RecordingPresetCatalog.GifQuality)
            {
                var quantized = GifRecordingService.Quantize(paletteSource, preset.Value);
                var colors = quantized.Palette?.Colors ?? [];
                paletteSizes.Add(colors.Count);
                Require(
                    quantized.Format == System.Windows.Media.PixelFormats.Indexed8 &&
                    colors.Count is > 0 &&
                    colors.Count <= preset.Value &&
                    colors.Any(color =>
                        Math.Abs(color.R - 15) +
                        Math.Abs(color.G - 26) +
                        Math.Abs(color.B - 35) <= 60) &&
                    preset.Description.Contains(preset.Value.ToString(), StringComparison.Ordinal),
                    $"preset GIF {preset.Name} usa paleta adaptativa limitada às cores descritas");
            }
            Require(
                paletteSizes.Zip(paletteSizes.Skip(1), (before, after) => after >= before)
                    .All(increases => increases),
                "presets GIF maiores não reduzem a variedade da paleta adaptativa");
        }

        Require(
            typeof(SlashText.MainWindow).GetMethod(
                "CreateTrayMenu",
                BindingFlags.NonPublic | BindingFlags.Instance) is not null &&
            typeof(SlashText.MainWindow).Assembly.GetType("SlashText.Views.TrayMenuTheme")?.GetMethod(
                "Apply", BindingFlags.NonPublic | BindingFlags.Static) is not null,
            "bandeja mantém composição nativa e aplicação do tema compartilhado; recursão verificada pelo settings-smoke");
    }
}
