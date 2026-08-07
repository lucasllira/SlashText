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
Require(rendered.Contains("|14:35|07|", StringComparison.Ordinal), "vari√°veis autom√°ticas");
Require(rendered.Contains("|2026|26|", StringComparison.Ordinal), "ano completo e abreviado");
Require(rendered.Contains("|20/07/2026|", StringComparison.Ordinal), "c√°lculo de data");
Require(rendered.EndsWith(TemplateEngine.TabMarker, StringComparison.Ordinal), "marcador Tab");

var nativeInputType = typeof(QuickAccentService).GetNestedType(
    "Input",
    BindingFlags.NonPublic);
Require(nativeInputType is not null, "estrutura nativa do Acento R√°pido");
Require(
    Marshal.SizeOf(nativeInputType!) == (Environment.Is64BitProcess ? 40 : 28),
    "estrutura INPUT compat√≠vel com SendInput");
Require(
    QuickAccentService.ShouldUseUppercase(shiftDown: false, capsLockOn: true),
    "Caps Lock mant√©m o acento em mai√∫sculo");
Require(
    !QuickAccentService.ShouldUseUppercase(shiftDown: true, capsLockOn: true),
    "Shift inverte Caps Lock");
var translationFlags = typeof(KeyboardHookService).GetField(
    "ToUnicodeNoStateChange",
    BindingFlags.NonPublic | BindingFlags.Static);
Require(
    translationFlags?.GetRawConstantValue() is uint flags && flags == 0x04,
    "leitura do teclado n√£o altera o estado de acentos mortos em layouts ABNT");
var portugueseCharacters = QuickAccentService.PreviewCharacters(["PortugueseBrazil"]);
Require(
    portugueseCharacters.Contains('√£') && !portugueseCharacters.Contains('√§'),
    "conjunto somente PT-BR");
Require(
    QuickAccentService.PreviewCharacters(["PortugueseBrazil", "German", "Currency"])
        .Contains('‚Ç¨'),
    "combina√ß√£o de conjuntos do Acento R√°pido");

var fields = engine.GetFillableFields("Ol√° {{nome}}, chamado {{chamado|INC000}}. {{nome}}");
Require(fields.Count == 2, "campos √∫nicos");
Require(fields[1].DefaultValue == "INC000", "valor padr√£o");

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
    Require(loaded.Count == 1 && loaded[0].Content == "Terceiro", "persist√™ncia Markdown");

    var colonSnippet = new Snippet
    {
        Name = "Dois pontos",
        Trigger = ":teste",
        Category = "Geral",
        Content = "Compat√≠vel"
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
              "name": "Resposta di√°ria",
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
            label: "Sauda√ß√£o"
            replace: |
              Ol√°!
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
    Require(usage.Records.Count == 1 && usage.Records[0].Count == 3, "migra√ß√£o de estat√≠sticas antigas");
    await usage.RecordQuickAccentAsync('√°');
    var reloadedUsage = new UsageService(usageFile);
    await reloadedUsage.LoadAsync();
    Require(reloadedUsage.QuickAccent.Count == 1, "estat√≠stica do Acento R√°pido");
    Require(
        reloadedUsage.QuickAccent.Characters.GetValueOrDefault("√°") == 1,
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
            "backup cont√©m atalhos, prefer√™ncias e estat√≠sticas");
    }
    var manualBackup = backupService.CreateManualSnapshot();
    Require(
        File.Exists(manualBackup) && backupService.ListSnapshots().Count == 2,
        "backup manual e listagem de c√≥pias");

    var code = "Antes\n```powershell\nGet-Date\n```\nDepois";
    Require(
        RichTextMarkdownConverter.ToHtml(code).Contains("<pre", StringComparison.Ordinal),
        "bloco de c√≥digo HTML");
    Require(
        RichTextMarkdownConverter.ToPlainText(code).Contains("Get-Date", StringComparison.Ordinal),
        "fallback de c√≥digo em texto simples");
    var rich = """
               <p align="center"><span style="font-family:Arial;font-size:16px;background-color:#FFF176">T√≠tulo</span></p>
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
        "pastas de captura por ano e m√™s");
    Require(
        CaptureService.SanitizeFileName("Outlook: chamado?") == "Outlook- chamado-",
        "nome de captura remove caracteres inv√°lidos");
    var captureDefaults = new CaptureSettings();
    Require(
        captureDefaults.Recording.VideoFps == 30 &&
        captureDefaults.Recording.GifFps == 10 &&
        captureDefaults.Recording.GifQuality == 128 &&
        captureDefaults.HistoryRetentionDays == 90,
        "padr√µes seguros de grava√ß√£o e hist√≥rico");
    Require(
        RecordingPresetCatalog.GifFps.Select(item => item.Value).SequenceEqual([10, 20, 30]) &&
        RecordingPresetCatalog.GifQuality.Select(item => item.Value).SequenceEqual([32, 64, 128, 256]) &&
        RecordingPresetCatalog.Mp4Quality.Select(item => item.Value)
            .SequenceEqual(["Baixa", "M√©dia", "Alta", "Muito alta"]),
        "cat√°logo exp√µe somente presets seguros e consistentes");
    var migratedRecording = new RecordingSettings
    {
        GifFps = 17,
        GifQuality = 80,
        GifDurationSeconds = 27,
        GifWidth = 1440,
        VideoQuality = "M√°xima"
    };
    RecordingPresetCatalog.Normalize(migratedRecording);
    Require(
        migratedRecording.GifFps == 20 &&
        migratedRecording.GifQuality == 64 &&
        migratedRecording.GifDurationSeconds == 27 &&
        migratedRecording.GifWidth == 1440 &&
        migratedRecording.VideoQuality == "Muito alta",
        "configura√ß√£o antiga migra para preset pr√≥ximo sem perder campos legados");
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
            $"descri√ß√£o do preset GIF {preset.Value} corresponde ao FPS aplicado");
    }
    RequireThrows<ArgumentOutOfRangeException>(
        () => GifRecordingService.ValidateSettings(
            new RecordingSettings { GifFps = 12, GifQuality = 128 }),
        "GIF bloqueia FPS arbitr√°rio");

    var settingsPath = Path.Combine(root, "legacy-settings.json");
    var settingsStore = new JsonFileStore<AppSettings>(settingsPath);
    await File.WriteAllTextAsync(settingsPath,
        """{"capture":{"recording":{"gifFps":17,"gifQuality":80,"gifDurationSeconds":27,"gifWidth":1440,"videoQuality":"M√°xima"}}}""");
    var loadedLegacy = await settingsStore.LoadAsync();
    RecordingPresetCatalog.Normalize(loadedLegacy.Capture.Recording);
    await settingsStore.SaveAsync(loadedLegacy);
    var persistedLegacy = await settingsStore.LoadAsync();
    Require(
        persistedLegacy.Capture.Recording.GifFps == 20 &&
        persistedLegacy.Capture.Recording.GifQuality == 64 &&
        persistedLegacy.Capture.Recording.GifDurationSeconds == 27 &&
        persistedLegacy.Capture.Recording.GifWidth == 1440,
        "migra√ß√£o de GIF antigo persiste sem quebrar inicializa√ß√£o");
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
        "nome local para grava√ß√£o MP4");
    var validMp4 = Path.Combine(root, "valid.mp4");
    await WriteValidMp4Async(validMp4);
    ScreenRecordingService.ValidateMp4File(validMp4);
    var invalidMp4 = Path.Combine(root, "invalid.mp4");
    await File.WriteAllBytesAsync(invalidMp4, [0, 1, 2, 3]);
    RequireThrows<InvalidDataException>(
        () => ScreenRecordingService.ValidateMp4File(invalidMp4),
        "MP4 vazio ou sem cont√™iner n√£o entra no hist√≥rico");
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
        ["M√©dia"] = (5_000_000, 70),
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
            $"preset MP4 {preset.Name} aplica os valores descﬂŒ<∂âûÀk∫wµÁQÕÂπå°Q•µïM¡Ö∏π…ΩµMïçΩπëÃ†Ã§§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°¡…ïÕï—IïÕ’±–π¡ÃÄÙÙÅô¡ÕA…ïÕï–πYÖ±’î∞(ÄÄÄÄÄÄÄÄÄÄÄÄêâ¡•¡ï±•πîÅµÖπ”
•¥Å¡…ïÕï–ÅëîÅÌô¡ÕA…ïÕï–πYÖ±’ïÙÅALà§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°çΩ’π—ï»π±Ö¡ÕïêÄ¥Åù•ôM—Ω¡¡ïë–ÄÅQ•µïM¡Ö∏π…Ωµ5•±±•ÕïçΩπëÃ†Ã¿§∞(ÄÄÄÄÄÄÄÄÄÄÄÄêâçΩπ—ÖëΩ»Å%Å¡Ö…ÑÅÖºÅô•πÖ±•ÈÖ»ÅÑÅÌô¡ÕA…ïÕï–πYÖ±’ïÙÅALà§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°•ôIïçΩ…ë•πùMï…Ÿ•çîπE’ï’ïÖ¡Öç•—‰ÄÙÙÄ»∞(ÄÄÄÄÄÄÄÄÄÄÄÄêâô•±ÑÅ%Å±•µ•—ÖëÑÅπºÅ¡…ïÕï–ÅëîÅÌô¡ÕA…ïÕï–πYÖ±’ïÙÅALà§Ï(ÄÄÄÅÙ((ÄÄÄÅIï≈’•…î†(ÄÄÄÄÄÄÄÅ¡¡AÖ—°ÃπÖ—Ö•…ïç—Ω…‰π≈’Ö±Ã†(ÄÄÄÄÄÄÄÄÄÄÄÅAÖ—†πΩµâ•πî†(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπŸ•…Ωπµïπ–πï—Ω±ëï…AÖ—†°πŸ•…Ωπµïπ–πM¡ïç•Ö±Ω±ëï»π1ΩçÖ±¡¡±•çÖ—•ΩπÖ—Ñ§∞(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄâM±ÖÕ°ïÕ¨à§∞(ÄÄÄÄÄÄÄÄÄÄÄÅM—…•πùΩµ¡Ö…•ÕΩ∏π=…ë•πÖ±%ùπΩ…ïÖÕî§Äòò(ÄÄÄÄÄÄÄÄÖ¡¡AÖ—°ÃπÖ—Ö•…ïç—Ω…‰πM—Ö…—Õ]•—†°¡¡AÖ—°Ãπ	ÖÕï•…ïç—Ω…‰Ä¨ÅAÖ—†π•…ïç—Ω…ÂMï¡Ö…Ö—Ω…°Ö»∞(ÄÄÄÄÄÄÄÄÄÄÄÅM—…•πùΩµ¡Ö…•ÕΩ∏π=…ë•πÖ±%ùπΩ…ïÖÕî§∞(ÄÄÄÄÄÄÄÄâëÖëΩÃÅ¡ï…µÖπïπ—ïÃÅô•çÖ¥ÅôΩ…ÑÅëÑÅ¡ÖÕ—ÑÅëºÅï·ïç’”ÖŸï∞Åï¥Äï1ΩçÖ±¡¡Ö—ÑïqqM±ÖÕ°ïÕ¨à§Ï((ÄÄÄÅ’Õ•πúÄ°ŸÖ»Åô…ÖµîƒÄÙÅπï‹ÅMÂÕ—ï¥π…Ö›•πúπ	•—µÖ¿†–∞Ä–§§(ÄÄÄÅ’Õ•πúÄ°ŸÖ»Åô…Öµî»ÄÙÅπï‹ÅMÂÕ—ï¥π…Ö›•πúπ	•—µÖ¿†–∞Ä–§§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅô…ÖµîƒπMï—A•·ï∞†¿∞Ä¿∞ÅMÂÕ—ï¥π…Ö›•πúπΩ±Ω»πIïê§Ï(ÄÄÄÄÄÄÄÅô…Öµî»πMï—A•·ï∞†¿∞Ä¿∞ÅMÂÕ—ï¥π…Ö›•πúπΩ±Ω»π	±’î§Ï(ÄÄÄÄÄÄÄÅ’Õ•πúÅŸÖ»Åù•ôIïçΩ…ë•πúÄÙÅπï‹Å•ôIïçΩ…ë•πùIïÕ’±–†(ÄÄÄÄÄÄÄÄÄÄÄÅmô…Öµîƒπ±Ωπî†§ÅÖÃÅMÂÕ—ï¥π…Ö›•πúπ	•—µÖ¿Ä¸¸Å—°…Ω‹Åπï‹Å%πŸÖ±•ë=¡ï…Ö—•Ωπ·çï¡—•Ω∏†§∞(ÄÄÄÄÄÄÄÄÄÄÄÄÅô…Öµî»π±Ωπî†§ÅÖÃÅMÂÕ—ï¥π…Ö›•πúπ	•—µÖ¿Ä¸¸Å—°…Ω‹Åπï‹Å%πŸÖ±•ë=¡ï…Ö—•Ωπ·çï¡—•Ω∏†•t∞(ÄÄÄÄÄÄÄÄÄÄÄÄƒ¿∞(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅMÂÕ—ï¥π…Ö›•πúπIïç—Öπù±î†¿∞Ä¿∞Ä–∞Ä–§§Ï(ÄÄÄÄÄÄÄÅŸÖ»Åù•ôAÖ—†ÄÙÅπï‹Å•ôIïçΩ…ë•πùMï…Ÿ•çî†§πMÖŸî†(ÄÄÄÄÄÄÄÄÄÄÄÅù•ôIïçΩ…ë•πú∞(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïMï——•πùÃ(ÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ=’—¡’—•…ïç—Ω…ÂQïµ¡±Ö—îÄÙÅ…ΩΩ–∞(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ•±ï9ÖµïQïµ¡±Ö—îÄÙÄâÖπ•µÖ—ïêà(ÄÄÄÄÄÄÄÄÄÄÄÅÙ∞(ÄÄÄÄÄÄÄÄÄÄÄÄâù•òà§Ï(ÄÄÄÄÄÄÄÅŸÖ»Åù•ô	Â—ïÃÄÙÅÖ›Ö•–Å•±îπIïÖë±±	Â—ïÕÕÂπå°ù•ôAÖ—†§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î†(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥πQï·–ππçΩë•πúπM%$πï—M—…•πú°ù•ô	Â—ïÃ§πΩπ—Ö•πÃ†(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄâ9QMA»∏¿à∞(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅM—…•πùΩµ¡Ö…•ÕΩ∏π=…ë•πÖ∞§∞(ÄÄÄÄÄÄÄÄÄÄÄÄâ%Å•πç±’§Åï·—ïπœçºÅëîÅ…ï¡ï—ßüçºÅ9QMAà§Ï(ÄÄÄÄÄÄÄÅ’Õ•πúÅŸÖ»Åù•ôM—…ïÖ¥ÄÙÅ•±îπ=¡ïπIïÖê°ù•ôAÖ—†§Ï(ÄÄÄÄÄÄÄÅŸÖ»ÅëïçΩëï»ÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›Ãπ5ïë•Ñπ%µÖù•πúπ•ô	•—µÖ¡ïçΩëï»†(ÄÄÄÄÄÄÄÄÄÄÄÅù•ôM—…ïÖ¥∞(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ5ïë•Ñπ%µÖù•πúπ	•—µÖ¡…ïÖ—ï=¡—•ΩπÃπA…ïÕï…ŸïA•·ï±Ω…µÖ–∞(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ5ïë•Ñπ%µÖù•πúπ	•—µÖ¡Öç°ï=¡—•Ω∏π=π1ΩÖê§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°ëïçΩëï»π…ÖµïÃπΩ’π–ÄÙÙÄ»∞Äâ%Å¡…ïÕï…ŸÑÅ—ΩëΩÃÅΩÃÅ≈’Öë…ΩÃà§Ï(ÄÄÄÅÙ(ÄÄÄÅIï≈’•…î†(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†â—…∞≠M°•ô–≠A…•π—Mç…ïï∏à§∞(ÄÄÄÄÄÄÄÄâÖ—Ö±°ºÅëîÅçÖ¡—’…ÑÅ¡ï±ºÅ—ïç±Öëºà§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†â—…∞≠M°•ô–≠]°ïï±U¿à§∞4(ÄÄÄÄÄÄÄÄâÖ—Ö±°ºÅëîÅçÖ¡—’…ÑÅ¡ï±ÑÅ…ΩëÑÅëºÅµΩ’Õîà§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÄÖ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†â]°ïï±U¿à§∞4(ÄÄÄÄÄÄÄÄâ…ΩëÑÅëºÅµΩ’ÕîÅï·•ùîÅµΩë•ô•çÖëΩ»à§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπΩ…µÖ—-ïÂâΩÖ…ëM°Ω…—ç’–†4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π-ï‰πƒ¿∞4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π5Ωë•ô•ï…-ïÂÃπ9Ωπî§ÄÙÙÄâƒ¿à∞4(ÄÄÄÄÄÄÄÄâù…ÖŸÑÅ—ïç±ÑÅëîÅô’ªüçºÅÕï¥Åë•ù•—áüçºÅµÖπ’Ö∞à§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπΩ…µÖ—-ïÂâΩÖ…ëM°Ω…—ç’–†4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π-ï‰πMπÖ¡Õ°Ω–∞4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π5Ωë•ô•ï…-ïÂÃπ9Ωπî§ÄÙÙÄâA…•π—Mç…ïï∏àÄòò4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†âA…•π–à§Äòò4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†âA…—Måà§Äòò4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†âMπÖ¡Õ°Ω–à§∞4(ÄÄÄÄÄÄÄÄâù…ÖŸÑÅA…•π–ÅMç…ïï∏ÅπºÅ¡…ïÕÕ•ΩπÖµïπ—ºÅΩ‘ÅπÑÅ±•âï…áüçºà§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπΩ…µÖ—]°ïï±M°Ω…—ç’–†4(ÄÄÄÄÄÄÄÄÄÄÄÄƒ»¿∞4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π5Ωë•ô•ï…-ïÂÃπΩπ—…Ω∞Å4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π5Ωë•ô•ï…-ïÂÃπM°•ô–§ÄÙÙ4(ÄÄÄÄÄÄÄÄÄÄÄÄâ—…∞≠M°•ô–≠]°ïï±U¿à∞4(ÄÄÄÄÄÄÄÄâù…ÖŸÑÅçΩµâ•πáüçºÅçΩ¥Å…ΩëÑÅëºÅµΩ’Õîà§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπΩ…µÖ—5Ω’ÕïM°Ω…—ç’–†4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π5Ω’Õï	’——Ω∏πa	’——Ω∏ƒ∞4(ÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π]•πëΩ›Ãπ%π¡’–π5Ωë•ô•ï…-ïÂÃπ±–§ÄÙÙ4(ÄÄÄÄÄÄÄÄÄÄÄÄâ±–≠5Ω’Õï`ƒà∞4(ÄÄÄÄÄÄÄÄâù…ÖŸÑÅâΩ”çºÅ±Ö—ï…Ö∞ÅëºÅµΩ’Õîà§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†â5Ω’Õï`»à§Äòò4(ÄÄÄÄÄÄÄÅ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†â—…∞≠5Ω’Õï5•ëë±îà§∞4(ÄÄÄÄÄÄÄÄââΩ”’ïÃÅëºÅµΩ’ÕîÅœçºÅÖ—Ö±°ΩÃÅ€Ö±•ëΩÃà§Ï4(ÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÄÖ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†â5Ω’Õï1ïô–à§Äòò4(ÄÄÄÄÄÄÄÄÖ±ΩâÖ±Ö¡—’…ïM°Ω…—ç’—Mï…Ÿ•çîπ%ÕYÖ±•ê†â5Ω’ÕïI•ù°–à§∞4(ÄÄÄÄÄÄÄÄâç±•≈’ïÃÅïÕÕïπç•Ö•ÃÅëºÅµΩ’ÕîÅ¡ï…µÖπïçï¥Å±•Ÿ…ïÃà§Ï4(4(ÄÄÄÅ’Õ•πúÄ°ŸÖ»ÅÕΩ’…çîÄÙÅπï‹ÅMÂÕ—ï¥π…Ö›•πúπ	•—µÖ¿†ƒ»¿∞Ä‰¿§§4(ÄÄÄÅÏ4(ÄÄÄÄÄÄÄÅ’Õ•πúÄ°ŸÖ»Åù…Ö¡°•çÃÄÙÅMÂÕ—ï¥π…Ö›•πúπ…Ö¡°•çÃπ…Ωµ%µÖùî°ÕΩ’…çî§§4(ÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÅù…Ö¡°•çÃπ±ïÖ»°MÂÕ—ï¥π…Ö›•πúπΩ±Ω»π]°•—î§Ï4(ÄÄÄÄÄÄÄÅÙ4(4(ÄÄÄÄÄÄÄÅŸÖ»ÅÖππΩ—Ö—•ΩπMçïπÖ…•ΩÃÄÙÅπï›mt4(ÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïππΩ—Ö—•Ω∏4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ-•πêÄÙÅÖ¡—’…ïππΩ—Ö—•Ωπ-•πêπ……Ω‹∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅM—Ö…–ÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†ƒ¿∞Äƒ¿§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπêÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†‰¿∞Äÿ¿§4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ∞4(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïππΩ—Ö—•Ω∏4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ-•πêÄÙÅÖ¡—’…ïππΩ—Ö—•Ωπ-•πêπ!•ù°±•ù°—ï»∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅM—Ö…–ÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†‡∞Ä–‘§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπêÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†ƒ¿¿∞Ä–‘§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ…ùàÄÙÅMÂÕ—ï¥π…Ö›•πúπΩ±Ω»πΩ±êπQΩ…ùà†§4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ∞4(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïππΩ—Ö—•Ω∏4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ-•πêÄÙÅÖ¡—’…ïππΩ—Ö—•Ωπ-•πêπIïç—Öπù±î∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅM—Ö…–ÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†ƒ‘∞Äƒ‘§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπêÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†‡¿∞Äÿ‘§4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ∞4(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïππΩ—Ö—•Ω∏4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ-•πêÄÙÅÖ¡—’…ïππΩ—Ö—•Ωπ-•πêπ±±•¡Õî∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅM—Ö…–ÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†»¿∞Äƒ‘§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπêÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†‡‘∞Ä‹¿§4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ∞4(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïππΩ—Ö—•Ω∏4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ-•πêÄÙÅÖ¡—’…ïππΩ—Ö—•Ωπ-•πêπAïπç•∞∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅAΩ•π—ÃÄÙ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅl4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†‘∞Ä‘§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†–¿∞ÄÃ¿§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†‹‘∞Äƒ»§4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅt4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ∞4(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïππΩ—Ö—•Ω∏4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ-•πêÄÙÅÖ¡—’…ïππΩ—Ö—•Ωπ-•πêπQï·–∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅM—Ö…–ÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†ƒ¿∞Ä»¿§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅQï·–ÄÙÄâQïÕ—îà4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ∞4(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïππΩ—Ö—•Ω∏4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ-•πêÄÙÅÖ¡—’…ïππΩ—Ö—•Ωπ-•πêπ9’µâï»∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅM—Ö…–ÄÙÅπï‹ÅMÂÕ—ï¥π]•πëΩ›ÃπAΩ•π–†‘‘∞Ä–»§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅQï·–ÄÙÄàƒà4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ4(ÄÄÄÄÄÄÄÅÙÏ4(4(ÄÄÄÄÄÄÄÅôΩ…ïÖç†Ä°ŸÖ»ÅÖππΩ—Ö—•Ω∏Å•∏ÅÖππΩ—Ö—•ΩπMçïπÖ…•ΩÃ§(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅ’Õ•πúÅŸÖ»Å…ïπëï…ïëÖ¡—’…îÄÙÅÖ¡—’…ïππΩ—Ö—•ΩπIïπëï…ï»πIïπëï»†4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÕΩ’…çî∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅmÖππΩ—Ö—•Ωπt∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄƒ»¿∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄ‰¿§Ï4(ÄÄÄÄÄÄÄÄÄÄÄÅIï≈’•…î†4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ!ÖÕ°ÖπùïëA•·ï∞°…ïπëï…ïëÖ¡—’…î§∞4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄêâ…ïπëï…•ÈÑÅôï……Öµïπ—ÑÅÌÖππΩ—Ö—•Ω∏π-•πëÙà§Ï(ÄÄÄÄÄÄÄÅÙ(ÄÄÄÅÙ((ÄÄÄÅŸÖ»ÅπΩÖ±±âÖç≠Öç—Ω…‰ÄÙÅπï‹ÅÖ≠ïIïçΩ…ëï…	Öç≠ïπëÖç—Ω…‰ÅÏÅΩµ¡±ï—ï=πM—Ω¿ÄÙÅôÖ±ÕîÅÙÏ(ÄÄÄÅ’Õ•πúÄ°ŸÖ»Å±•ôïçÂç±îÄÙÅπï‹ÅMç…ïïπIïçΩ…ë•πùMï…Ÿ•çî†(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπΩÖ±±âÖç≠Öç—Ω…‰∞(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅQ•µïM¡Ö∏π…Ωµ5•±±•ÕïçΩπëÃ†ÿ¿§§§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅŸÖ»Å—ÖÕ¨ÄÙÅ±•ôïçÂç±îπM—Ö…—ÕÂπå†(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅIïçΩ…ë•πùQÖ…ùï–°IïçΩ…ë•πùQÖ…ùï—-•πêπ]•πëΩ‹∞(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅMÂÕ—ï¥π…Ö›•πúπIïç—Öπù±î†¿∞Ä¿∞ÄÃ»¿∞Ä»–¿§∞Åπï‹Å%π—A—»†ƒ§§∞(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅÖ¡—’…ïMï——•πùÃÅÏÅ=’—¡’—•…ïç—Ω…ÂQïµ¡±Ö—îÄÙÅ…ΩΩ–∞Å•±ï9ÖµïQïµ¡±Ö—îÄÙÄâπºµçÖ±±âÖç¨àÅÙ∞(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅIïçΩ…ë•πùMï——•πùÃ†§§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°πΩÖ±±âÖç≠Öç—Ω…‰π	Öç≠ïπêπIïçΩ…ëÖ±±ïêπ]Ö•–°Q•µïM¡Ö∏π…ΩµMïçΩπëÃ†»§§∞(ÄÄÄÄÄÄÄÄÄÄÄÄââÖç≠ïπêÅ¡Ö…ÑÅ—•µïΩ’–ÅÕï¥ÅçÖ±±âÖç¨à§Ï(ÄÄÄÄÄÄÄÅ±•ôïçÂç±îπM—Ω¿†§Ï(ÄÄÄÄÄÄÄÅÖ›Ö•–ÅIï≈’•…ïQ°…Ω›ÕÕÂπåÒQ•µïΩ’—·çï¡—•Ω∏¯††§ÄÙ¯Å—ÖÕ¨∞Äâ—•µïΩ’–Å…ïÖ∞ÅÕï¥ÅçÖ±±âÖç¨à§Ï(ÄÄÄÄÄÄÄÅŸÖ»Åë•Õ¡ΩÕï±Ωç¨ÄÙÅM—Ω¡›Ö—ç†πM—Ö…—9ï‹†§Ï(ÄÄÄÄÄÄÄÅ±•ôïçÂç±îπ•Õ¡ΩÕî†§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°ë•Õ¡ΩÕï±Ωç¨π±Ö¡ÕïêÄÅQ•µïM¡Ö∏π…Ωµ5•±±•ÕïçΩπëÃ†ƒ¿¿§∞(ÄÄÄÄÄÄÄÄÄÄÄÄâ•Õ¡ΩÕîÅÖ√ÕÃÅ—•µïΩ’–ÅªçºÅâ±Ω≈’ï•ÑÅU$à§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°πΩÖ±±âÖç≠Öç—Ω…‰π	Öç≠ïπêπ•Õ¡ΩÕïÖ±±ÃÄÙÙÄ¿∞(ÄÄÄÄÄÄÄÄÄÄÄÄâ—•µïΩ’–ÅÕï¥ÅçÖ±±âÖç¨ÅªçºÅçΩπçΩ……îÅ•Õ¡ΩÕîÅçΩ¥ÅèÕë•ùºÅπÖ—•Ÿºà§Ï(ÄÄÄÄÄÄÄÅIï≈’•…î°±•ôïçÂç±îπM—Ö—îÄÙÙÅMç…ïïπIïçΩ…ë•πùM—Ö—îπÖ•±ïê∞(ÄÄÄÄÄÄÄÄÄÄÄÄâ—•µïΩ’–ÅÕï¥ÅçÖ±±âÖç¨Åïπçï……ÑÅïÕ—ÖëºÅ•πÖ±•ÈÖπëºà§Ï(ÄÄÄÅÙ)Ù)ô•πÖ±±‰4)Ï4(ÄÄÄÅ•òÄ°•…ïç—Ω…‰π·•Õ—Ã°…ΩΩ–§§4(ÄÄÄÅÏ4(ÄÄÄÄÄÄÄÅ•…ïç—Ω…‰πï±ï—î°…ΩΩ–∞Å—…’î§Ï4(ÄÄÄÅÙ4)Ù4(4)ΩπÕΩ±îπ]…•—ï1•πî†âM±ÖÕ°Qï·–ÅÕµΩ≠îÅ—ïÕ—ÃËÅ=,à§Ï4)…ï—’…∏Ï4(4)Õ—Ö—•åÅâΩΩ∞Å!ÖÕ°ÖπùïëA•·ï∞°MÂÕ—ï¥π…Ö›•πúπ	•—µÖ¿Åâ•—µÖ¿§4)Ï4(ÄÄÄÅôΩ»Ä°ŸÖ»Å‰ÄÙÄ¿ÏÅ‰ÄÅâ•—µÖ¿π!ï•ù°–ÏÅ‰Ä¨ÙÄ»§4(ÄÄÄÅÏ4(ÄÄÄÄÄÄÄÅôΩ»Ä°ŸÖ»Å‡ÄÙÄ¿ÏÅ‡ÄÅâ•—µÖ¿π]•ë—†ÏÅ‡Ä¨ÙÄ»§4(ÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÅ•òÄ°â•—µÖ¿πï—A•·ï∞°‡∞Å‰§πQΩ…ùà†§ÄÑÙ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅMÂÕ—ï¥π…Ö›•πúπΩ±Ω»π]°•—îπQΩ…ùà†§§4(ÄÄÄÄÄÄÄÄÄÄÄÅÏ4(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ…ï—’…∏Å—…’îÏ4(ÄÄÄÄÄÄÄÄÄÄÄÅÙ4(ÄÄÄÄÄÄÄÅÙ4(ÄÄÄÅÙ4(ÄÄÄÅ…ï—’…∏ÅôÖ±ÕîÏ4)Ù4(4)Õ—Ö—•åÅŸΩ•êÅIï≈’•…î°âΩΩ∞ÅçΩπë•—•Ω∏∞ÅÕ—…•πúÅÕçïπÖ…•º§)Ï4(ÄÄÄÅ•òÄ†ÖçΩπë•—•Ω∏§4(ÄÄÄÅÏ4(ÄÄÄÄÄÄÄÅ—°…Ω‹Åπï‹Å%πŸÖ±•ë=¡ï…Ö—•Ωπ·çï¡—•Ω∏†êâÖ±°ÑÅπºÅçïªÖ…•ºËÅÌÕçïπÖ…•ΩÙà§Ï4(ÄÄÄÅÙ4)Ù()Õ—Ö—•åÅŸΩ•êÅIï≈’•…ïQ°…Ω›ÃÒQ·çï¡—•Ω∏¯°ç—•Ω∏ÅÖç—•Ω∏∞ÅÕ—…•πúÅÕçïπÖ…•º§(ÄÄÄÅ›°ï…îÅQ·çï¡—•Ω∏ÄËÅ·çï¡—•Ω∏)Ï(ÄÄÄÅ—…‰(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅÖç—•Ω∏†§Ï(ÄÄÄÅÙ(ÄÄÄÅçÖ—ç†Ä°Q·çï¡—•Ω∏§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ…ï—’…∏Ï(ÄÄÄÅÙ((ÄÄÄÅ—°…Ω‹Åπï‹Å%πŸÖ±•ë=¡ï…Ö—•Ωπ·çï¡—•Ω∏†êâÖ±°ÑÅπºÅÕµΩ≠îÅ—ïÕ–ËÅÌÕçïπÖ…•ΩÙà§Ï)Ù()Õ—Ö—•åÅÖÕÂπåÅQÖÕ¨Å]Ö•—Uπ—•±ÕÂπå°’πåÒâΩΩ∞¯ÅçΩπë•—•Ω∏∞ÅÕ—…•πúÅÕçïπÖ…•º§)Ï(ÄÄÄÅŸÖ»Å—•µïΩ’–ÄÙÅM—Ω¡›Ö—ç†πM—Ö…—9ï‹†§Ï(ÄÄÄÅ›°•±îÄ†ÖçΩπë•—•Ω∏†§§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ•òÄ°—•µïΩ’–π±Ö¡ÕïêÄ¯ÅQ•µïM¡Ö∏π…ΩµMïçΩπëÃ†»§§(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅ—°…Ω‹Åπï‹Å%πŸÖ±•ë=¡ï…Ö—•Ωπ·çï¡—•Ω∏†êâQ•µïΩ’–ÅπºÅçïªÖ…•ºËÅÌÕçïπÖ…•ΩÙà§Ï(ÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÅÖ›Ö•–ÅQÖÕ¨πï±Ö‰†ƒ¿§Ï(ÄÄÄÅÙ)Ù()Õ—Ö—•åÅÖÕÂπåÅQÖÕ¨ÅIï≈’•…ïQ°…Ω›ÕÕÂπåÒQ·çï¡—•Ω∏¯°’πåÒQÖÕ¨¯ÅÖç—•Ω∏∞ÅÕ—…•πúÅÕçïπÖ…•º§(ÄÄÄÅ›°ï…îÅQ·çï¡—•Ω∏ÄËÅ·çï¡—•Ω∏)Ï(ÄÄÄÅ—…‰(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅÖ›Ö•–ÅÖç—•Ω∏†§Ï(ÄÄÄÅÙ(ÄÄÄÅçÖ—ç†Ä°Q·çï¡—•Ω∏§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ…ï—’…∏Ï(ÄÄÄÅÙ(ÄÄÄÅ—°…Ω‹Åπï‹Å%πŸÖ±•ë=¡ï…Ö—•Ωπ·çï¡—•Ω∏†êâÖ±°ÑÅπºÅÕµΩ≠îÅ—ïÕ–ËÅÌÕçïπÖ…•ΩÙà§Ï)Ù()Õ—Ö—•åÅQÖÕ¨Å]…•—ïYÖ±•ë5¿—ÕÂπå°Õ—…•πúÅ¡Ö—†§ÄÙ¯Å•±îπ]…•—ï±±	Â—ïÕÕÂπå†(ÄÄÄÅ¡Ö—†∞(ÄÄÄÅl(ÄÄÄÄÄÄÄÄ¿∞Ä¿∞Ä¿∞Äƒ»∞Ä°âÂ—î§ùòú∞Ä°âÂ—î§ù–ú∞Ä°âÂ—î§ù‰ú∞Ä°âÂ—î§ù¿ú∞Ä¿∞Ä¿∞Ä¿∞Ä¿∞(ÄÄÄÄÄÄÄÄ¿∞Ä¿∞Ä¿∞Äƒ»∞Ä°âÂ—î§ù¥ú∞Ä°âÂ—î§ùêú∞Ä°âÂ—î§ùÑú∞Ä°âÂ—î§ù–ú∞Äƒ∞Ä»∞ÄÃ∞Ä–∞(ÄÄÄÄÄÄÄÄ¿∞Ä¿∞Ä¿∞Ä‡∞Ä°âÂ—î§ù¥ú∞Ä°âÂ—î§ùºú∞Ä°âÂ—î§ùºú∞Ä°âÂ—î§ùÿú(ÄÄÄÅt§Ï()ÕïÖ±ïêÅç±ÖÕÃÅÖ≠ïIïçΩ…ëï…	Öç≠ïπëÖç—Ω…‰ÄËÅ%Mç…ïïπIïçΩ…ëï…	Öç≠ïπëÖç—Ω…‰)Ï(ÄÄÄÅ¡’â±•åÅÖ≠ïIïçΩ…ëï…	Öç≠ïπêÅ	Öç≠ïπêÅÏÅùï–ÏÅÙÄÙÅπï‹†§Ï(ÄÄÄÅ¡’â±•åÅâΩΩ∞ÅMïπë’¡±•çÖ—ïÖ±±âÖç≠Ã(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅùï–ÄÙ¯Å	Öç≠ïπêπMïπë’¡±•çÖ—ïÖ±±âÖç≠ÃÏ(ÄÄÄÄÄÄÄÅ•π•–ÄÙ¯Å	Öç≠ïπêπMïπë’¡±•çÖ—ïÖ±±âÖç≠ÃÄÙÅŸÖ±’îÏ(ÄÄÄÅÙ(ÄÄÄÅ¡’â±•åÅâΩΩ∞ÅΩµ¡±ï—ï=πM—Ω¿(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅùï–ÄÙ¯Å	Öç≠ïπêπΩµ¡±ï—ï=πM—Ω¿Ï(ÄÄÄÄÄÄÄÅ•π•–ÄÙ¯Å	Öç≠ïπêπΩµ¡±ï—ï=πM—Ω¿ÄÙÅŸÖ±’îÏ(ÄÄÄÅÙ(ÄÄÄÅ¡’â±•åÅâΩΩ∞ÅÖ•±=πM—Ω¿(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅùï–ÄÙ¯Å	Öç≠ïπêπÖ•±=πM—Ω¿Ï(ÄÄÄÄÄÄÄÅ•π•–ÄÙ¯Å	Öç≠ïπêπÖ•±=πM—Ω¿ÄÙÅŸÖ±’îÏ(ÄÄÄÅÙ(ÄÄÄÅ¡’â±•åÅ•π–ÅÖ±±âÖç≠ï±ÖÂ5Ã(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅùï–ÄÙ¯Å	Öç≠ïπêπÖ±±âÖç≠ï±ÖÂ5ÃÏ(ÄÄÄÄÄÄÄÅ•π•–ÄÙ¯Å	Öç≠ïπêπÖ±±âÖç≠ï±ÖÂ5ÃÄÙÅŸÖ±’îÏ(ÄÄÄÅÙ(ÄÄÄÅ¡’â±•åÅâΩΩ∞Å	±Ωç≠M—Ω¿(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅùï–ÄÙ¯Å	Öç≠ïπêπ	±Ωç≠M—Ω¿Ï(ÄÄÄÄÄÄÄÅ•π•–ÄÙ¯Å	Öç≠ïπêπ	±Ωç≠M—Ω¿ÄÙÅŸÖ±’îÏ(ÄÄÄÅÙ((ÄÄÄÅ¡’â±•åÅ%Mç…ïïπIïçΩ…ëï…	Öç≠ïπêÅ…ïÖ—î°IïçΩ…ëï…=¡—•ΩπÃÅΩ¡—•ΩπÃ§ÄÙ¯Å	Öç≠ïπêÏ)Ù()ÕïÖ±ïêÅç±ÖÕÃÅÖ≠ïIïçΩ…ëï…	Öç≠ïπêÄËÅ%Mç…ïïπIïçΩ…ëï…	Öç≠ïπê)Ï(ÄÄÄÅ¡…•ŸÖ—îÅ•π–Å}Öç—•ŸïÖ±±ÃÏ(ÄÄÄÅ¡…•ŸÖ—îÅ•π–Å}µÖ·•µ’µΩπç’……ïπ—Ö±±ÃÏ(ÄÄÄÅ¡…•ŸÖ—îÅ•π–Å}ë•Õ¡ΩÕïÖ±±ÃÏ(ÄÄÄÅ¡…•ŸÖ—îÅ•π–Å}Õ—Ω¡Ö±±ÃÏ(ÄÄÄÅ¡…•ŸÖ—îÅÕ—…•πúÅ}¡Ö—†ÄÙÅÕ—…•πúπµ¡—‰Ï(ÄÄÄÅ¡…•ŸÖ—îÅ…ïÖëΩπ±‰Å5Öπ’Ö±IïÕï—Ÿïπ—M±•¥Å}Õ—Ω¡Iï±ïÖÕîÄÙÅπï‹°ôÖ±Õî§Ï((ÄÄÄÅ¡’â±•åÅïŸïπ–ÅŸïπ—!Öπë±ï»ÒIïçΩ…ë•πùΩµ¡±ï—ïŸïπ—…ùÃ¯¸ÅΩµ¡±ï—ïêÏ(ÄÄÄÅ¡’â±•åÅïŸïπ–ÅŸïπ—!Öπë±ï»ÒIïçΩ…ë•πùÖ•±ïëŸïπ—…ùÃ¯¸ÅÖ•±ïêÏ(ÄÄÄÅ¡’â±•åÅïŸïπ–ÅŸïπ—!Öπë±ï»ÒIïçΩ…ë•πùM—Ö—’ÕŸïπ—…ùÃ¯¸ÅM—Ö—’Õ°ÖπùïêÏ(ÄÄÄÅ¡’â±•åÅ5Öπ’Ö±IïÕï—Ÿïπ—M±•¥ÅIïçΩ…ëÖ±±ïêÅÏÅùï–ÏÅÙÄÙÅπï‹°ôÖ±Õî§Ï(ÄÄÄÅ¡’â±•åÅ5Öπ’Ö±IïÕï—Ÿïπ—M±•¥ÅM—Ω¡π—ï…ïêÅÏÅùï–ÏÅÙÄÙÅπï‹°ôÖ±Õî§Ï(ÄÄÄÅ¡’â±•åÅâΩΩ∞ÅMïπë’¡±•çÖ—ïÖ±±âÖç≠ÃÅÏÅùï–ÏÅÕï–ÏÅÙ(ÄÄÄÅ¡’â±•åÅâΩΩ∞ÅΩµ¡±ï—ï=πM—Ω¿ÅÏÅùï–ÏÅÕï–ÏÅÙÄÙÅ—…’îÏ(ÄÄÄÅ¡’â±•åÅâΩΩ∞ÅÖ•±=πM—Ω¿ÅÏÅùï–ÏÅÕï–ÏÅÙ(ÄÄÄÅ¡’â±•åÅ•π–ÅÖ±±âÖç≠ï±ÖÂ5ÃÅÏÅùï–ÏÅÕï–ÏÅÙ(ÄÄÄÅ¡’â±•åÅâΩΩ∞Å	±Ωç≠M—Ω¿ÅÏÅùï–ÏÅÕï–ÏÅÙ(ÄÄÄÅ¡’â±•åÅ•π–Å•Õ¡ΩÕïÖ±±ÃÄÙ¯ÅYΩ±Ö—•±îπIïÖê°…ïòÅ}ë•Õ¡ΩÕïÖ±±Ã§Ï(ÄÄÄÅ¡’â±•åÅ•π–ÅM—Ω¡Ö±±ÃÄÙ¯ÅYΩ±Ö—•±îπIïÖê°…ïòÅ}Õ—Ω¡Ö±±Ã§Ï(ÄÄÄÅ¡’â±•åÅ•π–Å5Ö·•µ’µΩπç’……ïπ—Ö±±ÃÄÙ¯ÅYΩ±Ö—•±îπIïÖê°…ïòÅ}µÖ·•µ’µΩπç’……ïπ—Ö±±Ã§Ï((ÄÄÄÅ¡’â±•åÅŸΩ•êÅIïçΩ…ê°Õ—…•πúÅ¡Ö—†§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ9Ö—•ŸïÖ±∞††§ÄÙ¯(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅ}¡Ö—†ÄÙÅ¡Ö—†Ï(ÄÄÄÄÄÄÄÄÄÄÄÅIïçΩ…ëÖ±±ïêπMï–†§Ï(ÄÄÄÄÄÄÄÄÄÄÄÅM—Ö—’Õ°Öπùïê¸π%πŸΩ≠î°—°•Ã∞Åπï‹ÅIïçΩ…ë•πùM—Ö—’ÕŸïπ—…ùÃ°IïçΩ…ëï…M—Ö—’ÃπIïçΩ…ë•πú§§Ï(ÄÄÄÄÄÄÄÅÙ§Ï(ÄÄÄÅÙ((ÄÄÄÅ¡’â±•åÅŸΩ•êÅAÖ’Õî†§ÄÙ¯Å9Ö—•ŸïÖ±∞††§ÄÙ¯(ÄÄÄÄÄÄÄÅM—Ö—’Õ°Öπùïê¸π%πŸΩ≠î°—°•Ã∞Åπï‹ÅIïçΩ…ë•πùM—Ö—’ÕŸïπ—…ùÃ°IïçΩ…ëï…M—Ö—’ÃπAÖ’Õïê§§§Ï((ÄÄÄÅ¡’â±•åÅŸΩ•êÅIïÕ’µî†§ÄÙ¯Å9Ö—•ŸïÖ±∞††§ÄÙ¯(ÄÄÄÄÄÄÄÅM—Ö—’Õ°Öπùïê¸π%πŸΩ≠î°—°•Ã∞Åπï‹ÅIïçΩ…ë•πùM—Ö—’ÕŸïπ—…ùÃ°IïçΩ…ëï…M—Ö—’ÃπIïçΩ…ë•πú§§§Ï((ÄÄÄÅ¡’â±•åÅŸΩ•êÅM—Ω¿†§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ9Ö—•ŸïÖ±∞††§ÄÙ¯(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅ%π—ï…±Ωç≠ïêπ%πç…ïµïπ–°…ïòÅ}Õ—Ω¡Ö±±Ã§Ï(ÄÄÄÄÄÄÄÄÄÄÄÅM—Ω¡π—ï…ïêπMï–†§Ï(ÄÄÄÄÄÄÄÄÄÄÄÅ•òÄ°	±Ωç≠M—Ω¿§(ÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ}Õ—Ω¡Iï±ïÖÕîπ]Ö•–°Q•µïM¡Ö∏π…ΩµMïçΩπëÃ†Ã§§Ï(ÄÄÄÄÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÄÄÄÄÅ•òÄ°Ö•±=πM—Ω¿§(ÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÖ•±ïê¸π%πŸΩ≠î°—°•Ã∞Åπï‹ÅIïçΩ…ë•πùÖ•±ïëŸïπ—…ùÃ†âôÖ±°ÑÅÕ•µ’±ÖëÑà∞Å}¡Ö—†§§Ï(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ…ï—’…∏Ï(ÄÄÄÄÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÄÄÄÄÅ•òÄ°Ωµ¡±ï—ï=πM—Ω¿§(ÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ•òÄ°Ö±±âÖç≠ï±ÖÂ5ÃÄ¯Ä¿§(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ|ÄÙÅQÖÕ¨πI’∏°ÖÕÂπåÄ†§ÄÙ¯(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÖ›Ö•–ÅQÖÕ¨πï±Ö‰°Ö±±âÖç≠ï±ÖÂ5Ã§Ï(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅΩµ¡±ï—ï1Ö—ï»†§Ï(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÙ§Ï(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅï±Õî(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅΩµ¡±ï—ï1Ö—ï»†§Ï(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÅÙ§Ï(ÄÄÄÅÙ((ÄÄÄÅ¡’â±•åÅŸΩ•êÅIï±ïÖÕïM—Ω¿†§ÄÙ¯Å}Õ—Ω¡Iï±ïÖÕîπMï–†§Ï((ÄÄÄÅ¡’â±•åÅŸΩ•êÅΩµ¡±ï—ï1Ö—ï»†§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ]…•—ïYÖ±•ë5¿–°}¡Ö—†§Ï(ÄÄÄÄÄÄÄÅΩµ¡±ï—ïê¸π%πŸΩ≠î†(ÄÄÄÄÄÄÄÄÄÄÄÅ—°•Ã∞(ÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅIïçΩ…ë•πùΩµ¡±ï—ïŸïπ—…ùÃ°}¡Ö—†∞Åπï‹Å1•Õ–Ò…ÖµïÖ—Ñ¯†§§§Ï(ÄÄÄÄÄÄÄÅ•òÄ°Mïπë’¡±•çÖ—ïÖ±±âÖç≠Ã§(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅÖ•±ïê¸π%πŸΩ≠î†(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ—°•Ã∞(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅπï‹ÅIïçΩ…ë•πùÖ•±ïëŸïπ—…ùÃ†âçÖ±±âÖç¨Å—Ö…ë•ºà∞Å}¡Ö—†§§Ï(ÄÄÄÄÄÄÄÅÙ(ÄÄÄÅÙ((ÄÄÄÅ¡’â±•åÅŸΩ•êÅ•Õ¡ΩÕî†§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ9Ö—•ŸïÖ±∞††§ÄÙ¯Å%π—ï…±Ωç≠ïêπ%πç…ïµïπ–°…ïòÅ}ë•Õ¡ΩÕïÖ±±Ã§§Ï(ÄÄÄÅÙ((ÄÄÄÅ¡…•ŸÖ—îÅŸΩ•êÅ9Ö—•ŸïÖ±∞°ç—•Ω∏ÅÖç—•Ω∏§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅŸÖ»ÅçΩπç’……ïπ–ÄÙÅ%π—ï…±Ωç≠ïêπ%πç…ïµïπ–°…ïòÅ}Öç—•ŸïÖ±±Ã§Ï(ÄÄÄÄÄÄÄÅU¡ëÖ—ï5Ö·•µ’¥°çΩπç’……ïπ–§Ï(ÄÄÄÄÄÄÄÅ—…‰(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅQ°…ïÖêπM±ïï¿†ƒ‘§Ï(ÄÄÄÄÄÄÄÄÄÄÄÅÖç—•Ω∏†§Ï(ÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÅô•πÖ±±‰(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅ%π—ï…±Ωç≠ïêπïç…ïµïπ–°…ïòÅ}Öç—•ŸïÖ±±Ã§Ï(ÄÄÄÄÄÄÄÅÙ(ÄÄÄÅÙ((ÄÄÄÅ¡…•ŸÖ—îÅŸΩ•êÅU¡ëÖ—ï5Ö·•µ’¥°•π–ÅŸÖ±’î§(ÄÄÄÅÏ(ÄÄÄÄÄÄÄÅ›°•±îÄ°—…’î§(ÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÅŸÖ»Åç’……ïπ–ÄÙÅYΩ±Ö—•±îπIïÖê°…ïòÅ}µÖ·•µ’µΩπç’……ïπ—Ö±±Ã§Ï(ÄÄÄÄÄÄÄÄÄÄÄÅ•òÄ°ç’……ïπ–Ä¯ÙÅŸÖ±’îÅÒ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ%π—ï…±Ωç≠ïêπΩµ¡Ö…ï·ç°Öπùî°…ïòÅ}µÖ·•µ’µΩπç’……ïπ—Ö±±Ã∞ÅŸÖ±’î∞Åç’……ïπ–§ÄÙÙÅç’……ïπ–§(ÄÄÄÄÄÄÄÄÄÄÄÅÏ(ÄÄÄÄÄÄÄÄÄÄÄÄÄÄÄÅ…ï—’…∏Ï(ÄÄÄÄÄÄÄÄÄÄÄÅÙ(ÄÄÄÄÄÄÄÅÙ(ÄÄÄÅÙ((ÄÄÄÅ¡…•ŸÖ—îÅÕ—Ö—•åÅŸΩ•êÅ]…•—ïYÖ±•ë5¿–°Õ—…•πúÅ¡Ö—†§ÄÙ¯Å•±îπ]…•—ï±±	Â—ïÃ†(ÄÄÄÄÄÄÄÅ¡Ö—†∞(ÄÄÄÄÄÄÄÅl(ÄÄÄÄÄÄÄÄÄÄÄÄ¿∞Ä¿∞Ä¿∞Äƒ»∞Ä°âÂ—î§ùòú∞Ä°âÂ—î§ù–ú∞Ä°âÂ—î§ù‰ú∞Ä°âÂ—î§ù¿ú∞Ä¿∞Ä¿∞Ä¿∞Ä¿∞(ÄÄÄÄÄÄÄÄÄÄÄÄ¿∞Ä¿∞Ä¿∞Äƒ»∞Ä°âÂ—î§ù¥ú∞Ä°âÂ—î§ùêú∞Ä°âÂ—î§ùÑú∞Ä°âÂ—î§ù–ú∞Äƒ∞Ä»∞ÄÃ∞Ä–∞(ÄÄÄÄÄÄÄÄÄÄÄÄ¿∞Ä¿∞Ä¿∞Ä‡∞Ä°âÂ—î§ù¥ú∞Ä°âÂ—î§ùºú∞Ä°âÂ—î§ùºú∞Ä°âÂ—î§ùÿú(ÄÄÄÄÄÄÄÅt§Ï)Ù(