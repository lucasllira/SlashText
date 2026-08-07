using SlashText.Models;
using SlashText.Services;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Diagnostics;
using ScreenRecorderLib;

var engine = new TemplateEngine();
var reference = new DateTimeOffset(2026, 7, 27, 14, 35, 0, TimeSpan.FromHours(-3));
var rendered = engine.Render(
    "{{data}}|{{data_curta}}|{{data_extensa}}|{{hora}}|{{mes}}|{{mes_nome}}|" +
    "{{mes_curto}}|{{ano}}|{{ano_curto}}|{{dia_semana_curto}}|{{data:-7d}}|{{tab}}",
    now: reference);

Require(rendered.StartsWith("27/07/2026|27/07/26|", StringComparison.Ordinal), "datas abreviada e extensa");
Require(rendered.Contains("|14:35|07|", StringComparison.Ordinal), "variáveis automáticas");
Require(rendered.Contains("|2026|26|", StringComparison.Ordinal), "ano completo e abreviado");
Require(rendered.Contains("|20/07/2026|", StringComparison.Ordinal), "cálculo de data");
Require(rendered.EndsWith(TemplateEngine.TabMarker, StringComparison.Ordinal), "marcador Tab");

var nativeInputType = typeof(QuickAccentService).GetNestedType(
    "Input",
    BindingFlags.NonPublic);
Require(nativeInputType is not null, "estrutura nativa do Acento Rápido");
Require(
    Marshal.SizeOf(nativeInputType!) == (Environment.Is64BitProcess ? 40 : 28),
    "estrutura INPUT compatível com SendInput");
Require(
    QuickAccentService.ShouldUseUppercase(shiftDown: false, capsLockOn: true),
    "Caps Lock mantém o acento em maiúsculo");
Require(
    !QuickAccentService.ShouldUseUppercase(shiftDown: true, capsLockOn: true),
    "Shift inverte Caps Lock");
var translationFlags = typeof(KeyboardHookService).GetField(
    "ToUnicodeNoStateChange",
    BindingFlags.NonPublic | BindingFlags.Static);
Require(
    translationFlags?.GetRawConstantValue() is uint flags && flags == 0x04,
    "leitura do teclado não altera o estado de acentos mortos em layouts ABNT");
var portugueseCharacters = QuickAccentService.PreviewCharacters(["PortugueseBrazil"]);
Require(
    portugueseCharacters.Contains('ã') && !portugueseCharacters.Contains('ä'),
    "conjunto somente PT-BR");
Require(
    QuickAccentService.PreviewCharacters(["PortugueseBrazil", "German", "Currency"])
        .Contains('€'),
    "combinação de conjuntos do Acento Rápido");

var fields = engine.GetFillableFields("Olá {{nome}}, chamado {{chamado|INC000}}. {{nome}}");
Require(fields.Count == 2, "campos únicos");
Require(fields[1].DefaultValue == "INC000", "valor padrão");

var root = Path.Combine(Path.GetTempPath(), $"slashtext-smoke-{Guid.NewGuid():N}");
var snippetsFile = Path.Combine(root, "snippets.md");
var backups = Path.Combine(root, "backups");
try
{
    var repository = new SnippetMarkdownRepository(snippetsFile, backups);
    var snippet = new Snippet
    {
        Name = "Teste",
        Trigger = "/teste",
        Category = "Geral",
        Content = "Primeiro"
    };

    await repository.SaveAsync([snippet]);
    snippet.Content = "Segundo";
    await repository.SaveAsync([snippet]);
    snippet.Content = "Terceiro";
    await repository.SaveAsync([snippet]);

    var loaded = await repository.LoadAsync();
    Require(loaded.Count == 1 && loaded[0].Content == "Terceiro", "persistência Markdown");

    var colonSnippet = new Snippet
    {
        Name = "Dois pontos",
        Trigger = ":teste",
        Category = "Geral",
        Content = "Compatível"
    };
    await repository.SaveAsync([snippet, colonSnippet]);
    loaded = await repository.LoadAsync();
    Require(loaded.Any(item => item.Trigger == ":teste"), "gatilho com dois pontos");

    var textBlazeFile = Path.Combine(root, "textblaze.json");
    await File.WriteAllTextAsync(
        textBlazeFile,
        """
        {
          "version": 7,
          "folders": [{
            "name": "Atendimento",
            "snippets": [{
              "name": "Resposta diária",
              "shortcut": "/diario",
              "type": "html",
              "text": "Data: {time: DD/MM/YYYY; shift=-1D} {key: tab}Pronto"
            }]
          }]
        }
        """);
    var importService = new SnippetImportService();
    var textBlazeImport = await importService.ImportAsync(
        textBlazeFile,
        SnippetImportSource.TextBlaze);
    Require(
        textBlazeImport.Snippets.Count == 1 &&
        textBlazeImport.Snippets[0].Category == "Atendimento",
        "importa pastas e atalhos do Text Blaze");
    Require(
        textBlazeImport.Snippets[0].Content.Contains(
            "{{data:-1d|dd/MM/yyyy}} {{tab}}",
            StringComparison.Ordinal),
        "converte data e Tab do Text Blaze");

    var espansoFile = Path.Combine(root, "base.yml");
    await File.WriteAllTextAsync(
        espansoFile,
        """
        matches:
          - trigger: ":ola"
            label: "Saudação"
            replace: |
              Olá!
              Como posso ajudar?
          - triggers: [":obg", ":thanks"]
            replace: "Obrigado!"
        """);
    var espansoImport = await importService.ImportAsync(
        espansoFile,
        SnippetImportSource.Espanso);
    Require(
        espansoImport.Snippets.Count == 3 &&
        espansoImport.Snippets.Any(item => item.Trigger == ":ola") &&
        espansoImport.Snippets.Any(item => item.Trigger == ":thanks"),
        "importa trigger, triggers e blocos do Espanso");

    var usageFile = Path.Combine(root, "usage.json");
    var settingsFile = Path.Combine(root, "settings.json");
    await File.WriteAllTextAsync(settingsFile, """{"theme":"System"}""");
    await File.WriteAllTextAsync(
        usageFile,
        System.Text.Json.JsonSerializer.Serialize(new List<UsageRecord>
        {
            new() { SnippetId = snippet.Id, Count = 3 }
        }));
    var usage = new UsageService(usageFile);
    await usage.LoadAsync();
    Require(usage.Records.Count == 1 && usage.Records[0].Count == 3, "migração de estatísticas antigas");
    await usage.RecordQuickAccentAsync('á');
    var reloadedUsage = new UsageService(usageFile);
    await reloadedUsage.LoadAsync();
    Require(reloadedUsage.QuickAccent.Count == 1, "estatística do Acento Rápido");
    Require(
        reloadedUsage.QuickAccent.Characters.GetValueOrDefault("á") == 1,
        "ranking de caracteres acentuados");

    var backupService = new BackupService(
        backups,
        [snippetsFile, settingsFile, usageFile]);
    backupService.CreateDailySnapshot();
    backupService.CreateDailySnapshot();
    var backupFiles = Directory.GetFiles(backups, "SlashDesk-backup-*.zip");
    Require(backupFiles.Length == 1, "um backup consolidado por dia");
    using (var archive = ZipFile.OpenRead(backupFiles[0]))
    {
        Require(
            archive.Entries.Select(item => item.Name).Order()
                .SequenceEqual(["settings.json", "snippets.md", "usage.json"]),
            "backup contém atalhos, preferências e estatísticas");
    }
    var manualBackup = backupService.CreateManualSnapshot();
    Require(
        File.Exists(manualBackup) && backupService.ListSnapshots().Count == 2,
        "backup manual e listagem de cópias");

    var code = "Antes\n```powershell\nGet-Date\n```\nDepois";
    Require(
        RichTextMarkdownConverter.ToHtml(code).Contains("<pre", StringComparison.Ordinal),
        "bloco de código HTML");
    Require(
        RichTextMarkdownConverter.ToPlainText(code).Contains("Get-Date", StringComparison.Ordinal),
        "fallback de código em texto simples");
    var rich = """
               <p align="center"><span style="font-family:Arial;font-size:16px;background-color:#FFF176">Título</span></p>
               - Primeiro
               - Segundo
               | Nome | Valor |
               | --- | --- |
               | Teste | 10 |
               """;
    var richHtml = RichTextMarkdownConverter.ToHtml(rich);
    Require(richHtml.Contains("<ul>", StringComparison.Ordinal), "lista com marcadores em HTML");
    Require(richHtml.Contains("<table", StringComparison.Ordinal), "tabela em HTML");
    Require(
        richHtml.Contains("background-color:#FFF176", StringComparison.Ordinal),
        "marca-texto em HTML");

    Require(
        CaptureService.ResolveDirectoryTemplate(
                @"C:\Capturas\{year}\{month}",
                reference) ==
            @"C:\Capturas\2026\07",
        "pastas de captura por ano e mês");
    Require(
        CaptureService.SanitizeFileName("Outlook: chamado?") == "Outlook- chamado-",
        "nome de captura remove caracteres inválidos");
    var captureDefaults = new CaptureSettings();
    Require(
        captureDefaults.Recording.VideoFps == 30 &&
        captureDefaults.Recording.GifFps == 10 &&
        captureDefaults.Recording.GifQuality == 128 &&
        captureDefaults.HistoryRetentionDays == 90,
        "padrões seguros de gravação e histórico");
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
        recorderOptions.LogOptions.IsLogEnabled &&
        recorderOptions.LogOptions.LogSeverityLevel == LogLevel.Trace,
        "MP4 prefere hardware com fallback Media Foundation, sem quadros fixos e com log nativo");
    var mp4TechnicalValues = new Dictionary<string, (int Bitrate, int Quality)>
    {
        ["Baixa"] = (2_500_000, 55),
        ["Média"] = (5_000_000, 70),
        ["Alta"] = (9_000_000, 85),
        ["Muito alta"] = (16_000_000, 95)
    };
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

    using (var paletteBitmap = new System.Drawing.Bitmap(8, 8))
    {
        var paletteSource = GifRecordingService.ToBitmapSource(paletteBitmap);
        foreach (var preset in RecordingPresetCatalog.GifQuality)
        {
            var quantized = GifRecordingService.Quantize(paletteSource, preset.Value);
            Require(
                quantized.Palette?.Colors.Count == preset.Value &&
                preset.Description.Contains(preset.Value.ToString(), StringComparison.Ordinal),
                $"preset GIF {preset.Name} aplica a paleta descrita");
        }
    }

    var fakeFactory = new FakeRecorderBackendFactory();
    using (var lifecycle = new ScreenRecordingService(fakeFactory, TimeSpan.FromSeconds(1)))
    {
        var lifecycleTask = lifecycle.StartAsync(
            new RecordingTarget(
                RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 640, 480),
                new IntPtr(1)),
            new CaptureSettings
            {
                OutputDirectoryTemplate = root,
                FileNameTemplate = "lifecycle"
            },
            new RecordingSettings { VideoFps = 30 });
        Require(fakeFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "MP4 inicia backend");
        await WaitUntilAsync(
            () => lifecycle.State == ScreenRecordingState.Recording,
            "estado Recording");
        await Task.Delay(100);
        Require(lifecycle.Elapsed >= TimeSpan.FromMilliseconds(60), "contador MP4 avança");
        lifecycle.Pause();
        await WaitUntilAsync(
            () => lifecycle.State == ScreenRecordingState.Paused,
            "estado Paused");
        var pausedElapsed = lifecycle.Elapsed;
        await Task.Delay(80);
        Require(
            lifecycle.Elapsed - pausedElapsed < TimeSpan.FromMilliseconds(30),
            "contador MP4 não inclui tempo pausado");
        lifecycle.Resume();
        await WaitUntilAsync(
            () => lifecycle.State == ScreenRecordingState.Recording,
            "retorno ao estado Recording");
        await Task.Delay(80);
        Require(lifecycle.Elapsed > pausedElapsed, "contador MP4 retoma do acumulado");
        lifecycle.Stop();
        var stoppedElapsed = lifecycle.Elapsed;
        var lifecyclePath = await lifecycleTask.WaitAsync(TimeSpan.FromSeconds(3));
        Require(File.Exists(lifecyclePath), "MP4 finaliza e publica arquivo");
        Require(lifecycle.State == ScreenRecordingState.Completed, "estado Completed");
        Require(fakeFactory.Backend.DisposeCalls == 1, "Recorder descartado uma única vez");
        Require(fakeFactory.Backend.MaximumConcurrentCalls == 1, "chamadas nativas serializadas");
        Require(
            lifecycle.Elapsed - stoppedElapsed < TimeSpan.FromMilliseconds(30),
            "contador MP4 para no pedido de finalização");
    }

    var duplicateFactory = new FakeRecorderBackendFactory { SendDuplicateCallbacks = true };
    using (var lifecycle = new ScreenRecordingService(duplicateFactory, TimeSpan.FromSeconds(1)))
    {
        var task = lifecycle.StartAsync(
            new RecordingTarget(
                RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 320, 240),
                new IntPtr(1)),
            new CaptureSettings
            {
                OutputDirectoryTemplate = root,
                FileNameTemplate = "duplicate-callback"
            },
            new RecordingSettings());
        Require(duplicateFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para callback duplicado");
        lifecycle.Stop();
        lifecycle.Stop();
        await task.WaitAsync(TimeSpan.FromSeconds(3));
        Require(duplicateFactory.Backend.DisposeCalls == 1, "callback duplicado não duplica descarte");
        Require(duplicateFactory.Backend.StopCalls == 1, "dois cliques em finalizar chamam Stop uma vez");
    }

    var failureFactory = new FakeRecorderBackendFactory { FailOnStop = true };
    using (var lifecycle = new ScreenRecordingService(failureFactory, TimeSpan.FromSeconds(1)))
    {
        var task = lifecycle.StartAsync(
            new RecordingTarget(RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
            new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "failed-callback" },
            new RecordingSettings());
        Require(failureFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para callback de falha");
        lifecycle.Stop();
        await RequireThrowsAsync<InvalidOperationException>(() => task, "callback de falha conclui task");
        Require(lifecycle.State == ScreenRecordingState.Failed, "callback de falha restaura estado terminal");
        Require(failureFactory.Backend.DisposeCalls == 1, "falha descarta Recorder uma vez");
    }

    var delayedFactory = new FakeRecorderBackendFactory { CallbackDelayMs = 120 };
    using (var lifecycle = new ScreenRecordingService(delayedFactory, TimeSpan.FromSeconds(1)))
    {
        var task = lifecycle.StartAsync(
            new RecordingTarget(RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
            new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "delayed-callback" },
            new RecordingSettings());
        Require(delayedFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para callback atrasado");
        lifecycle.Stop();
        await task.WaitAsync(TimeSpan.FromSeconds(3));
        Require(delayedFactory.Backend.DisposeCalls == 1, "callback atrasado finaliza uma vez");
    }

    var blockingFactory = new FakeRecorderBackendFactory { BlockStop = true };
    using (var lifecycle = new ScreenRecordingService(blockingFactory, TimeSpan.FromSeconds(2)))
    {
        var task = lifecycle.StartAsync(
            new RecordingTarget(RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
            new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "closing-finalization" },
            new RecordingSettings());
        Require(blockingFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para Stop bloqueante");
        var uiClock = Stopwatch.StartNew();
        lifecycle.Stop();
        lifecycle.Dispose();
        Require(uiClock.Elapsed < TimeSpan.FromMilliseconds(100), "Stop e Dispose não bloqueiam a UI");
        Require(blockingFactory.Backend.StopEntered.Wait(TimeSpan.FromSeconds(2)), "Stop nativo entrou");
        blockingFactory.Backend.ReleaseStop();
        await task.WaitAsync(TimeSpan.FromSeconds(3));
        Require(blockingFactory.Backend.DisposeCalls == 1, "fechamento durante finalização descarta uma vez");
    }

    var timeoutFactory = new FakeRecorderBackendFactory { CompleteOnStop = false };
    using (var lifecycle = new ScreenRecordingService(timeoutFactory, TimeSpan.FromMilliseconds(80)))
    {
        var task = lifecycle.StartAsync(
            new RecordingTarget(
                RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 320, 240),
                new IntPtr(1)),
            new CaptureSettings
            {
                OutputDirectoryTemplate = root,
                FileNameTemplate = "late-callback"
            },
            new RecordingSettings());
        Require(timeoutFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para timeout");
        lifecycle.Stop();
        await RequireThrowsAsync<TimeoutException>(() => task, "timeout nativo controlado");
        Require(timeoutFactory.Backend.DisposeCalls == 0, "timeout não descarta Recorder durante callback nativo");
        timeoutFactory.Backend.CompleteLater();
        await WaitUntilAsync(
            () => lifecycle.State == ScreenRecordingState.Failed,
            "callback tardio conclui limpeza");
        Require(timeoutFactory.Backend.DisposeCalls == 1, "callback tardio executa descarte único");
    }

    var callerThread = 0;
    var gifCaptureThreads = new System.Collections.Concurrent.ConcurrentBag<int>();
    var pipeline = new GifRecordingService((_, _) =>
    {
        gifCaptureThreads.Add(Environment.CurrentManagedThreadId);
        var bitmap = new System.Drawing.Bitmap(16, 12);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.CornflowerBlue);
        return bitmap;
    });
    GifRecordingResult? pipelineResult = null;
    Exception? pipelineFailure = null;
    var caller = new Thread(() =>
    {
        callerThread = Environment.CurrentManagedThreadId;
        try
        {
            pipelineResult = pipeline.CaptureAsync(
                new System.Drawing.Rectangle(0, 0, 16, 12),
                new RecordingSettings
                {
                    GifFps = 10,
                    GifDurationSeconds = 1,
                    GifWidth = 240,
                    GifQuality = 128
                }).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            pipelineFailure = exception;
        }
    });
    caller.Start();
    caller.Join();
    if (pipelineFailure is not null)
    {
        throw new InvalidOperationException("Pipeline GIF falhou.", pipelineFailure);
    }

    using (pipelineResult ?? throw new InvalidOperationException("Pipeline GIF sem resultado."))
    {
        Require(
            pipelineResult.Metrics is
            {
                RequestedFps: 10,
                CapturedFrames: >= 8,
                StoredFrames: 1
            } metrics &&
            metrics.ProcessedFrames == metrics.CapturedFrames &&
            metrics.EffectiveCapturedFps > 0 &&
            metrics.DuplicateFrames == metrics.CapturedFrames - 1,
            "pipeline GIF descarta quadro idêntico preservando duração");
        Require(
            pipelineResult.Duration >= TimeSpan.FromMilliseconds(900) &&
            pipelineResult.Duration <= TimeSpan.FromMilliseconds(1100),
            "deduplicação GIF preserva o tempo monotônico da sessão");
        Require(
            gifCaptureThreads.All(thread => thread != callerThread),
            "captura e redimensionamento GIF fora da UI thread");
    }

    foreach (var fpsPreset in RecordingPresetCatalog.GifFps)
    {
        using var session = pipeline.StartRecording(
            new System.Drawing.Rectangle(0, 0, 16, 12),
            new RecordingSettings { GifFps = fpsPreset.Value, GifQuality = 128 });
        IRecordingController counter = session;
        Require(counter.Elapsed < TimeSpan.FromMilliseconds(80),
            $"contador GIF inicia em zero a {fpsPreset.Value} FPS");
        await Task.Delay(120);
        Require(counter.Elapsed >= TimeSpan.FromMilliseconds(70),
            $"contador GIF avança a {fpsPreset.Value} FPS");
        counter.Pause();
        var gifPausedAt = counter.Elapsed;
        await Task.Delay(80);
        Require(counter.Elapsed - gifPausedAt < TimeSpan.FromMilliseconds(30),
            $"contador GIF pausa a {fpsPreset.Value} FPS");
        counter.Resume();
        await Task.Delay(80);
        Require(counter.Elapsed > gifPausedAt,
            $"contador GIF retoma a {fpsPreset.Value} FPS");
        counter.Stop();
        var gifStoppedAt = counter.Elapsed;
        using var presetResult = await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Require(presetResult.Fps == fpsPreset.Value,
            $"pipeline mantÃ©m preset de {fpsPreset.Value} FPS");
        Require(counter.Elapsed - gifStoppedAt < TimeSpan.FromMilliseconds(30),
            $"contador GIF para ao finalizar a {fpsPreset.Value} FPS");
        Require(GifRecordingService.QueueCapacity == 2,
            $"fila GIF limitada no preset de {fpsPreset.Value} FPS");
    }

    Require(
        AppPaths.DataDirectory.Equals(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SlashDesk"),
            StringComparison.OrdinalIgnoreCase) &&
        !AppPaths.DataDirectory.StartsWith(AppPaths.BaseDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase),
        "dados permanentes ficam fora da pasta do executável em %LocalAppData%\\SlashDesk");

    using (var frame1 = new System.Drawing.Bitmap(4, 4))
    using (var frame2 = new System.Drawing.Bitmap(4, 4))
    {
        frame1.SetPixel(0, 0, System.Drawing.Color.Red);
        frame2.SetPixel(0, 0, System.Drawing.Color.Blue);
        using var gifRecording = new GifRecordingResult(
            [frame1.Clone() as System.Drawing.Bitmap ?? throw new InvalidOperationException(),
             frame2.Clone() as System.Drawing.Bitmap ?? throw new InvalidOperationException()],
            10,
            new System.Drawing.Rectangle(0, 0, 4, 4));
        var gifPath = new GifRecordingService().Save(
            gifRecording,
            new CaptureSettings
            {
                OutputDirectoryTemplate = root,
                FileNameTemplate = "animated"
            },
            "gif");
        var gifBytes = await File.ReadAllBytesAsync(gifPath);
        Require(
            System.Text.Encoding.ASCII.GetString(gifBytes).Contains(
                "NETSCAPE2.0",
                StringComparison.Ordinal),
            "GIF inclui extensão de repetição NETSCAPE");
        using var gifStream = File.OpenRead(gifPath);
        var decoder = new System.Windows.Media.Imaging.GifBitmapDecoder(
            gifStream,
            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        Require(decoder.Frames.Count == 2, "GIF preserva todos os quadros");
    }
    Require(
        GlobalCaptureShortcutService.IsValid("Ctrl+Shift+PrintScreen"),
        "atalho de captura pelo teclado");
    Require(
        GlobalCaptureShortcutService.IsValid("Ctrl+Shift+WheelUp"),
        "atalho de captura pela roda do mouse");
    Require(
        !GlobalCaptureShortcutService.IsValid("WheelUp"),
        "roda do mouse exige modificador");
    Require(
        GlobalCaptureShortcutService.FormatKeyboardShortcut(
            System.Windows.Input.Key.F10,
            System.Windows.Input.ModifierKeys.None) == "F10",
        "grava tecla de função sem digitação manual");
    Require(
        GlobalCaptureShortcutService.FormatKeyboardShortcut(
            System.Windows.Input.Key.Snapshot,
            System.Windows.Input.ModifierKeys.None) == "PrintScreen" &&
        GlobalCaptureShortcutService.IsValid("Print") &&
        GlobalCaptureShortcutService.IsValid("PrtSc") &&
        GlobalCaptureShortcutService.IsValid("Snapshot"),
        "grava Print Screen no pressionamento ou na liberação");
    Require(
        GlobalCaptureShortcutService.FormatWheelShortcut(
            120,
            System.Windows.Input.ModifierKeys.Control |
            System.Windows.Input.ModifierKeys.Shift) ==
            "Ctrl+Shift+WheelUp",
        "grava combinação com roda do mouse");
    Require(
        GlobalCaptureShortcutService.FormatMouseShortcut(
            System.Windows.Input.MouseButton.XButton1,
            System.Windows.Input.ModifierKeys.Alt) ==
            "Alt+MouseX1",
        "grava botão lateral do mouse");
    Require(
        GlobalCaptureShortcutService.IsValid("MouseX2") &&
        GlobalCaptureShortcutService.IsValid("Ctrl+MouseMiddle"),
        "botões do mouse são atalhos válidos");
    Require(
        !GlobalCaptureShortcutService.IsValid("MouseLeft") &&
        !GlobalCaptureShortcutService.IsValid("MouseRight"),
        "cliques essenciais do mouse permanecem livres");

    using (var source = new System.Drawing.Bitmap(120, 90))
    {
        using (var graphics = System.Drawing.Graphics.FromImage(source))
        {
            graphics.Clear(System.Drawing.Color.White);
        }

        var annotationScenarios = new[]
        {
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Arrow,
                Start = new System.Windows.Point(10, 10),
                End = new System.Windows.Point(90, 60)
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Highlighter,
                Start = new System.Windows.Point(8, 45),
                End = new System.Windows.Point(100, 45),
                Argb = System.Drawing.Color.Gold.ToArgb()
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Rectangle,
                Start = new System.Windows.Point(15, 15),
                End = new System.Windows.Point(80, 65)
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Ellipse,
                Start = new System.Windows.Point(20, 15),
                End = new System.Windows.Point(85, 70)
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Pencil,
                Points =
                [
                    new System.Windows.Point(5, 5),
                    new System.Windows.Point(40, 30),
                    new System.Windows.Point(75, 12)
                ]
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Text,
                Start = new System.Windows.Point(10, 20),
                Text = "Teste"
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Number,
                Start = new System.Windows.Point(55, 42),
                Text = "1"
            }
        };

        foreach (var annotation in annotationScenarios)
        {
            using var renderedCapture = CaptureAnnotationRenderer.Render(
                source,
                [annotation],
                120,
                90);
            Require(
                HasChangedPixel(renderedCapture),
                $"renderiza ferramenta {annotation.Kind}");
        }
    }

    var noCallbackFactory = new FakeRecorderBackendFactory { CompleteOnStop = false };
    using (var lifecycle = new ScreenRecordingService(
               noCallbackFactory,
               TimeSpan.FromMilliseconds(60)))
    {
        var task = lifecycle.StartAsync(
            new RecordingTarget(RecordingTargetKind.Window,
                new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
            new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "no-callback" },
            new RecordingSettings());
        Require(noCallbackFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)),
            "backend para timeout sem callback");
        lifecycle.Stop();
        await RequireThrowsAsync<TimeoutException>(() => task, "timeout real sem callback");
        var disposeClock = Stopwatch.StartNew();
        lifecycle.Dispose();
        Require(disposeClock.Elapsed < TimeSpan.FromMilliseconds(100),
            "Dispose após timeout não bloqueia UI");
        Require(noCallbackFactory.Backend.DisposeCalls == 0,
            "timeout sem callback não concorre Dispose com código nativo");
        Require(lifecycle.State == ScreenRecordingState.Failed,
            "timeout sem callback encerra estado Finalizando");
    }
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, true);
    }
}

Console.WriteLine("SlashText smoke tests: OK");
return;

static bool HasChangedPixel(System.Drawing.Bitmap bitmap)
{
    for (var y = 0; y < bitmap.Height; y += 2)
    {
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            if (bitmap.GetPixel(x, y).ToArgb() !=
                System.Drawing.Color.White.ToArgb())
            {
                return true;
            }
        }
    }
    return false;
}

static void Require(bool condition, string scenario)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Falha no cenário: {scenario}");
    }
}

static void RequireThrows<TException>(Action action, string scenario)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Falha no smoke test: {scenario}");
}

static async Task WaitUntilAsync(Func<bool> condition, string scenario)
{
    var timeout = Stopwatch.StartNew();
    while (!condition())
    {
        if (timeout.Elapsed > TimeSpan.FromSeconds(2))
        {
            throw new InvalidOperationException($"Timeout no cenário: {scenario}");
        }
        await Task.Delay(10);
    }
}

static async Task RequireThrowsAsync<TException>(Func<Task> action, string scenario)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"Falha no smoke test: {scenario}");
}

static Task WriteValidMp4Async(string path) => File.WriteAllBytesAsync(
    path,
    [
        0, 0, 0, 12, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0,
        0, 0, 0, 12, (byte)'m', (byte)'d', (byte)'a', (byte)'t', 1, 2, 3, 4,
        0, 0, 0, 8, (byte)'m', (byte)'o', (byte)'o', (byte)'v'
    ]);

sealed class FakeRecorderBackendFactory : IScreenRecorderBackendFactory
{
    public FakeRecorderBackend Backend { get; } = new();
    public bool SendDuplicateCallbacks
    {
        get => Backend.SendDuplicateCallbacks;
        init => Backend.SendDuplicateCallbacks = value;
    }
    public bool CompleteOnStop
    {
        get => Backend.CompleteOnStop;
        init => Backend.CompleteOnStop = value;
    }
    public bool FailOnStop
    {
        get => Backend.FailOnStop;
        init => Backend.FailOnStop = value;
    }
    public int CallbackDelayMs
    {
        get => Backend.CallbackDelayMs;
        init => Backend.CallbackDelayMs = value;
    }
    public bool BlockStop
    {
        get => Backend.BlockStop;
        init => Backend.BlockStop = value;
    }

    public IScreenRecorderBackend Create(RecorderOptions options) => Backend;
}

sealed class FakeRecorderBackend : IScreenRecorderBackend
{
    private int _activeCalls;
    private int _maximumConcurrentCalls;
    private int _disposeCalls;
    private int _stopCalls;
    private string _path = string.Empty;
    private readonly ManualResetEventSlim _stopRelease = new(false);

    public event EventHandler<RecordingCompleteEventArgs>? Completed;
    public event EventHandler<RecordingFailedEventArgs>? Failed;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
    public ManualResetEventSlim RecordCalled { get; } = new(false);
    public ManualResetEventSlim StopEntered { get; } = new(false);
    public bool SendDuplicateCallbacks { get; set; }
    public bool CompleteOnStop { get; set; } = true;
    public bool FailOnStop { get; set; }
    public int CallbackDelayMs { get; set; }
    public bool BlockStop { get; set; }
    public int DisposeCalls => Volatile.Read(ref _disposeCalls);
    public int StopCalls => Volatile.Read(ref _stopCalls);
    public int MaximumConcurrentCalls => Volatile.Read(ref _maximumConcurrentCalls);

    public void Record(string path)
    {
        NativeCall(() =>
        {
            _path = path;
            RecordCalled.Set();
            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording));
        });
    }

    public void Pause() => NativeCall(() =>
        StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Paused)));

    public void Resume() => NativeCall(() =>
        StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording)));

    public void Stop()
    {
        NativeCall(() =>
        {
            Interlocked.Increment(ref _stopCalls);
            StopEntered.Set();
            if (BlockStop)
            {
                _stopRelease.Wait(TimeSpan.FromSeconds(3));
            }
            if (FailOnStop)
            {
                Failed?.Invoke(this, new RecordingFailedEventArgs("falha simulada", _path));
                return;
            }
            if (CompleteOnStop)
            {
                if (CallbackDelayMs > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(CallbackDelayMs);
                        CompleteLater();
                    });
                }
                else
                {
                    CompleteLater();
                }
            }
        });
    }

    public void ReleaseStop() => _stopRelease.Set();

    public void CompleteLater()
    {
        WriteValidMp4(_path);
        Completed?.Invoke(
            this,
            new RecordingCompleteEventArgs(_path, new List<FrameData>()));
        if (SendDuplicateCallbacks)
        {
            Failed?.Invoke(
                this,
                new RecordingFailedEventArgs("callback tardio", _path));
        }
    }

    public void Dispose()
    {
        NativeCall(() => Interlocked.Increment(ref _disposeCalls));
    }

    private void NativeCall(Action action)
    {
        var concurrent = Interlocked.Increment(ref _activeCalls);
        UpdateMaximum(concurrent);
        try
        {
            Thread.Sleep(15);
            action();
        }
        finally
        {
            Interlocked.Decrement(ref _activeCalls);
        }
    }

    private void UpdateMaximum(int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maximumConcurrentCalls);
            if (current >= value ||
                Interlocked.CompareExchange(ref _maximumConcurrentCalls, value, current) == current)
            {
                return;
            }
        }
    }

    private static void WriteValidMp4(string path) => File.WriteAllBytes(
        path,
        [
            0, 0, 0, 12, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0,
            0, 0, 0, 12, (byte)'m', (byte)'d', (byte)'a', (byte)'t', 1, 2, 3, 4,
            0, 0, 0, 8, (byte)'m', (byte)'o', (byte)'o', (byte)'v'
        ]);
}
