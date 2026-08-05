using SlashText.Models;
using SlashText.Services;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO.Compression;

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
        captureDefaults.HistoryRetentionDays == 90,
        "padrões seguros de gravação e histórico");
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
    await File.WriteAllBytesAsync(
        validMp4,
        [0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0]);
    ScreenRecordingService.ValidateMp4File(validMp4);
    var invalidMp4 = Path.Combine(root, "invalid.mp4");
    await File.WriteAllBytesAsync(invalidMp4, [0, 1, 2, 3]);
    RequireThrows<InvalidDataException>(
        () => ScreenRecordingService.ValidateMp4File(invalidMp4),
        "MP4 vazio ou sem contêiner não entra no histórico");

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
