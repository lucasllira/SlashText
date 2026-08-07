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
Require(rendered.Contains("|14:35|07|", StringComparison.Ordinal), "variÃ¡veis automÃ¡ticas");
Require(rendered.Contains("|2026|26|", StringComparison.Ordinal), "ano completo e abreviado");
Require(rendered.Contains("|20/07/2026|", StringComparison.Ordinal), "cÃ¡lculo de data");
Require(rendered.EndsWith(TemplateEngine.TabMarker, StringComparison.Ordinal), "marcador Tab");

var nativeInputType = typeof(QuickAccentService).GetNestedType(
    "Input",
    BindingFlags.NonPublic);
Require(nativeInputType is not null, "estrutura nativa do Acento RÃ¡pido");
Require(
    Marshal.SizeOf(nativeInputType!) == (Environment.Is64BitProcess ? 40 : 28),
    "estrutura INPUT compatÃ­vel com SendInput");
Require(
    QuickAccentService.ShouldUseUppercase(shiftDown: false, capsLockOn: true),
    "Caps Lock mantÃ©m o acento em maiÃºsculo");
Require(
    !QuickAccentService.ShouldUseUppercase(shiftDown: true, capsLockOn: true),
    "Shift inverte Caps Lock");
var translationFlags = typeof(KeyboardHookService).GetField(
    "ToUnicodeNoStateChange",
    BindingFlags.NonPublic | BindingFlags.Static);
Require(
    translationFlags?.GetRawConstantValue() is uint flags && flags == 0x04,
    "leitura do teclado nÃ£o altera o estado de acentos mortos em layouts ABNT");
var portugueseCharacters = QuickAccentService.PreviewCharacters(["PortugueseBrazil"]);
Require(
    portugueseCharacters.Contains('Ã£') && !portugueseCharacters.Contains('Ã¤'),
    "conjunto somente PT-BR");
Require(
    QuickAccentService.PreviewCharacters(["PortugueseBrazil", "German", "Currency"])
        .Contains('â‚¬'),
    "combinaÃ§Ã£o de conjuntos do Acento RÃ¡pido");

var fields = engine.GetFillableFields("OlÃ¡ {{nome}}, chamado {{chamado|INC000}}. {{nome}}");
Require(fields.Count == 2, "campos Ãºnicos");
Require(fields[1].DefaultValue == "INC000", "valor padrÃ£o");

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
    Require(loaded.Count == 1 && loaded[0].Content == "Terceiro", "persistÃªncia Markdown");

    var colonSnippet = new Snippet
    {
        Name = "Dois pontos",
        Trigger = ":teste",
        Category = "Geral",
        Content = "CompatÃ­vel"
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
              "name": "Resposta diÃ¡ria",
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
            label: "SaudaÃ§Ã£o"
            replace: |
              OlÃ¡!
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
    Require(usage.Records.Count == 1 && usage.Records[0].Count == 3, "migraÃ§Ã£o de estatÃ­sticas antigas");
    await usage.RecordQuickAccentAsync('Ã¡');
    var reloadedUsage = new UsageService(usageFile);
    await reloadedUsage.LoadAsync();
    Require(reloadedUsage.QuickAccent.Count == 1, "estatÃ­stica do Acento RÃ¡pido");
    Require(
        reloadedUsage.QuickAccent.Characters.GetValueOrDefault("Ã¡") == 1,
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
                .SequenceEqual(["backup-manifest.json", "settings.json", "snippets.md", "usage.json"]),
            "backup contÃ©m manifesto, atalhos, preferÃªncias e estatÃ­sticas");
    }
    BackupService.ValidateSnapshot(backupFiles[0]);
    var manualBackup = backupService.CreateManualSnapshot();
    Require(
        File.Exists(manualBackup) && backupService.ListSnapshots().Count == 2,
        "backup manual e listagem de cÃ³pias");

    var code = "Antes\n```powershell\nGet-Date\n```\nDepois";
    Require(
        RichTextMarkdownConverter.ToHtml(code).Contains("<pre", StringComparison.Ordinal),
        "bloco de cÃ³digo HTML");
    Require(
        RichTextMarkdownConverter.ToPlainText(code).Contains("Get-Date", StringComparison.Ordinal),
        "fallback de cÃ³digo em texto simples");
    var rich = """
               <p align="center"><span style="font-family:Arial;font-size:16px;background-color:#FFF176">TÃ­tulo</span></p>
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
        "pastas de captura por ano e mÃªs");
    Require(
        CaptureService.SanitizeFileName("Outlook: chamado?") == "Outlook- chamado-",
        "nome de captura remove caracteres invÃ¡lidos");
    var captureDefaults = new CaptureSettings();
    Require(
        captureDefaults.Recording.VideoFps == 30 &&
        captureDefaults.Recording.GifFps == 10 &&
        captureDefaults.Recording.GifQuality == 128 &&
        captureDefaults.HistoryRetentionDays == 90,
        "padrÃµes seguros de gravaÃ§Ã£o e histÃ³rico");
    Require(
        RecordingPresetCatalog.GifFps.Select(item => item.Value).SequenceEqual([10, 20, 30]) &&
        RecordingPresetCatalog.GifQuality.Select(item => item.Value).SequenceEqual([32, 64, 128, 256]) &&
        RecordingPresetCatalog.Mp4Quality.Select(item => item.Value)
            .SequenceEqual(["Baixa", "MÃ©dia", "Alta", "Muito alta"]),
        "catÃ¡logo expÃµe somente presets seguros e consistentes");
    var migratedRecording = new RecordingSettings
    {
        GifFps = 17,
        GifQuality = 80,
        GifDurationSeconds = 27,
        GifWidth = 1440,
        VideoQuality = "MÃ¡xima"
    };
    RecordingPresetCatalog.Normalize(migratedRecording);
    Require(
        migratedRecording.GifFps == 20 &&
        migratedRecording.GifQuality == 64 &&
        migratedRecording.GifDurationSeconds == 27 &&
        migratedRecording.GifWidth == 1440 &&
        migratedRecording.VideoQuality == "Muito alta",
        "configuraÃ§Ã£o antiga migra para preset prÃ³ximo sem perder campos legados");
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
            $"descriÃ§Ã£o do preset GIF {preset.Value} corresponde ao FPS aplicado");
    }
    RequireThrows<ArgumentOutOfRangeException>(
        () => GifRecordingService.ValidateSettings(
            new RecordingSettings { GifFps = 12, GifQuality = 128 }),
        "GIF bloqueia FPS arbitrÃ¡rio");

    var settingsPath = Path.Combine(root, "legacy-settings.json");
    var settingsStore = new JsonFileStore<AppSettings>(settingsPath);
    await File.WriteAllTextAsync(settingsPath,
        """{"capture":{"recording":{"gifFps":17,"gifQuality":80,"gifDurationSeconds":27,"gifWidth":1440,"videoQuality":"MÃ¡xima"}}}""");
    var loadedLegacy = await settingsStore.LoadAsync();
    RecordingPresetCatalog.Normalize(loadedLegacy.Capture.Recording);
    await settingsStore.SaveAsync(loadedLegacy);
    var persistedLegacy = await settingsStore.LoadAsync();
    Require(
        persistedLegacy.Capture.Recording.GifFps == 20 &&
        persistedLegacy.Capture.Recording.GifQuality == 64 &&
        persistedLegacy.Capture.Recording.GifDurationSeconds == 27 &&
        persistedLegacy.Capture.Recording.GifWidth == 1440,
        "migraÃ§Ã£o de GIF antigo persiste sem quebrar inicializaÃ§Ã£o");
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
        "nome local para gravaÃ§Ã£o MP4");
    var validMp4 = Path.Combine(root, "valid.mp4");
    await WriteValidMp4Async(validMp4);
    ScreenRecordingService.ValidateMp4File(validMp4);
    var invalidMp4 = Path.Combine(root, "invalid.mp4");
    await File.WriteAllBytesAsync(invalidMp4, [0, 1, 2, 3]);
    RequireThrows<InvalidDataException>(
        () => ScreenRecordingService.ValidateMp4File(invalidMp4),
        "MP4 vazio ou sem contÃªiner nÃ£o entra no histÃ³rico");
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
        ["MÃ©dia"] = (5_000_000, 70),
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
        var paletteSource = GifRecordingSerïÞ}¶‰žËkºwµç@ÄÈÀ°(€€€€€€€€€€€€€€€€äÀ¤ì(€€€€€€€€€€€I•ÅÕ¥É” (€€€€€€€€€€€€€€€!…Í¡…¹•‘A¥á•°¡É•¹‘•É•‘…ÁÑÕÉ”¤°(€€€€€€€€€€€€€€€€‰É•¹‘•É¥é„™•ÉÉ…µ•¹Ñ„í…¹¹½Ñ…Ñ¥½¸¹-¥¹‘ôˆ¤ì(€€€€€€€ô(€€€ô((€€€Ù…ÈÕÁ‘…Ñ•Q•ÍÑÌ€ôA…Ñ ¹½µ‰¥¹”¡É½½Ð°€‰ÕÁ‘…Ñ•Ìˆ¤ì(€€€¥É•Ñ½Éä¹É•…Ñ•¥É•Ñ½Éä¡ÕÁ‘…Ñ•Q•ÍÑÌ¤ì(€€€Ù…È¹½Ü€ô¹•Ü…Ñ•Q¥µ•=™™Í•Ð ÈÀÈØ°€à°€Ø°€ÄÈ°€À°€À°Q¥µ•MÁ…¸¹i•É¼¤ì(€€€Ù…ÈÕÁ‘…Ñ•!…¹‘±•È€ô¹•Ü…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡I•±•…Í•)Í½¸ ˆÌ¸À¸Àˆ¤¤ì(€€€Ù…ÈÕÁ‘…Ñ•M•ÉÙ¥”€ô¹•ÜUÁ‘…Ñ•M•ÉÙ¥” (€€€€€€€¹•Ü!ÑÑÁ±¥•¹Ð¡ÕÁ‘…Ñ•!…¹‘±•È¤°(€€€€€€€A…Ñ ¹½µ‰¥¹”¡ÕÁ‘…Ñ•Q•ÍÑÌ°€‰ÍÑ…Ñ”¹©Í½¸ˆ¤°(€€€€€€€€ˆÈ¸ä¸Äˆ°(€€€€€€€€ ¤€ôø¹½Ü°(€€€€€€€Q¥µ•MÁ…¸¹É½µM•½¹‘Ì Ä¤¤ì(€€€Ù…ÈÕÁ‘…Ñ•½Õ¹€ô…Ý…¥ÐÕÁ‘…Ñ•M•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤ì(€€€I•ÅÕ¥É” (€€€€€€€ÕÁ‘…Ñ•½Õ¹¹UÁ‘…Ñ•Ù…¥±…‰±”€˜˜(€€€€€€€ÕÁ‘…Ñ•½Õ¹¹1…Ñ•ÍÑY•ÉÍ¥½¸€ôô€ˆÌ¸À¸Àˆ€˜˜(€€€€€€€ÕÁ‘…Ñ•½Õ¹¹I•±•…Í”ü¹A½ÉÑ…‰±•ÍÍ•Ð¹9…µ”€ôô€‰M±…Í¡•Í¬´Ì¸À¸ÀµÁ½ÉÑ…‰±”µÝ¥¸µàØÐ¹é¥Àˆ€˜˜(€€€€€€€ÕÁ‘…Ñ•½Õ¹¹½Ý¹±½…‘M¥é”€ôô€ÄÉ|ÌÐÔ°(€€€€€€€€‰É•±•…Í”•ÍÓ…Ù•°…Á±¥„Ù•ÉÏ¼”…ÉÑ•™…Ñ½Ì•á…Ñ½Ìˆ¤ì(€€€I•ÅÕ¥É”¡ÕÁ‘…Ñ•!…¹‘±•È¹…±±Ì€ôô€Ä°€‰½¹ÍÕ±Ñ„ƒé¹¥„…¼¥Ñ!Õˆˆ¤ì((€€€…Ý…¥ÐÕÁ‘…Ñ•M•ÉÙ¥”¹%¹½É•Y•ÉÍ¥½¹Íå¹Œ ˆÌ¸À¸Àˆ¤ì(€€€Ù…È¥¹½É•€ô…Ý…¥ÐÕÁ‘…Ñ•M•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤ì(€€€I•ÅÕ¥É”¡¥¹½É•¹MÑ…ÑÕÌ€ôôUÁ‘…Ñ•¡•­MÑ…ÑÕÌ¹%¹½É•€˜˜€…¥¹½É•¹UÁ‘…Ñ•Ù…¥±…‰±”°(€€€€€€€€‰Ù•ÉÏ¼¥¹½É…‘„»¼ƒ¤½™•É•¥‘„¹½Ù…µ•¹Ñ”ˆ¤ì(€€€ÕÁ‘…Ñ•!…¹‘±•È¹I•ÍÁ½¹Í•)Í½¸€ôI•±•…Í•)Í½¸ ˆÌ¸Ä¸Àˆ¤ì(€€€Ù…È…™Ñ•É%¹½É•€ô…Ý…¥ÐÕÁ‘…Ñ•M•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤ì(€€€I•ÅÕ¥É”¡…™Ñ•É%¹½É•¹UÁ‘…Ñ•Ù…¥±…‰±”€˜˜…™Ñ•É%¹½É•¹1…Ñ•ÍÑY•ÉÍ¥½¸€ôô€ˆÌ¸Ä¸Àˆ°(€€€€€€€€‰Ù•ÉÏ¼ÍÕÁ•É¥½ÈÙ½±Ñ„„Í•È½™•É•¥‘„ˆ¤ì((€€€…Ý…¥ÐÕÁ‘…Ñ•M•ÉÙ¥”¹I•µ¥¹‘1…Ñ•ÉÍå¹Œ ˆÌ¸Ä¸Àˆ¤ì(€€€Ù…È‘•™•ÉÉ•€ô…Ý…¥ÐÕÁ‘…Ñ•M•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤ì(€€€I•ÅÕ¥É”¡‘•™•ÉÉ•¹MÑ…ÑÕÌ€ôôUÁ‘…Ñ•¡•­MÑ…ÑÕÌ¹•™•ÉÉ•€˜˜€…‘•™•ÉÉ•¹UÁ‘…Ñ•Ù…¥±…‰±”°(€€€€€€€€‰±•µ‰É…È‘•Á½¥Ì…‘¥„Í½µ•¹Ñ”„Ù•ÉÏ¼…ÑÕ…°ˆ¤ì(€€€¹½Ü€¬ôUÁ‘…Ñ•M•ÉÙ¥”¹I•µ¥¹‘1…Ñ•É%¹Ñ•ÉÙ…°€¬Q¥µ•MÁ…¸¹É½µ5¥¹ÕÑ•Ì Ä¤ì(€€€Ù…È…™Ñ•É•™•ÉÉ…°€ô…Ý…¥ÐÕÁ‘…Ñ•M•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤ì(€€€I•ÅÕ¥É”¡…™Ñ•É•™•ÉÉ…°¹UÁ‘…Ñ•Ù…¥±…‰±”°€‰±•µ‰É…È‘•Á½¥ÌÙ½±Ñ„„½™•É••Èˆ¤ì((€€€Ù…È…¡•!…¹‘±•È€ô¹•Ü…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡I•±•…Í•)Í½¸ ˆÌ¸À¸Àˆ¤¤ì(€€€Ù…È…¡•M•ÉÙ¥”€ô¹•ÜUÁ‘…Ñ•M•ÉÙ¥” (€€€€€€€¹•Ü!ÑÑÁ±¥•¹Ð¡…¡•!…¹‘±•È¤°(€€€€€€€A…Ñ ¹½µ‰¥¹”¡ÕÁ‘…Ñ•Q•ÍÑÌ°€‰…¡”µÍÑ…Ñ”¹©Í½¸ˆ¤°(€€€€€€€€ˆÈ¸ä¸Äˆ°(€€€€€€€€ ¤€ôø¹½Ü°(€€€€€€€Q¥µ•MÁ…¸¹É½µM•½¹‘Ì Ä¤¤ì(€€€…Ý…¥ÐQ…Í¬¹]¡•¹±°¡…¡•M•ÉÙ¥”¹¡•­Íå¹Œ ¤°…¡•M•ÉÙ¥”¹¡•­Íå¹Œ ¤¤ì(€€€I•ÅÕ¥É”¡…¡•!…¹‘±•È¹…±±Ì€ôô€Ä°€‰Á•‘¥‘½ÌÍ¥µÕ±Ó‰¹•½ÌÕÍ…´•á±ÕÏ¼”…¡”ˆ¤ì((€€€Ù…ÈÍÑ…‰±•¥±Ñ•É!…¹‘±•È€ô¹•Ü…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡I•±•…Í•)Í½¸ (€€€€€€€€ˆÌ¸À¸Àˆ°¥¹±Õ‘•MÑ…‰±”è™…±Í”°¥¹±Õ‘•É…™ÐèÑÉÕ”°¥¹±Õ‘•AÉ•É•±•…Í”èÑÉÕ”¤¤ì(€€€Ù…ÈÍÑ…‰±•¥±Ñ•ÉM•ÉÙ¥”€ô¹•ÜUÁ‘…Ñ•M•ÉÙ¥” (€€€€€€€¹•Ü!ÑÑÁ±¥•¹Ð¡ÍÑ…‰±•¥±Ñ•É!…¹‘±•È¤°(€€€€€€€A…Ñ ¹½µ‰¥¹”¡ÕÁ‘…Ñ•Q•ÍÑÌ°€‰ÍÑ…‰±”µ™¥±Ñ•È¹©Í½¸ˆ¤°(€€€€€€€€ˆÈ¸ä¸Äˆ°(€€€€€€€€ ¤€ôø¹½Ü°(€€€€€€€Q¥µ•MÁ…¸¹É½µM•½¹‘Ì Ä¤¤ì(€€€Ù…ÈÍÑ…‰±•¥±Ñ•È€ô…Ý…¥ÐÍÑ…‰±•¥±Ñ•ÉM•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤ì(€€€I•ÅÕ¥É”¡ÍÑ…‰±•¥±Ñ•È¹MÑ…ÑÕÌ€ôôUÁ‘…Ñ•¡•­MÑ…ÑÕÌ¹9½I•±•…Í”°(€€€€€€€€‰‘É…™Ð”ÁÉ•É•±•…Í”Ï¼¥¹½É…‘…Ì¹¼…¹…°•ÍÓ…Ù•°ˆ¤ì((€€€Ù…ÈÕÉÉ•¹ÑM•ÉÙ¥”€ô¹•ÜUÁ‘…Ñ•M•ÉÙ¥” (€€€€€€€¹•Ü!ÑÑÁ±¥•¹Ð¡¹•Ü…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡I•±•…Í•)Í½¸ ˆÈ¸ä¸Äˆ¤¤¤°(€€€€€€€A…Ñ ¹½µ‰¥¹”¡ÕÁ‘…Ñ•Q•ÍÑÌ°€‰ÕÉÉ•¹Ð¹©Í½¸ˆ¤°(€€€€€€€€ˆÈ¸ä¸Äˆ°(€€€€€€€€ ¤€ôø¹½Ü°(€€€€€€€Q¥µ•MÁ…¸¹É½µM•½¹‘Ì Ä¤¤ì(€€€I•ÅÕ¥É” ¡…Ý…¥ÐÕÉÉ•¹ÑM•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤¤¹MÑ…ÑÕÌ€ôôUÁ‘…Ñ•¡•­MÑ…ÑÕÌ¹UÁQ½…Ñ”°(€€€€€€€€‰¹•¹¡Õµ„…ÑÕ…±¥é‡Ÿ¼ÅÕ…¹‘¼Ù•ÉÏÕ•Ì½¥¹¥‘•´ˆ¤ì((€€€Ù…È½™™±¥¹•M•ÉÙ¥”€ô¹•ÜUÁ‘…Ñ•M•ÉÙ¥” (€€€€€€€¹•Ü!ÑÑÁ±¥•¹Ð¡¹•Ü…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡¹•Ü!ÑÑÁI•ÅÕ•ÍÑá•ÁÑ¥½¸ ‰½™™±¥¹”ˆ¤¤¤°(€€€€€€€A…Ñ ¹½µ‰¥¹”¡ÕÁ‘…Ñ•Q•ÍÑÌ°€‰½™™±¥¹”¹©Í½¸ˆ¤°(€€€€€€€€ˆÈ¸ä¸Äˆ°(€€€€€€€€ ¤€ôø¹½Ü°(€€€€€€€Q¥µ•MÁ…¸¹É½µM•½¹‘Ì Ä¤¤ì(€€€I•ÅÕ¥É” ¡…Ý…¥Ð½™™±¥¹•M•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤¤¹MÑ…ÑÕÌ€ôôUÁ‘…Ñ•¡•­MÑ…ÑÕÌ¹=™™±¥¹”°(€€€€€€€€‰…ÕÏ©¹¥„‘”¥¹Ñ•É¹•Ð»¼±…»„•ÉÉ¼¥¹Ù…Í¥Ù¼ˆ¤ì((€€€Ù…ÈÑ¥µ•½ÕÑM•ÉÙ¥”€ô¹•ÜUÁ‘…Ñ•M•ÉÙ¥” (€€€€€€€¹•Ü!ÑÑÁ±¥•¹Ð¡¹•Ü…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡Q¥µ•MÁ…¸¹É½µ5¥±±¥Í•½¹‘Ì ÈÀÀ¤¤¤°(€€€€€€€A…Ñ ¹½µ‰¥¹”¡ÕÁ‘…Ñ•Q•ÍÑÌ°€‰Ñ¥µ•½ÕÐ¹©Í½¸ˆ¤°(€€€€€€€€ˆÈ¸ä¸Äˆ°(€€€€€€€€ ¤€ôø¹½Ü°(€€€€€€€Q¥µ•MÁ…¸¹É½µ5¥±±¥Í•½¹‘Ì ÈÀ¤¤ì(€€€I•ÅÕ¥É” ¡…Ý…¥ÐÑ¥µ•½ÕÑM•ÉÙ¥”¹¡•­Íå¹Œ¡™½É”èÑÉÕ”¤¤¹MÑ…ÑÕÌ€ôôUÁ‘…Ñ•¡•­MÑ…ÑÕÌ¹=™™±¥¹”°(€€€€€€€€‰Ñ¥µ•½ÕÐ‘”…ÑÕ…±¥é‡Ÿ¼ƒ¤ÑÉ…Ñ…‘¼ˆ¤ì((€€€Ù…È¹½…±±‰…­…Ñ½Éä€ô¹•Ü…­•I•½É‘•É	…­•¹‘…Ñ½Éäì½µÁ±•Ñ•=¹MÑ½À€ô™…±Í”ôì(€€€ÕÍ¥¹œ€¡Ù…È±¥™•å±”€ô¹•ÜMÉ••¹I•½É‘¥¹M•ÉÙ¥” (€€€€€€€€€€€€€€¹½…±±‰…­…Ñ½Éä°(€€€€€€€€€€€€€€Q¥µ•MÁ…¸¹É½µ5¥±±¥Í•½¹‘Ì ØÀ¤¤¤(€€€ì(€€€€€€€Ù…ÈÑ…Í¬€ô±¥™•å±”¹MÑ…ÉÑÍå¹Œ (€€€€€€€€€€€¹•ÜI•½É‘¥¹Q…É•Ð¡I•½É‘¥¹Q…É•Ñ-¥¹¹]¥¹‘½Ü°(€€€€€€€€€€€€€€€¹•ÜMåÍÑ•´¹É…Ý¥¹œ¹I•Ñ…¹±” À°€À°€ÌÈÀ°€ÈÐÀ¤°¹•Ü%¹ÑAÑÈ Ä¤¤°(€€€€€€€€€€€¹•Ü…ÁÑÕÉ•M•ÑÑ¥¹Ìì=ÕÑÁÕÑ¥É•Ñ½ÉåQ•µÁ±…Ñ”€ôÉ½½Ð°¥±•9…µ•Q•µÁ±…Ñ”€ô€‰¹¼µ…±±‰…¬ˆô°(€€€€€€€€€€€¹•ÜI•½É‘¥¹M•ÑÑ¥¹Ì ¤¤ì(€€€€€€€I•ÅÕ¥É”¡¹½…±±‰…­…Ñ½Éä¹	…­•¹¹I•½É‘…±±•¹]…¥Ð¡Q¥µ•MÁ…¸¹É½µM•½¹‘Ì È¤¤°(€€€€€€€€€€€€‰‰…­•¹Á…É„Ñ¥µ•½ÕÐÍ•´…±±‰…¬ˆ¤ì(€€€€€€€±¥™•å±”¹MÑ½À ¤ì(€€€€€€€…Ý…¥ÐI•ÅÕ¥É•Q¡É½ÝÍÍå¹ŒñQ¥µ•½ÕÑá•ÁÑ¥½¸ø  ¤€ôøÑ…Í¬°€‰Ñ¥µ•½ÕÐÉ•…°Í•´…±±‰…¬ˆ¤ì(€€€€€€€Ù…È‘¥ÍÁ½Í•±½¬€ôMÑ½ÁÝ…Ñ ¹MÑ…ÉÑ9•Ü ¤ì(€€€€€€€±¥™•å±”¹¥ÍÁ½Í” ¤ì(€€€€€€€I•ÅÕ¥É”¡‘¥ÍÁ½Í•±½¬¹±…ÁÍ•€ðQ¥µ•MÁ…¸¹É½µ5¥±±¥Í•½¹‘Ì ÄÀÀ¤°(€€€€€€€€€€€€‰¥ÍÁ½Í”…ÃÍÌÑ¥µ•½ÕÐ»¼‰±½ÅÕ•¥„U$ˆ¤ì(€€€€€€€I•ÅÕ¥É”¡¹½…±±‰…­…Ñ½Éä¹	…­•¹¹¥ÍÁ½Í•…±±Ì€ôô€À°(€€€€€€€€€€€€‰Ñ¥µ•½ÕÐÍ•´…±±‰…¬»¼½¹½ÉÉ”¥ÍÁ½Í”½´Í‘¥¼¹…Ñ¥Ù¼ˆ¤ì(€€€€€€€I•ÅÕ¥É”¡±¥™•å±”¹MÑ…Ñ”€ôôMÉ••¹I•½É‘¥¹MÑ…Ñ”¹…¥±•°(€€€€€€€€€€€€‰Ñ¥µ•½ÕÐÍ•´…±±‰…¬•¹•ÉÉ„•ÍÑ…‘¼¥¹…±¥é…¹‘¼ˆ¤ì(€€€ô)ô)™¥¹…±±ä)ì(€€€¥˜€¡¥É•Ñ½Éä¹á¥ÍÑÌ¡É½½Ð¤¤(€€€ì(€€€€€€€¥É•Ñ½Éä¹•±•Ñ”¡É½½Ð°ÑÉÕ”¤ì(€€€ô)ô()½¹Í½±”¹]É¥Ñ•1¥¹” ‰M±…Í¡Q•áÐÍµ½­”Ñ•ÍÑÌè=,ˆ¤ì)É•ÑÕÉ¸ì()ÍÑ…Ñ¥Œ‰½½°!…Í¡…¹•‘A¥á•°¡MåÍÑ•´¹É…Ý¥¹œ¹	¥Ñµ…À‰¥Ñµ…À¤)ì(€€€™½È€¡Ù…Èä€ô€Àìä€ð‰¥Ñµ…À¹!•¥¡Ðìä€¬ô€È¤(€€€ì(€€€€€€€™½È€¡Ù…Èà€ô€Àìà€ð‰¥Ñµ…À¹]¥‘Ñ ìà€¬ô€È¤(€€€€€€€ì(€€€€€€€€€€€¥˜€¡‰¥Ñµ…À¹•ÑA¥á•°¡à°ä¤¹Q½Éˆ ¤€„ô(€€€€€€€€€€€€€€€MåÍÑ•´¹É…Ý¥¹œ¹½±½È¹]¡¥Ñ”¹Q½Éˆ ¤¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€É•ÑÕÉ¸ÑÉÕ”ì(€€€€€€€€€€€ô(€€€€€€€ô(€€€ô(€€€É•ÑÕÉ¸™…±Í”ì)ô()ÍÑ…Ñ¥ŒÙ½¥I•ÅÕ¥É”¡‰½½°½¹‘¥Ñ¥½¸°ÍÑÉ¥¹œÍ•¹…É¥¼¤)ì(€€€¥˜€ …½¹‘¥Ñ¥½¸¤(€€€ì(€€€€€€€Ñ¡É½Ü¹•Ü%¹Ù…±¥‘=Á•É…Ñ¥½¹á•ÁÑ¥½¸ ‰…±¡„¹¼•»…É¥¼èíÍ•¹…É¥½ôˆ¤ì(€€€ô)ô()ÍÑ…Ñ¥ŒÙ½¥I•ÅÕ¥É•Q¡É½ÝÌñQá•ÁÑ¥½¸ø¡Ñ¥½¸…Ñ¥½¸°ÍÑÉ¥¹œÍ•¹…É¥¼¤(€€€Ý¡•É”Qá•ÁÑ¥½¸€èá•ÁÑ¥½¸)ì(€€€ÑÉä(€€€ì(€€€€€€€…Ñ¥½¸ ¤ì(€€€ô(€€€…Ñ €¡Qá•ÁÑ¥½¸¤(€€€ì(€€€€€€€É•ÑÕÉ¸ì(€€€ô((€€€Ñ¡É½Ü¹•Ü%¹Ù…±¥‘=Á•É…Ñ¥½¹á•ÁÑ¥½¸ ‰…±¡„¹¼Íµ½­”Ñ•ÍÐèíÍ•¹…É¥½ôˆ¤ì)ô()ÍÑ…Ñ¥Œ…Íå¹ŒQ…Í¬]…¥ÑU¹Ñ¥±Íå¹Œ¡Õ¹Œñ‰½½°ø½¹‘¥Ñ¥½¸°ÍÑÉ¥¹œÍ•¹…É¥¼¤)ì(€€€Ù…ÈÑ¥µ•½ÕÐ€ôMÑ½ÁÝ…Ñ ¹MÑ…ÉÑ9•Ü ¤ì(€€€Ý¡¥±”€ …½¹‘¥Ñ¥½¸ ¤¤(€€€ì(€€€€€€€¥˜€¡Ñ¥µ•½ÕÐ¹±…ÁÍ•€øQ¥µ•MÁ…¸¹É½µM•½¹‘Ì È¤¤(€€€€€€€ì(€€€€€€€€€€€Ñ¡É½Ü¹•Ü%¹Ù…±¥‘=Á•É…Ñ¥½¹á•ÁÑ¥½¸ ‰Q¥µ•½ÕÐ¹¼•»…É¥¼èíÍ•¹…É¥½ôˆ¤ì(€€€€€€€ô(€€€€€€€…Ý…¥ÐQ…Í¬¹•±…ä ÄÀ¤ì(€€€ô)ô()ÍÑ…Ñ¥Œ…Íå¹ŒQ…Í¬I•ÅÕ¥É•Q¡É½ÝÍÍå¹ŒñQá•ÁÑ¥½¸ø¡Õ¹ŒñQ…Í¬ø…Ñ¥½¸°ÍÑÉ¥¹œÍ•¹…É¥¼¤(€€€Ý¡•É”Qá•ÁÑ¥½¸€èá•ÁÑ¥½¸)ì(€€€ÑÉä(€€€ì(€€€€€€€…Ý…¥Ð…Ñ¥½¸ ¤ì(€€€ô(€€€…Ñ €¡Qá•ÁÑ¥½¸¤(€€€ì(€€€€€€€É•ÑÕÉ¸ì(€€€ô(€€€Ñ¡É½Ü¹•Ü%¹Ù…±¥‘=Á•É…Ñ¥½¹á•ÁÑ¥½¸ ‰…±¡„¹¼Íµ½­”Ñ•ÍÐèíÍ•¹…É¥½ôˆ¤ì)ô()ÍÑ…Ñ¥ŒQ…Í¬]É¥Ñ•Y…±¥‘5ÀÑÍå¹Œ¡ÍÑÉ¥¹œÁ…Ñ ¤€ôø¥±”¹]É¥Ñ•±±	åÑ•ÍÍå¹Œ (€€€Á…Ñ °(€€€l(€€€€€€€€À°€À°€À°€ÄÈ°€¡‰åÑ”¤˜œ°€¡‰åÑ”¤Ðœ°€¡‰åÑ”¤äœ°€¡‰åÑ”¤Àœ°€À°€À°€À°€À°(€€€€€€€€À°€À°€À°€ÄÈ°€¡‰åÑ”¤´œ°€¡‰åÑ”¤œ°€¡‰åÑ”¤„œ°€¡‰åÑ”¤Ðœ°€Ä°€È°€Ì°€Ð°(€€€€€€€€À°€À°€À°€à°€¡‰åÑ”¤´œ°€¡‰åÑ”¤¼œ°€¡‰åÑ”¤¼œ°€¡‰åÑ”¤Øœ(€€€t¤ì()ÍÑ…Ñ¥ŒÍÑÉ¥¹œI•±•…Í•)Í½¸ (€€€ÍÑÉ¥¹œÙ•ÉÍ¥½¸°(€€€‰½½°¥¹±Õ‘•MÑ…‰±”€ôÑÉÕ”°(€€€‰½½°¥¹±Õ‘•É…™Ð€ô™…±Í”°(€€€‰½½°¥¹±Õ‘•AÉ•É•±•…Í”€ô™…±Í”¤)ì(€€€Ù…ÈÉ•±•…Í•Ì€ô¹•Ü1¥ÍÐñÍÑÉ¥¹œø ¤ì(€€€¥˜€¡¥¹±Õ‘•É…™Ð¤(€€€ì(€€€€€€€É•±•…Í•Ì¹‘¡I•±•…Í•¹ÑÉä¡Ù•ÉÍ¥½¸°‘É…™ÐèÑÉÕ”°ÁÉ•É•±•…Í”è™…±Í”¤¤ì(€€€ô(€€€¥˜€¡¥¹±Õ‘•AÉ•É•±•…Í”¤(€€€ì(€€€€€€€É•±•…Í•Ì¹‘¡I•±•…Í•¹ÑÉä¡Ù•ÉÍ¥½¸€¬€ˆµÉŒ¸Äˆ°‘É…™Ðè™…±Í”°ÁÉ•É•±•…Í”èÑÉÕ”¤¤ì(€€€ô(€€€¥˜€¡¥¹±Õ‘•MÑ…‰±”¤(€€€ì(€€€€€€€É•±•…Í•Ì¹‘¡I•±•…Í•¹ÑÉä¡Ù•ÉÍ¥½¸°‘É…™Ðè™…±Í”°ÁÉ•É•±•…Í”è™…±Í”¤¤ì(€€€ô(€€€É•ÑÕÉ¸€‰míÍÑÉ¥¹œ¹)½¥¸ œ°œ°É•±•…Í•Ì¥õtˆì)ô()ÍÑ…Ñ¥ŒÍÑÉ¥¹œI•±•…Í•¹ÑÉä¡ÍÑÉ¥¹œÙ•ÉÍ¥½¸°‰½½°‘É…™Ð°‰½½°ÁÉ•É•±•…Í”¤€ôø€ˆˆˆ(€€€ì(€€€€€€‰Ñ…}¹…µ”ˆè€‰ÙííÙ•ÉÍ¥½¹õôˆ°(€€€€€€‰¹…µ”ˆè€‰M±…Í¡•Í¬ííÙ•ÉÍ¥½¹õôˆ°(€€€€€€‰‰½‘äˆè€‰9½Ñ…ÌííÙ•ÉÍ¥½¹õôˆ°(€€€€€€‰¡Ñµ±}ÕÉ°ˆè€‰¡ÑÑÁÌè¼½¥Ñ¡Õˆ¹½´½±Õ…Í±±¥É„½M±…Í¡Q•áÐ½É•±•…Í•Ì½Ñ…œ½ÙííÙ•ÉÍ¥½¹õôˆ°(€€€€€€‰ÁÕ‰±¥Í¡•‘}…Ðˆè€ˆÈÀÈØ´Àà´ÀÙPÄÈèÀÀèÀÁhˆ°(€€€€€€‰‘É…™Ðˆèíí‘É…™Ð¹Q½MÑÉ¥¹œ ¤¹Q½1½Ý•É%¹Ù…É¥…¹Ð ¥õô°(€€€€€€‰ÁÉ•É•±•…Í”ˆèííÁÉ•É•±•…Í”¹Q½MÑÉ¥¹œ ¤¹Q½1½Ý•É%¹Ù…É¥…¹Ð ¥õô°(€€€€€€‰…ÍÍ•ÑÌˆèl(€€€€€€€ì(€€€€€€€€€€‰¹…µ”ˆè€‰M±…Í¡•Í¬µííÙ•ÉÍ¥½¹õôµÁ½ÉÑ…‰±”µÝ¥¸µàØÐ¹é¥Àˆ°(€€€€€€€€€€‰‰É½ÝÍ•É}‘½Ý¹±½…‘}ÕÉ°ˆè€‰¡ÑÑÁÌè¼½•á…µÁ±”¹¥¹Ù…±¥½M±…Í¡•Í¬µííÙ•ÉÍ¥½¹õôµÁ½ÉÑ…‰±”µÝ¥¸µàØÐ¹é¥Àˆ°(€€€€€€€€€€‰Í¥é”ˆè€ÄÈÌÐÔ(€€€€€€€ô°(€€€€€€€ì(€€€€€€€€€€‰¹…µ”ˆè€‰M±…Í¡•Í¬µííÙ•ÉÍ¥½¹õôµÁ½ÉÑ…‰±”µÝ¥¸µàØÐ¹é¥À¹Í¡„ÈÔØˆ°(€€€€€€€€€€‰‰É½ÝÍ•É}‘½Ý¹±½…‘}ÕÉ°ˆè€‰¡ÑÑÁÌè¼½•á…µÁ±”¹¥¹Ù…±¥½M±…Í¡•Í¬µííÙ•ÉÍ¥½¹õôµÁ½ÉÑ…‰±”µÝ¥¸µàØÐ¹é¥À¹Í¡„ÈÔØˆ°(€€€€€€€€€€‰Í¥é”ˆè€ÄÈà(€€€€€€€ô(€€€€€t(€€€ô(€€€€ˆˆˆì()Í•…±•±…ÍÌ…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È€è!ÑÑÁ5•ÍÍ…•!…¹‘±•È)ì(€€€ÁÉ¥Ù…Ñ”É•…‘½¹±äá•ÁÑ¥½¸ü}•á•ÁÑ¥½¸ì(€€€ÁÉ¥Ù…Ñ”É•…‘½¹±äQ¥µ•MÁ…¸}‘•±…äì(€€€ÁÉ¥Ù…Ñ”¥¹Ð}…±±Ìì((€€€ÁÕ‰±¥Œ…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡ÍÑÉ¥¹œÉ•ÍÁ½¹Í•)Í½¸¤€ôøI•ÍÁ½¹Í•)Í½¸€ôÉ•ÍÁ½¹Í•)Í½¸ì(€€€ÁÕ‰±¥Œ…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡á•ÁÑ¥½¸•á•ÁÑ¥½¸¤€ôø}•á•ÁÑ¥½¸€ô•á•ÁÑ¥½¸ì(€€€ÁÕ‰±¥Œ…­•UÁ‘…Ñ•!ÑÑÁ!…¹‘±•È¡Q¥µ•MÁ…¸‘•±…ä¤(€€€ì(€€€€€€€}‘•±…ä€ô‘•±…äì(€€€€€€€I•ÍÁ½¹Í•)Í½¸€ô€‰mtˆì(€€€ô((€€€ÁÕ‰±¥ŒÍÑÉ¥¹œI•ÍÁ½¹Í•)Í½¸ì•ÐìÍ•Ðìô€ô€‰mtˆì(€€€ÁÕ‰±¥Œ¥¹Ð…±±Ì€ôøY½±…Ñ¥±”¹I•…¡É•˜}…±±Ì¤ì((€€€ÁÉ½Ñ•Ñ•½Ù•ÉÉ¥‘”…Íå¹ŒQ…Í¬ñ!ÑÑÁI•ÍÁ½¹Í•5•ÍÍ…”øM•¹‘Íå¹Œ (€€€€€€€!ÑÑÁI•ÅÕ•ÍÑ5•ÍÍ…”É•ÅÕ•ÍÐ°(€€€€€€€…¹•±±…Ñ¥½¹Q½­•¸…¹•±±…Ñ¥½¹Q½­•¸¤(€€€ì(€€€€€€€%¹Ñ•É±½­•¹%¹É•µ•¹Ð¡É•˜}…±±Ì¤ì(€€€€€€€¥˜€¡}•á•ÁÑ¥½¸¥Ì¹½Ð¹Õ±°¤(€€€€€€€ì(€€€€€€€€€€€Ñ¡É½Ü}•á•ÁÑ¥½¸ì(€€€€€€€ô(€€€€€€€¥˜€¡}‘•±…ä€øQ¥µ•MÁ…¸¹i•É¼¤(€€€€€€€ì(€€€€€€€€€€€…Ý…¥ÐQ…Í¬¹•±…ä¡}‘•±…ä°…¹•±±…Ñ¥½¹Q½­•¸¤ì(€€€€€€€ô(€€€€€€€É•ÑÕÉ¸¹•Ü!ÑÑÁI•ÍÁ½¹Í•5•ÍÍ…”¡MåÍÑ•´¹9•Ð¹!ÑÑÁMÑ…ÑÕÍ½‘”¹=,¤(€€€€€€€ì(€€€€€€€€€€€½¹Ñ•¹Ð€ô¹•ÜMÑÉ¥¹½¹Ñ•¹Ð¡I•ÍÁ½¹Í•)Í½¸¤(€€€€€€€ôì(€€€ô)ô()Í•…±•±…ÍÌ…­•I•½É‘•É	…­•¹‘…Ñ½Éä€è%MÉ••¹I•½É‘•É	…­•¹‘…Ñ½Éä)ì(€€€ÁÕ‰±¥Œ…­•I•½É‘•É	…­•¹	…­•¹ì•Ðìô€ô¹•Ü ¤ì(€€€ÁÕ‰±¥Œ‰½½°M•¹‘ÕÁ±¥…Ñ•…±±‰…­Ì(€€€ì(€€€€€€€•Ð€ôø	…­•¹¹M•¹‘ÕÁ±¥…Ñ•…±±‰…­Ìì(€€€€€€€¥¹¥Ð€ôø	…­•¹¹M•¹‘ÕÁ±¥…Ñ•…±±‰…­Ì€ôÙ…±Õ”ì(€€€ô(€€€ÁÕ‰±¥Œ‰½½°½µÁ±•Ñ•=¹MÑ½À(€€€ì(€€€€€€€•Ð€ôø	…­•¹¹½µÁ±•Ñ•=¹MÑ½Àì(€€€€€€€¥¹¥Ð€ôø	…­•¹¹½µÁ±•Ñ•=¹MÑ½À€ôÙ…±Õ”ì(€€€ô(€€€ÁÕ‰±¥Œ‰½½°…¥±=¹MÑ½À(€€€ì(€€€€€€€•Ð€ôø	…­•¹¹…¥±=¹MÑ½Àì(€€€€€€€¥¹¥Ð€ôø	…­•¹¹…¥±=¹MÑ½À€ôÙ…±Õ”ì(€€€ô(€€€ÁÕ‰±¥Œ¥¹Ð…±±‰…­•±…å5Ì(€€€ì(€€€€€€€•Ð€ôø	…­•¹¹…±±‰…­•±…å5Ìì(€€€€€€€¥¹¥Ð€ôø	…­•¹¹…±±‰…­•±…å5Ì€ôÙ…±Õ”ì(€€€ô(€€€ÁÕ‰±¥Œ‰½½°	±½­MÑ½À(€€€ì(€€€€€€€•Ð€ôø	…­•¹¹	±½­MÑ½Àì(€€€€€€€¥¹¥Ð€ôø	…­•¹¹	±½­MÑ½À€ôÙ…±Õ”ì(€€€ô((€€€ÁÕ‰±¥Œ%MÉ••¹I•½É‘•É	…­•¹É•…Ñ”¡I•½É‘•É=ÁÑ¥½¹Ì½ÁÑ¥½¹Ì¤€ôø	…­•¹ì)ô()Í•…±•±…ÍÌ…­•I•½É‘•É	…­•¹€è%MÉ••¹I•½É‘•É	…­•¹)ì(€€€ÁÉ¥Ù…Ñ”¥¹Ð}…Ñ¥Ù•…±±Ìì(€€€ÁÉ¥Ù…Ñ”¥¹Ð}µ…á¥µÕµ½¹ÕÉÉ•¹Ñ…±±Ìì(€€€ÁÉ¥Ù…Ñ”¥¹Ð}‘¥ÍÁ½Í•…±±Ìì(€€€ÁÉ¥Ù…Ñ”¥¹Ð}ÍÑ½Á…±±Ìì(€€€ÁÉ¥Ù…Ñ”ÍÑÉ¥¹œ}Á…Ñ €ôÍÑÉ¥¹œ¹µÁÑäì(€€€ÁÉ¥Ù…Ñ”É•…‘½¹±ä5…¹Õ…±I•Í•ÑÙ•¹ÑM±¥´}ÍÑ½ÁI•±•…Í”€ô¹•Ü¡™…±Í”¤ì((€€€ÁÕ‰±¥Œ•Ù•¹ÐÙ•¹Ñ!…¹‘±•ÈñI•½É‘¥¹½µÁ±•Ñ•Ù•¹ÑÉÌøü½µÁ±•Ñ•ì(€€€ÁÕ‰±¥Œ•Ù•¹ÐÙ•¹Ñ!…¹‘±•ÈñI•½É‘¥¹…¥±•‘Ù•¹ÑÉÌøü…¥±•ì(€€€ÁÕ‰±¥Œ•Ù•¹ÐÙ•¹Ñ!…¹‘±•ÈñI•½É‘¥¹MÑ…ÑÕÍÙ•¹ÑÉÌøüMÑ…ÑÕÍ¡…¹•ì(€€€ÁÕ‰±¥Œ5…¹Õ…±I•Í•ÑÙ•¹ÑM±¥´I•½É‘…±±•ì•Ðìô€ô¹•Ü¡™…±Í”¤ì(€€€ÁÕ‰±¥Œ5…¹Õ…±I•Í•ÑÙ•¹ÑM±¥´MÑ½Á¹Ñ•É•ì•Ðìô€ô¹•Ü¡™…±Í”¤ì(€€€ÁÕ‰±¥Œ‰½½°M•¹‘ÕÁ±¥…Ñ•…±±‰…­Ìì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ‰½½°½µÁ±•Ñ•=¹MÑ½Àì•ÐìÍ•Ðìô€ôÑÉÕ”ì(€€€ÁÕ‰±¥Œ‰½½°…¥±=¹MÑ½Àì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ¥¹Ð…±±‰…­•±…å5Ìì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ‰½½°	±½­MÑ½Àì•ÐìÍ•Ðìô(€€€ÁÕ‰±¥Œ¥¹Ð¥ÍÁ½Í•…±±Ì€ôøY½±…Ñ¥±”¹I•…¡É•˜}‘¥ÍÁ½Í•…±±Ì¤ì(€€€ÁÕ‰±¥Œ¥¹ÐMÑ½Á…±±Ì€ôøY½±…Ñ¥±”¹I•…¡É•˜}ÍÑ½Á…±±Ì¤ì(€€€ÁÕ‰±¥Œ¥¹Ð5…á¥µÕµ½¹ÕÉÉ•¹Ñ…±±Ì€ôøY½±…Ñ¥±”¹I•…¡É•˜}µ…á¥µÕµ½¹ÕÉÉ•¹Ñ…±±Ì¤ì((€€€ÁÕ‰±¥ŒÙ½¥I•½É¡ÍÑÉ¥¹œÁ…Ñ ¤(€€€ì(€€€€€€€9…Ñ¥Ù•…±°  ¤€ôø(€€€€€€€ì(€€€€€€€€€€€}Á…Ñ €ôÁ…Ñ ì(€€€€€€€€€€€I•½É‘…±±•¹M•Ð ¤ì(€€€€€€€€€€€MÑ…ÑÕÍ¡…¹•ü¹%¹Ù½­”¡Ñ¡¥Ì°¹•ÜI•½É‘¥¹MÑ…ÑÕÍÙ•¹ÑÉÌ¡I•½É‘•ÉMÑ…ÑÕÌ¹I•½É‘¥¹œ¤¤ì(€€€€€€€ô¤ì(€€€ô((€€€ÁÕ‰±¥ŒÙ½¥A…ÕÍ” ¤€ôø9…Ñ¥Ù•…±°  ¤€ôø(€€€€€€€MÑ…ÑÕÍ¡…¹•ü¹%¹Ù½­”¡Ñ¡¥Ì°¹•ÜI•½É‘¥¹MÑ…ÑÕÍÙ•¹ÑÉÌ¡I•½É‘•ÉMÑ…ÑÕÌ¹A…ÕÍ•¤¤¤ì((€€€ÁÕ‰±¥ŒÙ½¥I•ÍÕµ” ¤€ôø9…Ñ¥Ù•…±°  ¤€ôø(€€€€€€€MÑ…ÑÕÍ¡…¹•ü¹%¹Ù½­”¡Ñ¡¥Ì°¹•ÜI•½É‘¥¹MÑ…ÑÕÍÙ•¹ÑÉÌ¡I•½É‘•ÉMÑ…ÑÕÌ¹I•½É‘¥¹œ¤¤¤ì((€€€ÁÕ‰±¥ŒÙ½¥MÑ½À ¤(€€€ì(€€€€€€€9…Ñ¥Ù•…±°  ¤€ôø(€€€€€€€ì(€€€€€€€€€€€%¹Ñ•É±½­•¹%¹É•µ•¹Ð¡É•˜}ÍÑ½Á…±±Ì¤ì(€€€€€€€€€€€MÑ½Á¹Ñ•É•¹M•Ð ¤ì(€€€€€€€€€€€¥˜€¡	±½­MÑ½À¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€}ÍÑ½ÁI•±•…Í”¹]…¥Ð¡Q¥µ•MÁ…¸¹É½µM•½¹‘Ì Ì¤¤ì(€€€€€€€€€€€ô(€€€€€€€€€€€¥˜€¡…¥±=¹MÑ½À¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€…¥±•ü¹%¹Ù½­”¡Ñ¡¥Ì°¹•ÜI•½É‘¥¹…¥±•‘Ù•¹ÑÉÌ ‰™…±¡„Í¥µÕ±…‘„ˆ°}Á…Ñ ¤¤ì(€€€€€€€€€€€€€€€É•ÑÕÉ¸ì(€€€€€€€€€€€ô(€€€€€€€€€€€¥˜€¡½µÁ±•Ñ•=¹MÑ½À¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€¥˜€¡…±±‰…­•±…å5Ì€ø€À¤(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€|€ôQ…Í¬¹IÕ¸¡…Íå¹Œ€ ¤€ôø(€€€€€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€€€€€…Ý…¥ÐQ…Í¬¹•±…ä¡…±±‰…­•±…å5Ì¤ì(€€€€€€€€€€€€€€€€€€€€€€€½µÁ±•Ñ•1…Ñ•È ¤ì(€€€€€€€€€€€€€€€€€€€ô¤ì(€€€€€€€€€€€€€€€ô(€€€€€€€€€€€€€€€•±Í”(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€½µÁ±•Ñ•1…Ñ•È ¤ì(€€€€€€€€€€€€€€€ô(€€€€€€€€€€€ô(€€€€€€€ô¤ì(€€€ô((€€€ÁÕ‰±¥ŒÙ½¥I•±•…Í•MÑ½À ¤€ôø}ÍÑ½ÁI•±•…Í”¹M•Ð ¤ì((€€€ÁÕ‰±¥ŒÙ½¥½µÁ±•Ñ•1…Ñ•È ¤(€€€ì(€€€€€€€]É¥Ñ•Y…±¥‘5ÀÐ¡}Á…Ñ ¤ì(€€€€€€€½µÁ±•Ñ•ü¹%¹Ù½­” (€€€€€€€€€€€Ñ¡¥Ì°(€€€€€€€€€€€¹•ÜI•½É‘¥¹½µÁ±•Ñ•Ù•¹ÑÉÌ¡}Á…Ñ °¹•Ü1¥ÍÐñÉ…µ•…Ñ„ø ¤¤¤ì(€€€€€€€¥˜€¡M•¹‘ÕÁ±¥…Ñ•…±±‰…­Ì¤(€€€€€€€ì(€€€€€€€€€€€…¥±•ü¹%¹Ù½­” (€€€€€€€€€€€€€€€Ñ¡¥Ì°(€€€€€€€€€€€€€€€¹•ÜI•½É‘¥¹…¥±•‘Ù•¹ÑÉÌ ‰…±±‰…¬Ñ…É‘¥¼ˆ°}Á…Ñ ¤¤ì(€€€€€€€ô(€€€ô((€€€ÁÕ‰±¥ŒÙ½¥¥ÍÁ½Í” ¤(€€€ì(€€€€€€€9…Ñ¥Ù•…±°  ¤€ôø%¹Ñ•É±½­•¹%¹É•µ•¹Ð¡É•˜}‘¥ÍÁ½Í•…±±Ì¤¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥9…Ñ¥Ù•…±°¡Ñ¥½¸…Ñ¥½¸¤(€€€ì(€€€€€€€Ù…È½¹ÕÉÉ•¹Ð€ô%¹Ñ•É±½­•¹%¹É•µ•¹Ð¡É•˜}…Ñ¥Ù•…±±Ì¤ì(€€€€€€€UÁ‘…Ñ•5…á¥µÕ´¡½¹ÕÉÉ•¹Ð¤ì(€€€€€€€ÑÉä(€€€€€€€ì(€€€€€€€€€€€Q¡É•…¹M±••À ÄÔ¤ì(€€€€€€€€€€€…Ñ¥½¸ ¤ì(€€€€€€€ô(€€€€€€€™¥¹…±±ä(€€€€€€€ì(€€€€€€€€€€€%¹Ñ•É±½­•¹•É•µ•¹Ð¡É•˜}…Ñ¥Ù•…±±Ì¤ì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥UÁ‘…Ñ•5…á¥µÕ´¡¥¹ÐÙ…±Õ”¤(€€€ì(€€€€€€€Ý¡¥±”€¡ÑÉÕ”¤(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÕÉÉ•¹Ð€ôY½±…Ñ¥±”¹I•…¡É•˜}µ…á¥µÕµ½¹ÕÉÉ•¹Ñ…±±Ì¤ì(€€€€€€€€€€€¥˜€¡ÕÉÉ•¹Ð€øôÙ…±Õ”ñð(€€€€€€€€€€€€€€€%¹Ñ•É±½­•¹½µÁ…É•á¡…¹”¡É•˜}µ…á¥µÕµ½¹ÕÉÉ•¹Ñ…±±Ì°Ù…±Õ”°ÕÉÉ•¹Ð¤€ôôÕÉÉ•¹Ð¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€É•ÑÕÉ¸ì(€€€€€€€€€€€ô(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÙ½¥]É¥Ñ•Y…±¥‘5ÀÐ¡ÍÑÉ¥¹œÁ…Ñ ¤€ôø¥±”¹]É¥Ñ•±±	åÑ•Ì (€€€€€€€Á…Ñ °(€€€€€€€l(€€€€€€€€€€€€À°€À°€À°€ÄÈ°€¡‰åÑ”¤˜œ°€¡‰åÑ”¤Ðœ°€¡‰åÑ”¤äœ°€¡‰åÑ”¤Àœ°€À°€À°€À°€À°(€€€€€€€€€€€€À°€À°€À°€ÄÈ°€¡‰åÑ”¤´œ°€¡‰åÑ”¤œ°€¡‰åÑ”¤„œ°€¡‰åÑ”¤Ðœ°€Ä°€È°€Ì°€Ð°(€€€€€€€€€€€€À°€À°€À°€à°€¡‰åÑ”¤´œ°€¡‰åÑ”¤¼œ°€¡‰åÑ”¤¼œ°€¡‰åÑ”¤Øœ(€€€€€€€t¤ì)ô