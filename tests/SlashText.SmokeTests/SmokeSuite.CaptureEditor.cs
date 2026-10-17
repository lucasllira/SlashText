using SlashText.Models;
using SlashText.Services;

internal static partial class SmokeSuite
{
    private static async Task RunCaptureEditorChecksAsync(string root)
    {
        Exception? workbenchFailure = null;
        var workbenchThread = new Thread(() =>
        {
            try
            {
                CaptureEditorPerformanceChecks.Run();
                CaptureCustomStampChecks.Run();
                CaptureStampLongPathChecks.Run(Path.Combine(root, "stamp-long-paths"));
                using var bitmap = new System.Drawing.Bitmap(800, 400);
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.CornflowerBlue);
                }
                using var editor = new SlashText.Views.CaptureWorkbenchEditor();
                editor.LoadImage(bitmap);
                void LayoutPreview()
                {
                    editor.Measure(new System.Windows.Size(600, 320));
                    editor.Arrange(new System.Windows.Rect(0, 0, 600, 320));
                    editor.UpdateLayout();
                }
                LayoutPreview();
                var viewport = (System.Windows.Controls.ScrollViewer)editor.Content;
                var preview = (System.Windows.Controls.Viewbox)viewport.Content;
                editor.SetZoom(1);
                LayoutPreview();
                var fittedWidth = preview.Width;
                Require(double.IsFinite(fittedWidth) && fittedWidth > 0,
                    "prévia tem uma dimensão ajustada e limitada ao viewport");
                foreach (var zoom in new[] { .75, 1d, 1.25 })
                {
                    editor.SetZoom(zoom);
                    LayoutPreview();
                    Require(Math.Abs(preview.Width - fittedWidth * zoom) < .01,
                        $"zoom {zoom:P0} muda a dimensão visível sem ser compensado pelo Viewbox");
                    using var output = editor.Render();
                    Require(output.Width == 800 && output.Height == 400 &&
                            output.GetPixel(200, 100).ToArgb() == bitmap.GetPixel(200, 100).ToArgb(),
                        "zoom não redimensiona nem altera os pixels da imagem exportada");
                }
                RequireThrows<ArgumentOutOfRangeException>(() => editor.SetZoom(double.NaN),
                    "zoom inválido é rejeitado");
                editor.InsertStamp("👍", new System.Windows.Point(400, 200));
                var previewImage = (System.Windows.Controls.Image)((System.Windows.Controls.Grid)preview.Child).Children[0];
                Require(previewImage.Source is System.Windows.Media.Imaging.BitmapSource,
                    "prévia usa a mesma composição raster do documento, incluindo emote Noto");
                using (var stampOutput = editor.Render())
                using (var expectedStamp = CaptureAnnotationRenderer.Render(bitmap,
                    [new CaptureAnnotation { Kind = CaptureAnnotationKind.Stamp, Start = new System.Windows.Point(400, 200),
                        Text = "👍", Size = 48 }], 800, 400))
                {
                    Require(stampOutput.GetPixel(400, 200).ToArgb() == expectedStamp.GetPixel(400, 200).ToArgb(),
                        "editor simples exporta o mesmo emote do renderizador compartilhado");
                    var pixel = new byte[4];
                    ((System.Windows.Media.Imaging.BitmapSource)previewImage.Source).CopyPixels(new System.Windows.Int32Rect(400, 200, 1, 1), pixel, 4, 0);
                    var expectedPixel = stampOutput.GetPixel(400, 200);
                    Require(pixel[0] == expectedPixel.B && pixel[1] == expectedPixel.G && pixel[2] == expectedPixel.R,
                        "emote da prévia e do arquivo tem os mesmos pixels, sem glifo do sistema");
                }
                var session = editor.Document;
                editor.MarkSaved();
                editor.ResizeImage(400, 200);
                Require(editor.HasUnsavedChanges && ReferenceEquals(session, editor.Document), "redimensionar não recria a sessão");
                editor.Undo();
                Require(!editor.HasUnsavedChanges && editor.CanRedo && editor.Document!.Dimensions.Width == 800,
                    "salvar preserva undo/redo e checkpoint");
                editor.Redo();
                Require(editor.Document!.Dimensions.Width == 400, "refazer restaura dimensões");
                editor.DiscardChanges();
                Require(!editor.HasUnsavedChanges && editor.Document!.Dimensions.Width == 800, "descartar volta à última versão salva");
                Require(SlashText.Views.CaptureEmojiPicker.Search("coracao").Count > 0 &&
                        SlashText.Views.CaptureEmojiPicker.Search("emote inexistente xyz").Count == 0,
                    "busca do seletor Noto ignora acentos e expõe resultado vazio");
                RequireThrows<ArgumentException>(() => NotoEmojiCatalog.CreateBitmap("emoji-inexistente"),
                    "emoji desconhecido não vira silenciosamente um coração");
                foreach (var extension in new[] { ".png", ".jpg", ".gif" })
                {
                    var path = Path.Combine(root, "clipboard-probe" + extension);
                    var format = extension switch
                    {
                        ".jpg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                        ".gif" => System.Drawing.Imaging.ImageFormat.Gif,
                        _ => System.Drawing.Imaging.ImageFormat.Png
                    };
                    bitmap.Save(path, format);
                    var payload = CaptureService.CreateClipboardData(path);
                    Require(payload.ContainsImage() && payload.GetImage().PixelWidth == 800 &&
                            payload.GetImage().PixelHeight == 400,
                        $"Recentes disponibiliza imagem colável para {extension}");
                    Require(payload.ContainsFileDropList() && payload.GetFileDropList()[0] == path,
                        $"Recentes preserva o arquivo original de {extension}, inclusive GIF animado");
                    using var unlockedFile = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                var videoPath = Path.Combine(root, "clipboard-probe.mp4");
                File.WriteAllBytes(videoPath, [0]);
                var videoPayload = CaptureService.CreateClipboardData(videoPath);
                Require(videoPayload.ContainsFileDropList() && !videoPayload.ContainsImage(),
                    "copiar MP4 continua copiando o arquivo, sem tentar decodificar como imagem");
                RequireThrows<FileNotFoundException>(
                    () => CaptureService.CreateClipboardData(Path.Combine(root, "missing.png")),
                    "arquivo ausente é informado sem substituir o clipboard");
            }
            catch (Exception exception)
            {
                workbenchFailure = exception;
            }
        });
        workbenchThread.SetApartmentState(ApartmentState.STA);
        workbenchThread.Start();
        workbenchThread.Join();
        if (workbenchFailure is not null)
        {
            throw new InvalidOperationException("Smoke de zoom/clipboard da Captura falhou.", workbenchFailure);
        }

        using (var original = new System.Drawing.Bitmap(240, 160))
        {
            using (var graphics = System.Drawing.Graphics.FromImage(original)) graphics.Clear(System.Drawing.Color.White);
            using var document = new CaptureEditorDocument(original);
            var points = new List<System.Windows.Point> { new(20, 20), new(200, 20) };
            document.AddAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Pencil, Points = points,
                OutlineArgb = System.Drawing.Color.Red.ToArgb(), Thickness = 8 });
            points.Clear();
            using (var result = document.Render())
                Require(result.GetPixel(100, 20).R > 230 && result.GetPixel(100, 20).G < 50 && original.GetPixel(100, 20).G == 255,
                    "comando copia pontos mutáveis e preserva a fonte original");
            document.MarkSaved();
            Require(!document.Crop(new System.Windows.Rect(0, 0, 7, 7)), "recorte pequeno não altera o documento");
            Require(document.Crop(new System.Windows.Rect(new System.Windows.Point(220, 140), new System.Windows.Point(20, 20))),
                "recorte aceita arraste inverso");
            Require(document.Dimensions == new System.Drawing.Size(200, 120), "recorte usa pixels de imagem");
            document.Resize(100, 60);
            document.AddAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Rectangle,
                Start = new System.Windows.Point(10, 10), End = new System.Windows.Point(35, 35),
                FillArgb = System.Drawing.Color.Blue.ToArgb(), OutlineArgb = null });
            using (var result = document.Render())
                Require(result.Size == new System.Drawing.Size(100, 60) && result.GetPixel(20, 20).B == 255 && result.GetPixel(20, 20).R == 0,
                    "anotações posteriores à geometria usam coordenadas do novo resultado");
            document.Undo(); document.Undo(); document.Undo();
            Require(!document.HasUnsavedChanges && document.CanRedo && document.Dimensions == original.Size,
                "undo de anotações e geometria chega ao checkpoint salvo");
            document.AddAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Text, Start = new(25, 60),
                Text = "Texto", FontFamily = "Georgia", Size = 24, Bold = false, Italic = true });
            Require(document.HasUnsavedChanges && !document.CanRedo, "bifurcação invalida redo e não se confunde com checkpoint de mesmo tamanho");
            document.DiscardChanges();
            Require(document.OperationCount == 1 && !document.HasUnsavedChanges, "descartar restaura checkpoint, não o arquivo original");
            Require(!CaptureEditorDocument.ValidSize(32000, 32000) && !CaptureEditorDocument.ValidSize(7, 8) && CaptureEditorDocument.ValidSize(8000, 8000),
                "redimensionamento tem limite de memória e dimensões");
            document.Resize(120, 80);
            using var resized = document.Render();
            Require(resized.GetPixel(119, 79).A == 255, "resize não introduz bordas transparentes");
        }
        // Editing an older object must retain its identity and creation coordinate
        // space across later geometry, cache invalidation, undo and save checkpoints.
        using (var source = new System.Drawing.Bitmap(240, 160))
        {
            using (var graphics = System.Drawing.Graphics.FromImage(source)) graphics.Clear(System.Drawing.Color.White);
            using var objects = new CaptureEditorDocument(source);
            objects.AddAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Rectangle, Start = new(40, 40), End = new(80, 80),
                FillArgb = System.Drawing.Color.Red.ToArgb(), OutlineArgb = null });
            var id = objects.EditableAnnotations().Single().Id;
            using (var cached = objects.Render()) Require(cached.GetPixel(50, 50).R == 255, "primeira renderização do objeto");
            objects.Crop(new System.Windows.Rect(20, 20, 200, 120)); objects.Resize(100, 120);
            var transformed = objects.EditableAnnotations().Single();
            Require(transformed.Id == id && transformed.Annotation.Start == new System.Windows.Point(10, 20) &&
                transformed.ScaleX == .5 && transformed.ScaleY == 1, "objeto conserva identidade após recorte e resize não proporcional");
            objects.MarkSaved();
            Require(objects.MoveAnnotation(id, 10, 10), "mover objeto em coordenadas atuais");
            var moved = objects.EditableAnnotations().Single();
            Require(moved.Annotation.Start == new System.Windows.Point(20, 30), "movimento inverte transformação da sessão");
            Require(objects.HitTestAnnotation(new(25, 35))?.Id == id, "seleção encontra objeto transformado");
            Require(objects.UpdateAnnotation(id, moved.Annotation with { FillArgb = System.Drawing.Color.Blue.ToArgb() }), "alterar propriedades de objeto existente");
            using (var result = objects.Render())
                Require(result.GetPixel(25, 35).B > 240 && result.GetPixel(25, 35).R < 20 && result.GetPixel(12, 22).R > 240 && result.GetPixel(12, 22).G > 240,
                    "cache reconstrói composição editada sem manter a posição antiga");
            objects.Undo(); Require(objects.EditableAnnotations().Single().Annotation.FillArgb == System.Drawing.Color.Red.ToArgb(), "undo restaura estilo");
            objects.Undo(); Require(!objects.HasUnsavedChanges, "undo do movimento chega ao checkpoint");
            objects.Redo(); objects.Redo(); Require(objects.DeleteAnnotation(id) && objects.EditableAnnotations().Count == 0, "excluir objeto");
            objects.Undo(); Require(objects.EditableAnnotations().Single().Id == id, "undo da exclusão recupera identidade");
            objects.DiscardChanges(); Require(objects.EditableAnnotations().Single().Annotation.Start == new System.Windows.Point(10, 20) && !objects.HasUnsavedChanges, "descartar restaura objetos do checkpoint");
            Require(!objects.DeleteAnnotation(-1) && !objects.MoveAnnotation(id, double.NaN, 0), "operações inválidas não modificam objetos");
            Require(source.GetPixel(50, 50).G == 255, "edição de objetos preserva fonte original");
            objects.AddAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Text, Start = new(15, 25), Text = "Texto", FontFamily = "Arial", Size = 20 });
            var text = objects.EditableAnnotations().Last();
            Require(objects.UpdateAnnotation(text.Id, text.Annotation with { Text = "Novo texto", Size = 32, Bold = true }) && objects.EditableAnnotations().Last().Annotation.Text == "Novo texto", "texto pode ser alterado após inserção");
            Require(!objects.CanRedo, "alteração nova invalida ramo redo antigo");
        }
        var historyFixture = Enumerable.Range(0, 75).Select(i => new CaptureRecord {
            FilePath = $"regiao-projeto-{i:000}.png", Type = "regiao", CreatedAt = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero).AddMinutes(i)
        }).ToArray();
        Require(CaptureHistoryQuery.Filter(historyFixture, "all", "").Count == 75, "histórico completo não é limitado aos 40 recentes");
        Require(CaptureHistoryQuery.Filter(historyFixture, "regiao", "REGIÃO 09/10/2026").Count == 75, "busca combina tipo e data sem diferença de acentos ou caixa");
        Require(CaptureHistoryQuery.Filter(historyFixture, "all", "projeto-074").Single().FilePath.EndsWith("074.png"), "busca pelo nome do arquivo");
        Require(CaptureHistoryQuery.Filter(historyFixture, "video", "").Count == 0 && CaptureHistoryQuery.Filter(historyFixture, "all", "outubro").Count == 75, "filtro de mídia e mês em português");
        Require(CaptureHistoryQuery.Filter(historyFixture, "all", "")[0] == historyFixture[74], "resultados ordenam mais recentes primeiro");
        var pilotExe = Path.Combine(root, "pilot-isolation");
        var officialData = Path.Combine(root, "pilot-local", "SlashDesk");
        Directory.CreateDirectory(officialData);
        await File.WriteAllTextAsync(Path.Combine(officialData, "settings.json"), "{\"theme\":\"Dark\"}");
        Directory.CreateDirectory(Path.Combine(pilotExe, "SlashDeskData"));
        await File.WriteAllTextAsync(Path.Combine(pilotExe, "SlashDeskData", "snippets.md"), "official-portable-sentinel");
        var pilotEnvironment = new AppDataEnvironment(DistributionMode.Installed, pilotExe, Path.Combine(root, "pilot-local"), isCapturePilot: true);
        var pilotLayout = new DataMigrationService().EnsureLayout(pilotEnvironment);
        Require(pilotEnvironment.IsPortable && pilotEnvironment.DataDirectory == Path.Combine(pilotExe, "SlashDeskPilotData") &&
                !pilotLayout.Migrated && pilotLayout.SourceDirectory is null && pilotLayout.BackupPath is null &&
                !File.Exists(Path.Combine(pilotEnvironment.DataDirectory, "settings.json")) &&
                !File.Exists(Path.Combine(pilotEnvironment.DataDirectory, "snippets.md")),
            "piloto não migra nem importa dados da instalação oficial ou do portátil adjacente");
        Require(await File.ReadAllTextAsync(Path.Combine(pilotExe, "SlashDeskData", "snippets.md")) == "official-portable-sentinel" &&
                await File.ReadAllTextAsync(Path.Combine(officialData, "settings.json")) == "{\"theme\":\"Dark\"}",
            "isolamento preserva arquivos oficiais byte a byte");
    }
}
