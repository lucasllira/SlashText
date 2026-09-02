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

Require(TriggerRule.TryValidate("/teste", out _), "regra única aceita prefixo barra");
Require(TriggerRule.TryValidate(":ação_2-rapida", out _), "regra única aceita letras Unicode, números, hífen e sublinhado");
Require(!TriggerRule.TryValidate("teste", out _), "regra única rejeita gatilho sem prefixo");
Require(!TriggerRule.TryValidate("/inválido!", out _), "regra única rejeita caractere impossível para o monitor");
var maximumTrigger = "/" + new string('a', TriggerRule.MaximumLength - 1);
Require(TriggerRule.TryValidate(maximumTrigger, out _), "gatilho no tamanho máximo");
Require(!TriggerRule.TryValidate(maximumTrigger + "a", out _), "gatilho acima do tamanho máximo");
Require(TriggerRule.ConflictsWith("/TESTE", ["/teste"]), "conflito ignora maiúsculas e minúsculas");
Require(TriggerRule.IsPrefixOfAnother("/at", ["/atendimento"]), "gatilhos com prefixos semelhantes");

var bufferState = new KeyboardBufferState();
bufferState.Append('/', new IntPtr(10), new IntPtr(11));
bufferState.Append('a', new IntPtr(10), new IntPtr(11));
bufferState.Append('t', new IntPtr(10), new IntPtr(11));
Require(bufferState.Text == "/at", "buffer mantém sugestão parcial");
Require(bufferState.TargetChanged(new IntPtr(12), new IntPtr(11)), "troca de janela detectada no buffer");
bufferState.Clear(BufferResetReason.MouseClick);
Require(!bufferState.HasValue && bufferState.LastResetReason == BufferResetReason.MouseClick, "clique limpa buffer");
bufferState.Append(':', new IntPtr(20), new IntPtr(21));
bufferState.Append('x', new IntPtr(20), new IntPtr(21));
Require(bufferState.TargetChanged(new IntPtr(20), new IntPtr(22)), "troca de controle focado limpa buffer");

var planOneTab = ExpansionPlan.Create("Primeiro campo" + TemplateEngine.TabMarker + "Segundo campo");
Require(planOneTab.Count == 2 && planOneTab[0].SendTabAfter && !planOneTab[1].SendTabAfter, "sequência com um Tab");
var planManyTabs = ExpansionPlan.Create(
    "Campo 1" + TemplateEngine.TabMarker + "Campo 2" + TemplateEngine.TabMarker + "Campo 3");
Require(planManyTabs.Count == 3 && planManyTabs.Count(step => step.SendTabAfter) == 2, "sequência com vários Tabs");
Require(Enum.IsDefined(SuggestionConfirmation.Enter) &&
        Enum.IsDefined(SuggestionConfirmation.Tab) &&
        Enum.IsDefined(SuggestionConfirmation.Space) &&
        Enum.IsDefined(SuggestionConfirmation.Click), "formas de confirmação da sugestão");

var expansionGate = new SingleFlightGate();
using (var firstExpansion = expansionGate.TryEnter())
{
    Require(firstExpansion is not null && expansionGate.IsActive, "primeira expansão entra no single-flight");
    Require(expansionGate.TryEnter() is null, "segunda expansão simultânea é ignorada");
}
using var afterExpansion = expansionGate.TryEnter();
Require(!expansionGate.IsActive || afterExpansion is not null, "trava de expansão liberada");
var captureGate = new SingleFlightGate();
try
{
    using var captureLease = captureGate.TryEnter();
    Require(captureGate.TryEnter() is null, "apenas uma captura simultânea");
    throw new InvalidOperationException("falha simulada");
}
catch (InvalidOperationException)
{
    // A liberação ocorre pelo finally implícito de using.
}
Require(!captureGate.IsActive, "trava de captura liberada após exceção");

var debounce = new DebounceGate(TimeSpan.FromMilliseconds(300));
Require(debounce.TryAccept(1_000_000), "primeiro evento do debounce");
Require(!debounce.TryAccept(1_000_001), "evento repetido é bloqueado pelo debounce");
Require(
    (GlobalCaptureShortcutService.ApplyNoRepeat(2) & GlobalCaptureShortcutService.ModNoRepeat) != 0,
    "MOD_NOREPEAT aplicado a hotkeys");

var toolbarScenarios = new[]
{
    (Name: "primário 100% taskbar inferior", Work: new System.Windows.Rect(0, 0, 1920, 1040), Dpi: 1d),
    (Name: "primário 125%", Work: new System.Windows.Rect(0, 0, 2560, 1392), Dpi: 1.25d),
    (Name: "primário 150%", Work: new System.Windows.Rect(0, 0, 2560, 1400), Dpi: 1.5d),
    (Name: "secundário à esquerda DPI diferente", Work: new System.Windows.Rect(-1920, 0, 1920, 1040), Dpi: 1.25d),
    (Name: "secundário acima", Work: new System.Windows.Rect(0, -1080, 1920, 1080), Dpi: 1.5d),
    (Name: "taskbar lateral", Work: new System.Windows.Rect(80, 0, 1840, 1080), Dpi: 1d)
};
foreach (var scenario in toolbarScenarios)
{
    var margin = 12 * scenario.Dpi;
    var selections = new[]
    {
        new System.Windows.Rect(scenario.Work.Left, scenario.Work.Top, 80, 60),
        new System.Windows.Rect(scenario.Work.Right - 80, scenario.Work.Top, 80, 60),
        new System.Windows.Rect(scenario.Work.Left, scenario.Work.Bottom - 80, 80, 80),
        new System.Windows.Rect(scenario.Work.Right - 80, scenario.Work.Bottom - 80, 80, 80),
        new System.Windows.Rect(scenario.Work.Left + 10, scenario.Work.Top + 10, 24, 24),
        new System.Windows.Rect(
            scenario.Work.Left + 4,
            scenario.Work.Top + 4,
            scenario.Work.Width - 8,
            scenario.Work.Height - 8)
    };
    foreach (var selection in selections)
    {
        var availableWidth = scenario.Work.Width - (margin * 2);
        var naturalWidth = 900 * scenario.Dpi;
        var finalWidth = Math.Min(naturalWidth, availableWidth);
        var rows = Math.Max(1, (int)Math.Ceiling(naturalWidth / availableWidth));
        var finalHeight = (72 + ((rows - 1) * 48)) * scenario.Dpi;
        var placement = ToolbarPlacementCalculator.Calculate(
            selection,
            scenario.Work,
            new System.Windows.Size(finalWidth, finalHeight),
            naturalWidth,
            dpiScale: scenario.Dpi);
        Require(
            placement.Bounds.Left >= scenario.Work.Left + margin - .01 &&
            placement.Bounds.Top >= scenario.Work.Top + margin - .01 &&
            placement.Bounds.Right <= scenario.Work.Right - margin + .01 &&
            placement.Bounds.Bottom <= scenario.Work.Bottom - margin + .01,
            $"retângulo completo da barra permanece na área útil: {scenario.Name}");
    }
}
var narrowPlacement = ToolbarPlacementCalculator.Calculate(
    new System.Windows.Rect(430, 350, 48, 48),
    new System.Windows.Rect(0, 0, 480, 440),
    new System.Windows.Size(456, 184),
    naturalWidth: 900);
Require(
    narrowPlacement.Bounds.Width == 456 &&
    narrowPlacement.Mode == ToolbarLayoutMode.Compact &&
    narrowPlacement.ExpectedRows == 2 &&
    narrowPlacement.Side is ToolbarPlacementSide.Above or ToolbarPlacementSide.InsideBottom,
    "barra larga no canto inferior direito usa modo compacto e posição vertical segura");

var toolSelection = new CaptureToolSelection(CaptureAnnotationKind.Arrow);
Require(toolSelection.IsSelected(CaptureAnnotationKind.Arrow), "ferramenta inicial selecionada");
toolSelection.Select(CaptureAnnotationKind.Pencil);
Require(
    toolSelection.IsSelected(CaptureAnnotationKind.Pencil) &&
    !toolSelection.IsSelected(CaptureAnnotationKind.Arrow),
    "somente uma ferramenta de desenho permanece selecionada");
Require(
    CaptureToolbarLayoutPolicy.ShouldUseCompactMode(540) &&
    !CaptureToolbarLayoutPolicy.ShouldUseCompactMode(680),
    "barra alterna entre modo compacto e normal sem corte");
Require(
    CaptureMotion.Duration(animationsEnabled: false, 160) == TimeSpan.Zero &&
    CaptureMotion.Duration(animationsEnabled: true, 160) == TimeSpan.FromMilliseconds(160),
    "animações respeitam a preferência do Windows");
Require(
    NotoEmojiCatalog.Items.Count == 36 &&
    NotoEmojiCatalog.Items.Select(item => item.Value).Distinct().Count() == 36,
    "catálogo Noto Emoji contém 36 opções únicas");
Require(
    NotoEmojiCatalog.Items.All(NotoEmojiCatalog.HasAsset),
    "todos os emojis Noto possuem PNG incorporado");

foreach (var anchor in new[]
         {
             new System.Windows.Rect(0, 0, 36, 36),
             new System.Windows.Rect(1884, 0, 36, 36),
             new System.Windows.Rect(0, 1004, 36, 36),
             new System.Windows.Rect(1884, 1004, 36, 36)
         })
{
    var popover = ToolbarPlacementCalculator.Calculate(
        anchor,
        new System.Windows.Rect(0, 0, 1920, 1040),
        new System.Windows.Size(340, 420),
        naturalWidth: 340,
        gap: 8,
        dpiScale: 1);
    Require(
        popover.Bounds.Left >= 12 && popover.Bounds.Top >= 12 &&
        popover.Bounds.Right <= 1908 && popover.Bounds.Bottom <= 1028,
        "popover contextual permanece dentro da área útil nos quatro cantos");
}

var captureMenuBelow = AnchoredPopoverPlacementCalculator.Calculate(
    new System.Windows.Rect(120, 420, 118, 40),
    new System.Windows.Rect(0, 0, 1920, 1040),
    new System.Windows.Size(220, 144));
Require(
    captureMenuBelow.Side == AnchoredPopoverSide.Below &&
    captureMenuBelow.Bounds.Left == 120 &&
    captureMenuBelow.Bounds.Top == 468,
    "menu Capturar abre abaixo e alinhado à esquerda do botão dividido");

var captureMenuAbove = AnchoredPopoverPlacementCalculator.Calculate(
    new System.Windows.Rect(1640, 940, 118, 40),
    new System.Windows.Rect(0, 0, 1920, 1040),
    new System.Windows.Size(220, 144));
Require(
    captureMenuAbove.Side == AnchoredPopoverSide.Above &&
    captureMenuAbove.Bounds.Left == 1640 &&
    captureMenuAbove.Bounds.Bottom == 932,
    "menu Capturar inverte para cima somente quando falta espaço abaixo");

var captureMenuClamped = AnchoredPopoverPlacementCalculator.Calculate(
    new System.Windows.Rect(1870, 420, 42, 40),
    new System.Windows.Rect(0, 0, 1920, 1040),
    new System.Windows.Size(220, 144));
Require(
    captureMenuClamped.Bounds.Right == 1908,
    "menu Capturar permanece inteiro na área útil junto à borda direita");

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

    var legacySnippet = new Snippet
    {
        Name = "Legado preservado",
        Trigger = "/legado.incompatível",
        Category = "Geral",
        Content = "Preservar sem ativar",
        HasLegacyIncompatibleTrigger = true
    };
    await repository.SaveAsync([snippet, colonSnippet, legacySnippet]);
    loaded = await repository.LoadAsync();
    Require(
        loaded.Any(item => item.Trigger == legacySnippet.Trigger && item.HasLegacyIncompatibleTrigger),
        "gatilho legado incompatível é preservado e sinalizado");

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
                .SequenceEqual(["backup-manifest.json", "settings.json", "snippets.md", "usage.json"]),
            "backup contém manifesto, atalhos, preferências e estatísticas");
    }
    BackupService.ValidateSnapshot(backupFiles[0]);
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

    var storageRoot = Path.Combine(root, "armazenamento portátil çã com espaços");
    var portableHome = Path.Combine(storageRoot, "origem portátil");
    var localDataRoot = Path.Combine(storageRoot, "local-app-data");
    var installedLegacy = Path.Combine(localDataRoot, "SlashDesk");
    Directory.CreateDirectory(portableHome);
    Directory.CreateDirectory(installedLegacy);
    var preservedSnippetId = Guid.NewGuid();
    await File.WriteAllTextAsync(
        Path.Combine(installedLegacy, "snippets.md"),
        $"## /preservado\n<!-- slashtext:{{\"id\":\"{preservedSnippetId}\",\"name\":\"Preservado\",\"category\":\"Migração\",\"format\":\"plain\",\"enabled\":true,\"confirmKeys\":[\"Enter\"]}} -->\n```text\nConteúdo\n```\n");
    await File.WriteAllTextAsync(Path.Combine(installedLegacy, "settings.json"),
        """{"theme":"Dark","checkUpdatesOnStartup":false}""");
    await File.WriteAllTextAsync(Path.Combine(installedLegacy, "usage.json"),
        "{\"snippets\":[{\"snippetId\":\"" + preservedSnippetId +
        "\",\"count\":9,\"charactersSaved\":42}]," +
        "\"quickAccent\":{\"count\":2,\"characters\":{}}}");
    await File.WriteAllTextAsync(Path.Combine(installedLegacy, "capture-history.json"),
        """[{"id":"history-preserved","createdAt":"2026-08-06T12:00:00-03:00","type":"monitor","mediaKind":"image","filePath":"C:\\Capturas\\preservada.png","width":800,"height":600}]""");

    var portableEnvironment = new AppDataEnvironment(
        DistributionMode.Portable,
        portableHome,
        localDataRoot);
    AppPaths.Initialize(portableEnvironment);
    Require(
        AppPaths.DataDirectory == Path.Combine(portableHome, "SlashDeskData") &&
        AppPaths.LogsDirectory == Path.Combine(portableHome, "SlashDeskData", "Logs") &&
        AppPaths.BackupsDirectory == Path.Combine(portableHome, "SlashDeskData", "Backups"),
        "modo portátil centraliza dados ao lado do executável");
    var migrationResult = AppPaths.EnsureDataLayout();
    Require(
        migrationResult.Migrated &&
        Directory.Exists(installedLegacy) &&
        File.Exists(AppPaths.SnippetsFile) &&
        File.ReadAllText(AppPaths.SnippetsFile).Contains(preservedSnippetId.ToString(), StringComparison.Ordinal) &&
        File.ReadAllText(AppPaths.CaptureHistoryFile).Contains("history-preserved", StringComparison.Ordinal) &&
        migrationResult.BackupPath is not null && File.Exists(migrationResult.BackupPath),
        "migração copia, valida, ativa e preserva origem, atalhos e histórico");
    using (var migrationArchive = ZipFile.OpenRead(migrationResult.BackupPath!))
    {
        Require(
            migrationArchive.GetEntry("migration-manifest.json") is not null &&
            migrationArchive.Entries.Any(item => item.FullName.EndsWith("snippets.md", StringComparison.Ordinal)),
            "backup de migração contém manifesto e atalhos");
    }

    var simultaneousHome = Path.Combine(storageRoot, "duas origens");
    var simultaneousData = Path.Combine(simultaneousHome, "SlashDeskData");
    Directory.CreateDirectory(simultaneousData);
    await File.WriteAllTextAsync(Path.Combine(simultaneousData, "settings.json"),
        """{"theme":"Light"}""");
    AppPaths.Initialize(new AppDataEnvironment(
        DistributionMode.Portable,
        simultaneousHome,
        localDataRoot));
    var simultaneous = AppPaths.EnsureDataLayout();
    Require(
        !simultaneous.Migrated &&
        simultaneous.CompetingSourcePreserved &&
        File.ReadAllText(AppPaths.SettingsFile).Contains("Light", StringComparison.Ordinal) &&
        simultaneous.BackupPath is not null && File.Exists(simultaneous.BackupPath),
        "duas origens priorizam portátil sem mescla destrutiva e preservam a outra origem");

    var movedHome = Path.Combine(storageRoot, "portátil movido");
    Directory.Move(portableHome, movedHome);
    AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, movedHome, localDataRoot));
    Require(
        File.ReadAllText(AppPaths.SnippetsFile).Contains(preservedSnippetId.ToString(), StringComparison.Ordinal) &&
        File.ReadAllText(AppPaths.CaptureHistoryFile).Contains("history-preserved", StringComparison.Ordinal),
        "atalhos e histórico acompanham a pasta portátil movida");

    var movedCaptureDirectory = Path.Combine(movedHome, "Capturas");
    Directory.CreateDirectory(movedCaptureDirectory);
    var movedCapture = Path.Combine(movedCaptureDirectory, "imagem preservada.png");
    await File.WriteAllBytesAsync(movedCapture, [1, 2, 3]);
    var relativeRecord = new CaptureRecord
    {
        FilePath = Path.Combine(portableHome, "Capturas", "imagem preservada.png"),
        PortableRelativePath = Path.Combine("Capturas", "imagem preservada.png")
    };
    Require(
        CapturePathResolver.Resolve(relativeRecord, AppPaths.Current) == movedCapture,
        "caminho relativo localiza mídia após mover a pasta portátil");

    await File.WriteAllTextAsync(AppPaths.CaptureHistoryFile,
        "[{\"id\":\"valid-item\",\"createdAt\":\"2026-08-06T12:00:00-03:00\"," +
        "\"type\":\"regiao\",\"mediaKind\":\"image\",\"filePath\":\"C:\\\\ok.png\"," +
        "\"width\":100,\"height\":80}," +
        "{\"id\":\"corrupt-item\",\"createdAt\":[],\"width\":\"inválido\"}]");
    var tolerantHistory = new CaptureService();
    await tolerantHistory.LoadAsync();
    Require(
        tolerantHistory.History.Count == 1 && tolerantHistory.History[0].Id == "valid-item",
        "item de histórico corrompido não impede carregar registros válidos");

    var invalidPortableRoot = Path.Combine(storageRoot, "sem-permissão");
    await File.WriteAllTextAsync(invalidPortableRoot, "não é um diretório");
    var invalidPortable = new AppDataEnvironment(
        DistributionMode.Portable,
        invalidPortableRoot,
        localDataRoot);
    Require(
        !invalidPortable.TryProbePortableWrite(out var portableWriteError) &&
        !string.IsNullOrWhiteSpace(portableWriteError),
        "portátil detecta diretório sem capacidade de gravação sem alternar origem");

    AppPaths.Initialize(new AppDataEnvironment(
        DistributionMode.Installed,
        Path.Combine(storageRoot, "installed-bin"),
        localDataRoot));
    Require(
        AppPaths.DataDirectory == installedLegacy &&
        !AppPaths.IsPortable,
        "modo instalado usa %LocalAppData%\\SlashDesk");

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
                Kind = CaptureAnnotationKind.Line,
                Start = new System.Windows.Point(5, 80),
                End = new System.Windows.Point(110, 10),
                OutlineArgb = System.Drawing.Color.Cyan.ToArgb(),
                Opacity = .7f,
                Thickness = 8
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Rectangle,
                Start = new System.Windows.Point(15, 15),
                End = new System.Windows.Point(80, 65),
                FillArgb = System.Drawing.Color.Orange.ToArgb(),
                OutlineArgb = null,
                Opacity = .6f
            },
            new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Ellipse,
                Start = new System.Windows.Point(20, 15),
                End = new System.Windows.Point(85, 70),
                FillArgb = null,
                OutlineArgb = System.Drawing.Color.Blue.ToArgb(),
                Thickness = 12
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

        var invisible = new CaptureAnnotation
        {
            Kind = CaptureAnnotationKind.Rectangle,
            FillArgb = null,
            OutlineArgb = null
        };
        Require(!invisible.HasVisibleShapeStyle, "bloqueia forma totalmente invisível");

        System.Drawing.Bitmap? renderedStamp = null;
        Exception? stampFailure = null;
        var stampThread = new Thread(() =>
        {
            try
            {
                renderedStamp = CaptureAnnotationRenderer.Render(
                    source,
                    [new CaptureAnnotation
                    {
                        Kind = CaptureAnnotationKind.Stamp,
                        Start = new System.Windows.Point(60, 45),
                        Text = "❤️",
                        Size = 48
                    }],
                    120,
                    90);
            }
            catch (Exception exception)
            {
                stampFailure = exception;
            }
        });
        stampThread.SetApartmentState(ApartmentState.STA);
        stampThread.Start();
        stampThread.Join();
        if (stampFailure is not null)
        {
            throw new InvalidOperationException("Renderização do emoticon falhou.", stampFailure);
        }
        using (renderedStamp)
        {
            Require(renderedStamp is not null && HasChangedPixel(renderedStamp),
                "emoticon colorido é renderizado no bitmap final");
        }
    }

    var annotationHistory = new CaptureAnnotationHistory();
    var stampAnnotation = new CaptureAnnotation
    {
        Kind = CaptureAnnotationKind.Stamp,
        Text = "⭐",
        Size = 32
    };
    annotationHistory.Add(stampAnnotation);
    Require(annotationHistory.Items.Count == 1 && annotationHistory.CanUndo,
        "emoticon entra no histórico de desfazer");
    annotationHistory.Undo();
    Require(annotationHistory.Items.Count == 0 && annotationHistory.CanRedo,
        "desfaz emoticon");
    annotationHistory.Redo();
    Require(annotationHistory.Items.Count == 1, "refaz emoticon");
    annotationHistory.ClearAll();
    Require(annotationHistory.Items.Count == 0, "limpa todas as marcações");
    annotationHistory.Undo();
    Require(annotationHistory.Items.Count == 1, "limpeza de marcações pode ser desfeita");

    var updateTests = Path.Combine(root, "updates");
    Directory.CreateDirectory(updateTests);
    var now = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    var updateHandler = new FakeUpdateHttpHandler(ReleaseJson("3.0.0"));
    var updateService = new UpdateService(
        new HttpClient(updateHandler),
        Path.Combine(updateTests, "state.json"),
        "2.9.1",
        () => now,
        TimeSpan.FromSeconds(1));
    var updateFound = await updateService.CheckAsync(force: true);
    Require(
        updateFound.UpdateAvailable &&
        updateFound.LatestVersion == "3.0.0" &&
        updateFound.Release?.PortableAsset.Name == "SlashDesk-3.0.0-portable-win-x64.zip" &&
        updateFound.DownloadSize == 12_345,
        "release estável aplica versão e artefatos exatos");
    Require(updateHandler.Calls == 1, "consulta única ao GitHub");

    await updateService.IgnoreVersionAsync("3.0.0");
    var ignored = await updateService.CheckAsync(force: true);
    Require(ignored.Status == UpdateCheckStatus.Ignored && !ignored.UpdateAvailable,
        "versão ignorada não é oferecida novamente");
    updateHandler.ResponseJson = ReleaseJson("3.1.0");
    var afterIgnored = await updateService.CheckAsync(force: true);
    Require(afterIgnored.UpdateAvailable && afterIgnored.LatestVersion == "3.1.0",
        "versão superior volta a ser oferecida");

    await updateService.RemindLaterAsync("3.1.0");
    var cachedDeferred = await updateService.CheckAsync();
    Require(cachedDeferred.Status == UpdateCheckStatus.Cached && !cachedDeferred.UpdateAvailable,
        "cache automático respeita lembrar depois");
    now += UpdateService.AutomaticCheckInterval + TimeSpan.FromMinutes(1);
    var deferred = await updateService.CheckAsync();
    Require(deferred.Status == UpdateCheckStatus.Deferred && !deferred.UpdateAvailable,
        "verificação automática adia somente a versão atual");
    var manualAfterDeferral = await updateService.CheckAsync(force: true);
    Require(
        manualAfterDeferral.Status == UpdateCheckStatus.UpdateAvailable &&
        manualAfterDeferral.UpdateAvailable &&
        manualAfterDeferral.LatestVersion == "3.1.0",
        "busca manual volta a oferecer versão adiada");
    now += UpdateService.RemindLaterInterval + TimeSpan.FromMinutes(1);
    var afterDeferral = await updateService.CheckAsync();
    Require(afterDeferral.UpdateAvailable, "lembrar depois volta a oferecer automaticamente");

    var cacheHandler = new FakeUpdateHttpHandler(ReleaseJson("3.0.0"));
    var cacheService = new UpdateService(
        new HttpClient(cacheHandler),
        Path.Combine(updateTests, "cache-state.json"),
        "2.9.1",
        () => now,
        TimeSpan.FromSeconds(1));
    await Task.WhenAll(cacheService.CheckAsync(), cacheService.CheckAsync());
    Require(cacheHandler.Calls == 1, "pedidos simultâneos usam exclusão e cache");

    var stableFilterHandler = new FakeUpdateHttpHandler(ReleaseJson(
        "3.0.0", includeStable: false, includeDraft: true, includePrerelease: true));
    var stableFilterService = new UpdateService(
        new HttpClient(stableFilterHandler),
        Path.Combine(updateTests, "stable-filter.json"),
        "2.9.1",
        () => now,
        TimeSpan.FromSeconds(1));
    var stableFilter = await stableFilterService.CheckAsync(force: true);
    Require(stableFilter.Status == UpdateCheckStatus.NoRelease,
        "draft e prerelease são ignoradas no canal estável");

    var currentService = new UpdateService(
        new HttpClient(new FakeUpdateHttpHandler(ReleaseJson("2.9.1"))),
        Path.Combine(updateTests, "current.json"),
        "2.9.1",
        () => now,
        TimeSpan.FromSeconds(1));
    Require((await currentService.CheckAsync(force: true)).Status == UpdateCheckStatus.UpToDate,
        "nenhuma atualização quando versões coincidem");

    var offlineService = new UpdateService(
        new HttpClient(new FakeUpdateHttpHandler(new HttpRequestException("offline"))),
        Path.Combine(updateTests, "offline.json"),
        "2.9.1",
        () => now,
        TimeSpan.FromSeconds(1));
    Require((await offlineService.CheckAsync(force: true)).Status == UpdateCheckStatus.Offline,
        "ausência de internet não lança erro invasivo");

    var timeoutService = new UpdateService(
        new HttpClient(new FakeUpdateHttpHandler(TimeSpan.FromMilliseconds(200))),
        Path.Combine(updateTests, "timeout.json"),
        "2.9.1",
        () => now,
        TimeSpan.FromMilliseconds(20));
    Require((await timeoutService.CheckAsync(force: true)).Status == UpdateCheckStatus.Offline,
        "timeout de atualização é tratado");

    var transactionRoot = Path.Combine(root, "update-transaction");
    Directory.CreateDirectory(transactionRoot);
    var transactionData = Path.Combine(transactionRoot, "SlashDeskData");
    Directory.CreateDirectory(transactionData);
    var preservedUpdateData = new Dictionary<string, string>
    {
        ["snippets.md"] = "atalhos, categorias, hyperlinks e variáveis preservados",
        ["settings.json"] = "{\"theme\":\"Dark\",\"checkUpdatesOnStartup\":true}",
        ["usage.json"] = "{\"totalExpansions\":17}",
        ["capture-history.json"] = "[{\"id\":\"preservado-na-atualizacao\"}]"
    };
    foreach (var item in preservedUpdateData)
    {
        await File.WriteAllTextAsync(Path.Combine(transactionData, item.Key), item.Value);
    }
    var targetExecutable = Path.Combine(transactionRoot, "SlashDesk.exe");
    var stagedExecutable = Path.Combine(transactionRoot, "SlashDesk.new.exe");
    var backupExecutable = Path.Combine(transactionRoot, "SlashDesk.previous.exe");
    var failedExecutable = Path.Combine(transactionRoot, "SlashDesk.failed.exe");
    await File.WriteAllTextAsync(targetExecutable, "versão anterior");
    await File.WriteAllTextAsync(stagedExecutable, "versão nova");
    PortableUpdateFileTransaction.Apply(targetExecutable, stagedExecutable, backupExecutable);
    Require(
        await File.ReadAllTextAsync(targetExecutable) == "versão nova" &&
        await File.ReadAllTextAsync(backupExecutable) == "versão anterior" &&
        preservedUpdateData.All(item =>
            File.ReadAllText(Path.Combine(transactionData, item.Key)) == item.Value),
        "substituição troca somente o executável e preserva atalhos, histórico, configurações e estatísticas");
    PortableUpdateFileTransaction.Rollback(targetExecutable, backupExecutable, failedExecutable);
    Require(
        await File.ReadAllTextAsync(targetExecutable) == "versão anterior" &&
        preservedUpdateData.All(item =>
            File.ReadAllText(Path.Combine(transactionData, item.Key)) == item.Value),
        "rollback restaura executável sem alterar atalhos, histórico, configurações e estatísticas");

    var replacementInvoked = false;
    RequireThrows<IOException>(() => PortableUpdateFileTransaction.Apply(
        targetExecutable,
        failedExecutable,
        backupExecutable,
        (_, _, _) =>
        {
            replacementInvoked = true;
            throw new IOException("falha simulada");
        }), "falha na substituição é propagada");
    Require(replacementInvoked && preservedUpdateData.All(item =>
            File.ReadAllText(Path.Combine(transactionData, item.Key)) == item.Value),
        "falha na substituição preserva dados");

    var portableUpdateHome = Path.Combine(root, "Portátil Atualização");
    Directory.CreateDirectory(portableUpdateHome);
    var portableExecutable = Path.Combine(portableUpdateHome, "SlashDesk.exe");
    File.Copy(Environment.ProcessPath!, portableExecutable);
    AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, portableUpdateHome,
        Path.Combine(root, "legacy-update")));
    AppPaths.EnsureDataLayout();
    var updaterDataSentinel = Path.Combine(AppPaths.DataDirectory, "history-sentinel.txt");
    await File.WriteAllTextAsync(updaterDataSentinel, "histórico intacto");
    var updaterVersion = Version.Parse(
        FileVersionInfo.GetVersionInfo(portableExecutable).FileVersion ?? "1.0.0.0").ToString(3);
    var packageBytes = CreatePortableZip(portableExecutable);
    var packageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(packageBytes))
        .ToLowerInvariant();
    var packageName = $"SlashDesk-{updaterVersion}-portable-win-x64.zip";
    var checksumBytes = System.Text.Encoding.ASCII.GetBytes($"{packageHash}  {packageName}");
    var packageRelease = CreatePackageRelease(updaterVersion, packageName, packageBytes.Length,
        checksumBytes.Length);
    var packageHandler = new FakePackageHttpHandler(packageBytes, checksumBytes);
    var packageService = new PortableUpdateService(
        new HttpClient(packageHandler), portableExecutable, currentProcessId: 0);
    var preparedPackage = await packageService.PrepareAsync(packageRelease);
    Require(
        File.Exists(preparedPackage.Manifest.StagedExecutable) &&
        File.Exists(preparedPackage.Manifest.HelperExecutable) &&
        await File.ReadAllTextAsync(updaterDataSentinel) == "histórico intacto",
        "pacote válido é preparado dentro de SlashDeskData sem alterar histórico");

    var badChecksum = checksumBytes.ToArray();
    badChecksum[0] = badChecksum[0] == (byte)'0' ? (byte)'1' : (byte)'0';
    var invalidChecksumService = new PortableUpdateService(
        new HttpClient(new FakePackageHttpHandler(packageBytes, badChecksum)),
        portableExecutable,
        currentProcessId: 0);
    await RequireThrowsAsync<InvalidDataException>(
        () => invalidChecksumService.PrepareAsync(packageRelease),
        "checksum inválido impede atualização");

    var incompleteRelease = CreatePackageRelease(
        updaterVersion, packageName, packageBytes.Length + 10, checksumBytes.Length);
    var incompleteService = new PortableUpdateService(
        new HttpClient(new FakePackageHttpHandler(packageBytes, checksumBytes)),
        portableExecutable,
        currentProcessId: 0);
    await RequireThrowsAsync<EndOfStreamException>(
        () => incompleteService.PrepareAsync(incompleteRelease),
        "download incompleto impede atualização");

    var concurrentHandler = new FakePackageHttpHandler(packageBytes, checksumBytes)
    {
        Delay = TimeSpan.FromMilliseconds(250)
    };
    var concurrentPackageService = new PortableUpdateService(
        new HttpClient(concurrentHandler), portableExecutable, currentProcessId: 0);
    using var concurrentCancellation = new CancellationTokenSource();
    var firstPrepare = concurrentPackageService.PrepareAsync(
        packageRelease, cancellationToken: concurrentCancellation.Token);
    await concurrentHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
    await RequireThrowsAsync<InvalidOperationException>(
        () => concurrentPackageService.PrepareAsync(packageRelease),
        "dois pedidos simultâneos de atualização são bloqueados");
    concurrentCancellation.Cancel();
    await RequireThrowsAsync<OperationCanceledException>(
        () => firstPrepare,
        "fechamento durante download cancela preparação");
    Require(await File.ReadAllTextAsync(updaterDataSentinel) == "histórico intacto",
        "cancelamento do download preserva SlashDeskData");

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

static string ReleaseJson(
    string version,
    bool includeStable = true,
    bool includeDraft = false,
    bool includePrerelease = false)
{
    var releases = new List<string>();
    if (includeDraft)
    {
        releases.Add(ReleaseEntry(version, draft: true, prerelease: false));
    }
    if (includePrerelease)
    {
        releases.Add(ReleaseEntry(version + "-rc.1", draft: false, prerelease: true));
    }
    if (includeStable)
    {
        releases.Add(ReleaseEntry(version, draft: false, prerelease: false));
    }
    return $"[{string.Join(',', releases)}]";
}

static string ReleaseEntry(string version, bool draft, bool prerelease) => $$"""
    {
      "tag_name": "v{{version}}",
      "name": "SlashDesk {{version}}",
      "body": "Notas {{version}}",
      "html_url": "https://github.com/lucasllira/SlashText/releases/tag/v{{version}}",
      "published_at": "2026-08-06T12:00:00Z",
      "draft": {{draft.ToString().ToLowerInvariant()}},
      "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
      "assets": [
        {
          "name": "SlashDesk-{{version}}-portable-win-x64.zip",
          "browser_download_url": "https://example.invalid/SlashDesk-{{version}}-portable-win-x64.zip",
          "size": 12345
        },
        {
          "name": "SlashDesk-{{version}}-portable-win-x64.zip.sha256",
          "browser_download_url": "https://example.invalid/SlashDesk-{{version}}-portable-win-x64.zip.sha256",
          "size": 128
        }
      ]
    }
    """;

static byte[] CreatePortableZip(string executable)
{
    using var output = new MemoryStream();
    using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
    {
        var entry = archive.CreateEntry("SlashDesk.exe", CompressionLevel.NoCompression);
        using var destination = entry.Open();
        using var source = File.OpenRead(executable);
        source.CopyTo(destination);
    }
    return output.ToArray();
}

static ReleaseInfo CreatePackageRelease(
    string version,
    string packageName,
    long packageSize,
    long checksumSize) => new(
        version,
        $"SlashDesk {version}",
        "Notas",
        $"https://github.com/lucasllira/SlashText/releases/tag/v{version}",
        DateTimeOffset.UtcNow,
        new ReleaseAssetInfo(
            packageName,
            $"https://github.com/lucasllira/SlashText/releases/download/v{version}/{packageName}",
            packageSize),
        new ReleaseAssetInfo(
            packageName + ".sha256",
            $"https://github.com/lucasllira/SlashText/releases/download/v{version}/{packageName}.sha256",
            checksumSize));

sealed class FakePackageHttpHandler(byte[] package, byte[] checksum) : HttpMessageHandler
{
    public TimeSpan Delay { get; init; }
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }
        var body = request.RequestUri?.AbsolutePath.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) == true
            ? checksum
            : package;
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
        };
    }
}

sealed class FakeUpdateHttpHandler : HttpMessageHandler
{
    private readonly Exception? _exception;
    private readonly TimeSpan _delay;
    private int _calls;

    public FakeUpdateHttpHandler(string responseJson) => ResponseJson = responseJson;
    public FakeUpdateHttpHandler(Exception exception) => _exception = exception;
    public FakeUpdateHttpHandler(TimeSpan delay)
    {
        _delay = delay;
        ResponseJson = "[]";
    }

    public string ResponseJson { get; set; } = "[]";
    public int Calls => Volatile.Read(ref _calls);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (_exception is not null)
        {
            throw _exception;
        }
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken);
        }
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson)
        };
    }
}

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
