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
        var paletteSource = GifRecordingSer×O8îÚ$z{-®éÜj×’b`¢v—Bf–ÆRå&VDÆÅFW‡D7–æ2‡WFFW$FF6VçF–æVÂ’ÓÒ&†—7L;7&–6ò–çF7Fò"À¢'6÷FRl:Æ–Fò:’&W&FòFVçG&òFR6Æ6„FW6´FF6VÒÇFW&"†—7L;7&–6ò"“° ¢f"&D6†V6·7VÒÒ6†V6·7VÔ'—FW2åFô'&’‚“°¢&D6†V6·7VÕ³ÒÒ&D6†V6·7VÕ³ÒÓÒ†'—FR’srò†'—FR’sr¢†'—FR’ss°¢f"–çfÆ–D6†V6·7VÕ6W'f–6RÒæWr÷'F&ÆUWFFU6W'f–6R€¢æWr‡GG6Æ–VçB†æWrf¶U6¶vT‡GG†æFÆW"‡6¶vT'—FW2Â&D6†V6·7VÒ’’À¢÷'F&ÆTW†V7WF&ÆRÀ¢7W'&VçE&ö6W74–C¢“°¢v—B&WV—&UF‡&÷w47–æ3Ä–çfÆ–DFFW†6WF–öãâ€¢‚’Óâ–çfÆ–D6†V6·7VÕ6W'f–6Rå&W&T7–æ2‡6¶vU&VÆV6R’À¢&6†V6·7VÒ–çl:Æ–Fò–×VFRGVÆ—¦:|:6ò"“° ¢f"–æ6ö×ÆWFU&VÆV6RÒ7&VFU6¶vU&VÆV6R€¢WFFW%fW'6–öâÂ6¶vTæÖRÂ6¶vT'—FW2äÆVæwF‚²Â6†V6·7VÔ'—FW2äÆVæwF‚“°¢f"–æ6ö×ÆWFU6W'f–6RÒæWr÷'F&ÆUWFFU6W'f–6R€¢æWr‡GG6Æ–VçB†æWrf¶U6¶vT‡GG†æFÆW"‡6¶vT'—FW2Â6†V6·7VÔ'—FW2’’À¢÷'F&ÆTW†V7WF&ÆRÀ¢7W'&VçE&ö6W74–C¢“°¢v—B&WV—&UF‡&÷w47–æ3ÄVæDöe7G&VÔW†6WF–öãâ€¢‚’Óâ–æ6ö×ÆWFU6W'f–6Rå&W&T7–æ2†–æ6ö×ÆWFU&VÆV6R’À¢&F÷væÆöB–æ6ö×ÆWFò–×VFRGVÆ—¦:|:6ò"“° ¢f"6öæ7W'&VçD†æFÆW"ÒæWrf¶U6¶vT‡GG†æFÆW"‡6¶vT'—FW2Â6†V6·7VÔ'—FW2¢°¢FVÆ’ÒF–ÖU7âäg&öÔÖ–ÆÆ—6V6öæG2ƒ#S¢Ó°¢f"6öæ7W'&VçE6¶vU6W'f–6RÒæWr÷'F&ÆUWFFU6W'f–6R€¢æWr‡GG6Æ–VçB†6öæ7W'&VçD†æFÆW"’Â÷'F&ÆTW†V7WF&ÆRÂ7W'&VçE&ö6W74–C¢“°¢W6–ærf"6öæ7W'&VçD6æ6VÆÆF–öâÒæWr6æ6VÆÆF–öåFö¶Vå6÷W&6R‚“°¢f"f—'7E&W&RÒ6öæ7W'&VçE6¶vU6W'f–6Rå&W&T7–æ2€¢6¶vU&VÆV6RÂ6æ6VÆÆF–öåFö¶Vã¢6öæ7W'&VçD6æ6VÆÆF–öâåFö¶Vâ“°¢v—B6öæ7W'&VçD†æFÆW"å7F'FVBåF6²åv—D7–æ2…F–ÖU7âäg&öÕ6V6öæG2ƒ"’“°¢v—B&WV—&UF‡&÷w47–æ3Ä–çfÆ–D÷W&F–öäW†6WF–öãâ€¢‚’Óâ6öæ7W'&VçE6¶vU6W'f–6Rå&W&T7–æ2‡6¶vU&VÆV6R’À¢&Fö—2VF–F÷26–×VÇL:&æV÷2FRGVÆ—¦:|:6ò<:6ò&Æ÷VVF÷2"“°¢6öæ7W'&VçD6æ6VÆÆF–öâä6æ6VÂ‚“°¢v—B&WV—&UF‡&÷w47–æ3Ä÷W&F–öä6æ6VÆVDW†6WF–öãâ€¢‚’Óâf—'7E&W&RÀ¢&fV6†ÖVçFòGW&çFRF÷væÆöB6æ6VÆ&W&:|:6ò"“°¢&WV—&R†v—Bf–ÆRå&VDÆÅFW‡D7–æ2‡WFFW$FF6VçF–æVÂ’ÓÒ&†—7L;7&–6ò–çF7Fò"À¢&6æ6VÆÖVçFòFòF÷væÆöB&W6W'f6Æ6„FW6´FF"“° ¢f"æô6ÆÆ&6´f7F÷'’ÒæWrf¶U&V6÷&FW$&6¶VæDf7F÷'’²6ö×ÆWFTöå7F÷ÒfÇ6RÓ°¢W6–ær‡f"Æ–fV7–6ÆRÒæWr67&VVå&V6÷&F–æu6W'f–6R€¢æô6ÆÆ&6´f7F÷'’À¢F–ÖU7âäg&öÔÖ–ÆÆ—6V6öæG2ƒc’’¢°¢f"F6²ÒÆ–fV7–6ÆRå7F'D7–æ2€¢æWr&V6÷&F–æuF&vWB…&V6÷&F–æuF&vWD¶–æBåv–æF÷rÀ¢æWr7—7FVÒäG&v–ærå&V7FævÆRƒÂÂ3#Â#C’ÂæWr–çEG"ƒ’’À¢æWr6GW&U6WGF–æw2²÷WGWDF—&V7F÷'•FV×ÆFRÒ&ö÷BÂf–ÆTæÖUFV×ÆFRÒ&æòÖ6ÆÆ&6²"ÒÀ¢æWr&V6÷&F–æu6WGF–æw2‚’“°¢&WV—&R†æô6ÆÆ&6´f7F÷'’ä&6¶VæBå&V6÷&D6ÆÆVBåv—B…F–ÖU7âäg&öÕ6V6öæG2ƒ"’’À¢&&6¶VæB&F–ÖV÷WB6VÒ6ÆÆ&6²"“°¢Æ–fV7–6ÆRå7F÷‚“°¢v—B&WV—&UF‡&÷w47–æ3ÅF–ÖV÷WDW†6WF–öãâ‚‚’ÓâF6²Â'F–ÖV÷WB&VÂ6VÒ6ÆÆ&6²"“°¢f"F—7÷6T6Æö6²Ò7F÷vF6‚å7F'DæWr‚“°¢Æ–fV7–6ÆRäF—7÷6R‚“°¢&WV—&R†F—7÷6T6Æö6²äVÆ6VBÂF–ÖU7âäg&öÔÖ–ÆÆ—6V6öæG2ƒ’À¢$F—7÷6R;72F–ÖV÷WBì:6ò&Æ÷VV–T’"“°¢&WV—&R†æô6ÆÆ&6´f7F÷'’ä&6¶VæBäF—7÷6T6ÆÇ2ÓÒÀ¢'F–ÖV÷WB6VÒ6ÆÆ&6²ì:6ò6öæ6÷'&RF—7÷6R6öÒ<;6F–vòæF—fò"“°¢&WV—&R†Æ–fV7–6ÆRå7FFRÓÒ67&VVå&V6÷&F–æu7FFRäf–ÆVBÀ¢'F–ÖV÷WB6VÒ6ÆÆ&6²Væ6W'&W7FFòf–æÆ—¦æFò"“°¢Ğ§Ğ¦f–æÆÇ§°¢–b„F—&V7F÷'’äW†—7G2‡&ö÷B’¢°¢F—&V7F÷'’äFVÆWFR‡&ö÷BÂG'VR“°¢Ğ§Ğ ¤6öç6öÆRåw&—FTÆ–æR‚%6Æ6…FW‡B6Öö¶RFW7G3¢ô²"“°§&WGW&ã° §7FF–2&ööÂ†46†ævVE—†VÂ…7—7FVÒäG&v–ærä&—FÖ&—FÖ§°¢f÷"‡f"’Ò²’Â&—FÖä†V–v‡C²’³Ò"¢°¢f÷"‡f"‚Ò²‚Â&—FÖåv–GFƒ²‚³Ò"¢°¢–b†&—FÖävWE—†VÂ‡‚Â’’åFô&v"‚’Ğ¢7—7FVÒäG&v–ærä6öÆ÷"åv†—FRåFô&v"‚’¢°¢&WGW&âG'VS°¢Ğ¢Ğ¢Ğ¢&WGW&âfÇ6S°§Ğ §7FF–2fö–B&WV—&R†&ööÂ6öæF—F–öâÂ7G&–ær66Væ&–ò§°¢–b‚6öæF—F–öâ¢°¢F‡&÷ræWr–çfÆ–D÷W&F–öäW†6WF–öâ‚B$fÆ†æò6Vì:&–ó¢·66Væ&–÷Ò"“°¢Ğ§Ğ §7FF–2fö–B&WV—&UF‡&÷w3ÅDW†6WF–öãâ„7F–öâ7F–öâÂ7G&–ær66Væ&–ò¢v†W&RDW†6WF–öâ¢W†6WF–öà§°¢G'¢°¢7F–öâ‚“°¢Ğ¢6F6‚…DW†6WF–öâ¢°¢&WGW&ã°¢Ğ ¢F‡&÷ræWr–çfÆ–D÷W&F–öäW†6WF–öâ‚B$fÆ†æò6Öö¶RFW7C¢·66Væ&–÷Ò"“°§Ğ §7FF–27–æ2F6²v—EVçF–Ä7–æ2„gVæ3Æ&ööÃâ6öæF—F–öâÂ7G&–ær66Væ&–ò§°¢f"F–ÖV÷WBÒ7F÷vF6‚å7F'DæWr‚“°¢v†–ÆR‚6öæF—F–öâ‚’¢°¢–b‡F–ÖV÷WBäVÆ6VBâF–ÖU7âäg&öÕ6V6öæG2ƒ"’¢°¢F‡&÷ræWr–çfÆ–D÷W&F–öäW†6WF–öâ‚B%F–ÖV÷WBæò6Vì:&–ó¢·66Væ&–÷Ò"“°¢Ğ¢v—BF6²äFVÆ’ƒ“°¢Ğ§Ğ §7FF–27–æ2F6²&WV—&UF‡&÷w47–æ3ÅDW†6WF–öãâ„gVæ3ÅF6³â7F–öâÂ7G&–ær66Væ&–ò¢v†W&RDW†6WF–öâ¢W†6WF–öà§°¢G'¢°¢v—B7F–öâ‚“°¢Ğ¢6F6‚…DW†6WF–öâ¢°¢&WGW&ã°¢Ğ¢F‡&÷ræWr–çfÆ–D÷W&F–öäW†6WF–öâ‚B$fÆ†æò6Öö¶RFW7C¢·66Væ&–÷Ò"“°§Ğ §7FF–2F6²w&—FUfÆ–D×D7–æ2‡7G&–ærF‚’Óâf–ÆRåw&—FTÆÄ'—FW47–æ2€¢F‚À¢°¢ÂÂÂ"Â†'—FR’vbrÂ†'—FR’wBrÂ†'—FR’w’rÂ†'—FR’wrÂÂÂÂÀ¢ÂÂÂ"Â†'—FR’vÒrÂ†'—FR’vBrÂ†'—FR’vrÂ†'—FR’wBrÂÂ"Â2ÂBÀ¢ÂÂÂ‚Â†'—FR’vÒrÂ†'—FR’vòrÂ†'—FR’vòrÂ†'—FR’wbp¢Ò“° §7FF–27G&–ær&VÆV6T§6öâ€¢7G&–ærfW'6–öâÀ¢&ööÂ–æ6ÇVFU7F&ÆRÒG'VRÀ¢&ööÂ–æ6ÇVFTG&gBÒfÇ6RÀ¢&ööÂ–æ6ÇVFU&W&VÆV6RÒfÇ6R§°¢f"&VÆV6W2ÒæWrÆ—7CÇ7G&–æsâ‚“°¢–b†–æ6ÇVFTG&gB¢°¢&VÆV6W2äFB…&VÆV6TVçG'’‡fW'6–öâÂG&gC¢G'VRÂ&W&VÆV6S¢fÇ6R’“°¢Ğ¢–b†–æ6ÇVFU&W&VÆV6R¢°¢&VÆV6W2äFB…&VÆV6TVçG'’‡fW'6–öâ²"×&2ã"ÂG&gC¢fÇ6RÂ&W&VÆV6S¢G'VR’“°¢Ğ¢–b†–æ6ÇVFU7F&ÆR¢°¢&VÆV6W2äFB…&VÆV6TVçG'’‡fW'6–öâÂG&gC¢fÇ6RÂ&W&VÆV6S¢fÇ6R’“°¢Ğ¢&WGW&âB%··7G&–ærä¦ö–â‚rÂrÂ&VÆV6W2—ÕÒ#°§Ğ §7FF–27G&–ær&VÆV6TVçG'’‡7G&–ærfW'6–öâÂ&ööÂG&gBÂ&ööÂ&W&VÆV6R’ÓâBB"" ¢°¢'FuöæÖR#¢'g··fW'6–öç×Ò"À¢&æÖR#¢%6Æ6„FW6²··fW'6–öç×Ò"À¢&&öG’#¢$æ÷F2··fW'6–öç×Ò"À¢&‡FÖÅ÷W&Â#¢&‡GG3¢òöv—F‡V"æ6öÒöÇV66ÆÆ—&õ6Æ6…FW‡B÷&VÆV6W2÷Fr÷g··fW'6–öç×Ò"À¢'V&Æ—6†VEöB#¢###bÓ‚ÓeC#££¢"À¢&G&gB#¢·¶G&gBåFõ7G&–ær‚’åFôÆ÷vW$–çf&–çB‚—×ÒÀ¢'&W&VÆV6R#¢··&W&VÆV6RåFõ7G&–ær‚’åFôÆ÷vW$–çf&–çB‚—×ÒÀ¢&76WG2#¢°¢°¢&æÖR#¢%6Æ6„FW6²×··fW'6–öç×Ò×÷'F&ÆR×v–â×ƒcBç¦—"À¢&'&÷w6W%öF÷væÆöE÷W&Â#¢&‡GG3¢òöW†×ÆRæ–çfÆ–Bõ6Æ6„FW6²×··fW'6–öç×Ò×÷'F&ÆR×v–â×ƒcBç¦—"À¢'6—¦R#¢#3CP¢ÒÀ¢°¢&æÖR#¢%6Æ6„FW6²×··fW'6–öç×Ò×÷'F&ÆR×v–â×ƒcBç¦—ç6†#Sb"À¢&'&÷w6W%öF÷væÆöE÷W&Â#¢&‡GG3¢òöW†×ÆRæ–çfÆ–Bõ6Æ6„FW6²×··fW'6–öç×Ò×÷'F&ÆR×v–â×ƒcBç¦—ç6†#Sb"À¢'6—¦R#¢#€¢Ğ¢Ğ¢Ğ¢""#° §7FF–2'—FUµÒ7&VFU÷'F&ÆU¦—‡7G&–ærW†V7WF&ÆR§°¢W6–ærf"÷WGWBÒæWrÖVÖ÷'•7G&VÒ‚“°¢W6–ær‡f"&6†—fRÒæWr¦—&6†—fR†÷WGWBÂ¦—&6†—fTÖöFRä7&VFRÂÆVfT÷Vã¢G'VR’¢°¢f"VçG'’Ò&6†—fRä7&VFTVçG'’‚%6Æ6„FW6²æW†R"Â6ö×&W76–öäÆWfVÂäæô6ö×&W76–öâ“°¢W6–ærf"FW7F–æF–öâÒVçG'’ä÷Vâ‚“°¢W6–ærf"6÷W&6RÒf–ÆRä÷Vå&VB†W†V7WF&ÆR“°¢6÷W&6Rä6÷•Fò†FW7F–æF–öâ“°¢Ğ¢&WGW&â÷WGWBåFô'&’‚“°§Ğ §7FF–2&VÆV6T–æfò7&VFU6¶vU&VÆV6R€¢7G&–ærfW'6–öâÀ¢7G&–ær6¶vTæÖRÀ¢Æöær6¶vU6—¦RÀ¢Æöær6†V6·7VÕ6—¦R’ÓâæWr€¢fW'6–öâÀ¢B%6Æ6„FW6²·fW'6–öçÒ"À¢$æ÷F2"À¢B&‡GG3¢òöv—F‡V"æ6öÒöÇV66ÆÆ—&õ6Æ6…FW‡B÷&VÆV6W2÷Fr÷g·fW'6–öçÒ"À¢FFUF–ÖTöfg6WBåWF4æ÷rÀ¢æWr&VÆV6T76WD–æfò€¢6¶vTæÖRÀ¢B&‡GG3¢òöv—F‡V"æ6öÒöÇV66ÆÆ—&õ6Æ6…FW‡B÷&VÆV6W2öF÷væÆöB÷g·fW'6–öçÒ÷·6¶vTæÖWÒ"À¢6¶vU6—¦R’À¢æWr&VÆV6T76WD–æfò€¢6¶vTæÖR²"ç6†#Sb"À¢B&‡GG3¢òöv—F‡V"æ6öÒöÇV66ÆÆ—&õ6Æ6…FW‡B÷&VÆV6W2öF÷væÆöB÷g·fW'6–öçÒ÷·6¶vTæÖWÒç6†#Sb"À¢6†V6·7VÕ6—¦R’“° §6VÆVB6Æ72f¶U6¶vT‡GG†æFÆW"†'—FUµÒ6¶vRÂ'—FUµÒ6†V6·7VÒ’¢‡GGÖW76vT†æFÆW §°¢V&Æ–2F–ÖU7âFVÆ’²vWC²–æ—C²Ğ¢V&Æ–2F6´6ö×ÆWF–öå6÷W&6R7F'FVB²vWC²ÒĞ¢æWr…F6´7&VF–öä÷F–öç2å'Vä6öçF–çVF–öç47–æ6‡&öæ÷W6Ç’“° ¢&÷FV7FVB÷fW'&–FR7–æ2F6³Ä‡GG&W7öç6TÖW76vSâ6VæD7–æ2€¢‡GG&WVW7DÖW76vR&WVW7BÀ¢6æ6VÆÆF–öåFö¶Vâ6æ6VÆÆF–öåFö¶Vâ¢°¢7F'FVBåG'•6WE&W7VÇB‚“°¢–b„FVÆ’âF–ÖU7âå¦W&ò¢°¢v—BF6²äFVÆ’„FVÆ’Â6æ6VÆÆF–öåFö¶Vâ“°¢Ğ¢f"&öG’Ò&WVW7Bå&WVW7EW&“òä'6öÇWFUF‚äVæG5v—F‚‚"ç6†#Sb"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’ÓÒG'VP¢ò6†V6·7VĞ¢¢6¶vS°¢&WGW&âæWr‡GG&W7öç6TÖW76vR…7—7FVÒäæWBä‡GG7FGW46öFRäô²¢°¢6öçFVçBÒæWr'—FT'&”6öçFVçB†&öG’¢Ó°¢Ğ§Ğ §6VÆVB6Æ72f¶UWFFT‡GG†æFÆW"¢‡GGÖW76vT†æFÆW §°¢&—fFR&VFöæÇ’W†6WF–öãòöW†6WF–öã°¢&—fFR&VFöæÇ’F–ÖU7âöFVÆ“°¢&—fFR–çBö6ÆÇ3° ¢V&Æ–2f¶UWFFT‡GG†æFÆW"‡7G&–ær&W7öç6T§6öâ’Óâ&W7öç6T§6öâÒ&W7öç6T§6öã°¢V&Æ–2f¶UWFFT‡GG†æFÆW"„W†6WF–öâW†6WF–öâ’ÓâöW†6WF–öâÒW†6WF–öã°¢V&Æ–2f¶UWFFT‡GG†æFÆW"…F–ÖU7âFVÆ’¢°¢öFVÆ’ÒFVÆ“°¢&W7öç6T§6öâÒ%µÒ#°¢Ğ ¢V&Æ–27G&–ær&W7öç6T§6öâ²vWC²6WC²ÒÒ%µÒ#°¢V&Æ–2–çB6ÆÇ2ÓâföÆF–ÆRå&VB‡&Vbö6ÆÇ2“° ¢&÷FV7FVB÷fW'&–FR7–æ2F6³Ä‡GG&W7öç6TÖW76vSâ6VæD7–æ2€¢‡GG&WVW7DÖW76vR&WVW7BÀ¢6æ6VÆÆF–öåFö¶Vâ6æ6VÆÆF–öåFö¶Vâ¢°¢–çFW&Æö6¶VBä–æ7&VÖVçB‡&Vbö6ÆÇ2“°¢–b…öW†6WF–öâ—2æ÷BçVÆÂ¢°¢F‡&÷röW†6WF–öã°¢Ğ¢–b…öFVÆ’âF–ÖU7âå¦W&ò¢°¢v—BF6²äFVÆ’…öFVÆ’Â6æ6VÆÆF–öåFö¶Vâ“°¢Ğ¢&WGW&âæWr‡GG&W7öç6TÖW76vR…7—7FVÒäæWBä‡GG7FGW46öFRäô²¢°¢6öçFVçBÒæWr7G&–æt6öçFVçB…&W7öç6T§6öâ¢Ó°¢Ğ§Ğ §6VÆVB6Æ72f¶U&V6÷&FW$&6¶VæDf7F÷'’¢•67&VVå&V6÷&FW$&6¶VæDf7F÷'§°¢V&Æ–2f¶U&V6÷&FW$&6¶VæB&6¶VæB²vWC²ÒÒæWr‚“°¢V&Æ–2&ööÂ6VæDGWÆ–6FT6ÆÆ&6·0¢°¢vWBÓâ&6¶VæBå6VæDGWÆ–6FT6ÆÆ&6·3°¢–æ—BÓâ&6¶VæBå6VæDGWÆ–6FT6ÆÆ&6·2ÒfÇVS°¢Ğ¢V&Æ–2&ööÂ6ö×ÆWFTöå7F÷ ¢°¢vWBÓâ&6¶VæBä6ö×ÆWFTöå7F÷°¢–æ—BÓâ&6¶VæBä6ö×ÆWFTöå7F÷ÒfÇVS°¢Ğ¢V&Æ–2&ööÂf–Äöå7F÷ ¢°¢vWBÓâ&6¶VæBäf–Äöå7F÷°¢–æ—BÓâ&6¶VæBäf–Äöå7F÷ÒfÇVS°¢Ğ¢V&Æ–2–çB6ÆÆ&6´FVÆ”×0¢°¢vWBÓâ&6¶VæBä6ÆÆ&6´FVÆ”×3°¢–æ—BÓâ&6¶VæBä6ÆÆ&6´FVÆ”×2ÒfÇVS°¢Ğ¢V&Æ–2&ööÂ&Æö6µ7F÷ ¢°¢vWBÓâ&6¶VæBä&Æö6µ7F÷°¢–æ—BÓâ&6¶VæBä&Æö6µ7F÷ÒfÇVS°¢Ğ ¢V&Æ–2•67&VVå&V6÷&FW$&6¶VæB7&VFR…&V6÷&FW$÷F–öç2÷F–öç2’Óâ&6¶VæC°§Ğ §6VÆVB6Æ72f¶U&V6÷&FW$&6¶VæB¢•67&VVå&V6÷&FW$&6¶Væ@§°¢&—fFR–çBö7F—fT6ÆÇ3°¢&—fFR–çBöÖ†–×VÔ6öæ7W'&VçD6ÆÇ3°¢&—fFR–çBöF—7÷6T6ÆÇ3°¢&—fFR–çB÷7F÷6ÆÇ3°¢&—fFR7G&–ær÷F‚Ò7G&–æräV×G“°¢&—fFR&VFöæÇ’ÖçVÅ&W6WDWfVçE6Æ–Ò÷7F÷&VÆV6RÒæWr†fÇ6R“° ¢V&Æ–2WfVçBWfVçD†æFÆW#Å&V6÷&F–æt6ö×ÆWFTWfVçD&w3ãò6ö×ÆWFVC°¢V&Æ–2WfVçBWfVçD†æFÆW#Å&V6÷&F–ætf–ÆVDWfVçD&w3ãòf–ÆVC°¢V&Æ–2WfVçBWfVçD†æFÆW#Å&V6÷&F–æu7FGW4WfVçD&w3ãò7FGW46†ævVC°¢V&Æ–2ÖçVÅ&W6WDWfVçE6Æ–Ò&V6÷&D6ÆÆVB²vWC²ÒÒæWr†fÇ6R“°¢V&Æ–2ÖçVÅ&W6WDWfVçE6Æ–Ò7F÷VçFW&VB²vWC²ÒÒæWr†fÇ6R“°¢V&Æ–2&ööÂ6VæDGWÆ–6FT6ÆÆ&6·2²vWC²6WC²Ğ¢V&Æ–2&ööÂ6ö×ÆWFTöå7F÷²vWC²6WC²ÒÒG'VS°¢V&Æ–2&ööÂf–Äöå7F÷²vWC²6WC²Ğ¢V&Æ–2–çB6ÆÆ&6´FVÆ”×2²vWC²6WC²Ğ¢V&Æ–2&ööÂ&Æö6µ7F÷²vWC²6WC²Ğ¢V&Æ–2–çBF—7÷6T6ÆÇ2ÓâföÆF–ÆRå&VB‡&VböF—7÷6T6ÆÇ2“°¢V&Æ–2–çB7F÷6ÆÇ2ÓâföÆF–ÆRå&VB‡&Vb÷7F÷6ÆÇ2“°¢V&Æ–2–çBÖ†–×VÔ6öæ7W'&VçD6ÆÇ2ÓâföÆF–ÆRå&VB‡&VböÖ†–×VÔ6öæ7W'&VçD6ÆÇ2“° ¢V&Æ–2fö–B&V6÷&B‡7G&–ærF‚¢°¢æF—fT6ÆÂ‚‚’Óà¢°¢÷F‚ÒFƒ°¢&V6÷&D6ÆÆVBå6WB‚“°¢7FGW46†ævVCòä–çfö¶R‡F†—2ÂæWr&V6÷&F–æu7FGW4WfVçD&w2…&V6÷&FW%7FGW2å&V6÷&F–ær’“°¢Ò“°¢Ğ ¢V&Æ–2fö–BW6R‚’ÓâæF—fT6ÆÂ‚‚’Óà¢7FGW46†ævVCòä–çfö¶R‡F†—2ÂæWr&V6÷&F–æu7FGW4WfVçD&w2…&V6÷&FW%7FGW2åW6VB’’“° ¢V&Æ–2fö–B&W7VÖR‚’ÓâæF—fT6ÆÂ‚‚’Óà¢7FGW46†ævVCòä–çfö¶R‡F†—2ÂæWr&V6÷&F–æu7FGW4WfVçD&w2…&V6÷&FW%7FGW2å&V6÷&F–ær’’“° ¢V&Æ–2fö–B7F÷‚¢°¢æF—fT6ÆÂ‚‚’Óà¢°¢–çFW&Æö6¶VBä–æ7&VÖVçB‡&Vb÷7F÷6ÆÇ2“°¢7F÷VçFW&VBå6WB‚“°¢–b„&Æö6µ7F÷¢°¢÷7F÷&VÆV6Råv—B…F–ÖU7âäg&öÕ6V6öæG2ƒ2’“°¢Ğ¢–b„f–Äöå7F÷¢°¢f–ÆVCòä–çfö¶R‡F†—2ÂæWr&V6÷&F–ætf–ÆVDWfVçD&w2‚&fÆ†6–×VÆF"Â÷F‚’“°¢&WGW&ã°¢Ğ¢–b„6ö×ÆWFTöå7F÷¢°¢–b„6ÆÆ&6´FVÆ”×2â¢°¢òÒF6²å'Vâ†7–æ2‚’Óà¢°¢v—BF6²äFVÆ’„6ÆÆ&6´FVÆ”×2“°¢6ö×ÆWFTÆFW"‚“°¢Ò“°¢Ğ¢VÇ6P¢°¢6ö×ÆWFTÆFW"‚“°¢Ğ¢Ğ¢Ò“°¢Ğ ¢V&Æ–2fö–B&VÆV6U7F÷‚’Óâ÷7F÷&VÆV6Rå6WB‚“° ¢V&Æ–2fö–B6ö×ÆWFTÆFW"‚¢°¢w&—FUfÆ–D×B…÷F‚“°¢6ö×ÆWFVCòä–çfö¶R€¢F†—2À¢æWr&V6÷&F–æt6ö×ÆWFTWfVçD&w2…÷F‚ÂæWrÆ—7CÄg&ÖTFFâ‚’’“°¢–b…6VæDGWÆ–6FT6ÆÆ&6·2¢°¢f–ÆVCòä–çfö¶R€¢F†—2À¢æWr&V6÷&F–ætf–ÆVDWfVçD&w2‚&6ÆÆ&6²F&F–ò"Â÷F‚’“°¢Ğ¢Ğ ¢V&Æ–2fö–BF—7÷6R‚¢°¢æF—fT6ÆÂ‚‚’Óâ–çFW&Æö6¶VBä–æ7&VÖVçB‡&VböF—7÷6T6ÆÇ2’“°¢Ğ ¢&—fFRfö–BæF—fT6ÆÂ„7F–öâ7F–öâ¢°¢f"6öæ7W'&VçBÒ–çFW&Æö6¶VBä–æ7&VÖVçB‡&Vbö7F—fT6ÆÇ2“°¢WFFTÖ†–×VÒ†6öæ7W'&VçB“°¢G'¢°¢F‡&VBå6ÆVWƒR“°¢7F–öâ‚“°¢Ğ¢f–æÆÇ¢°¢–çFW&Æö6¶VBäFV7&VÖVçB‡&Vbö7F—fT6ÆÇ2“°¢Ğ¢Ğ ¢&—fFRfö–BWFFTÖ†–×VÒ†–çBfÇVR¢°¢v†–ÆR‡G'VR¢°¢f"7W'&VçBÒföÆF–ÆRå&VB‡&VböÖ†–×VÔ6öæ7W'&VçD6ÆÇ2“°¢–b†7W'&VçBãÒfÇVRÇÀ¢–çFW&Æö6¶VBä6ö×&TW†6†ævR‡&VböÖ†–×VÔ6öæ7W'&VçD6ÆÇ2ÂfÇVRÂ7W'&VçB’ÓÒ7W'&VçB¢°¢&WGW&ã°¢Ğ¢Ğ¢Ğ ¢&—fFR7FF–2fö–Bw&—FUfÆ–D×B‡7G&–ærF‚’Óâf–ÆRåw&—FTÆÄ'—FW2€¢F‚À¢°¢ÂÂÂ"Â†'—FR’vbrÂ†'—FR’wBrÂ†'—FR’w’rÂ†'—FR’wrÂÂÂÂÀ¢ÂÂÂ"Â†'—FR’vÒrÂ†'—FR’vBrÂ†'—FR’vrÂ†'—FR’wBrÂÂ"Â2ÂBÀ¢ÂÂÂ‚Â†'—FR’vÒrÂ†'—FR’vòrÂ†'—FR’vòrÂ†'—FR’wbp¢Ò“°§Ğ 