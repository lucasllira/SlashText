using SlashText.Models;
using SlashText.Services;

internal static partial class SmokeSuite
{
    private static async Task RunRenderingChecksAsync(string root)
    {
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
            GlobalCaptureShortcutService.IsValid("Ctrl+Shift+WheelUp") &&
            GlobalCaptureShortcutService.IsValid("Ctrl+Shift+WheelDown") &&
            Enum.IsDefined(CaptureShortcutAction.Scrolling),
            "atalhos de janela e captura longa pela roda do mouse");
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
                "Ctrl+Shift+WheelUp" &&
            GlobalCaptureShortcutService.FormatWheelShortcut(
                -120,
                System.Windows.Input.ModifierKeys.Control |
                System.Windows.Input.ModifierKeys.Shift) ==
                "Ctrl+Shift+WheelDown",
            "grava combinações de janela e captura longa pela roda do mouse");
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
        Require(annotationHistory.HitTest(new System.Windows.Point(0, 0)) == 0 &&
            annotationHistory.Move(0, 50, 30) && annotationHistory.Items.Single().Start == new System.Windows.Point(50, 30),
            "move emoji existente sem criar outra anotação");
        Require(annotationHistory.HitTest(new System.Windows.Point(0, 0)) == -1 &&
            annotationHistory.HitTest(new System.Windows.Point(50, 30)) == 0,
            "seleção acompanha a nova posição do emoji");
        Require(!annotationHistory.Move(-1, 10, 10) && !annotationHistory.Move(0, double.NaN, 0) &&
            !annotationHistory.Move(0, 0, 0), "movimentos inválidos e clique sem arraste não criam histórico");
        annotationHistory.Undo();
        Require(annotationHistory.Items.Single().Start == new System.Windows.Point(0, 0), "um undo reverte todo o movimento");
        annotationHistory.Redo();
        Require(annotationHistory.Items.Single().Start == new System.Windows.Point(50, 30) &&
            annotationHistory.Items.Single().Text == stampAnnotation.Text && stampAnnotation.Start == new System.Windows.Point(0, 0),
            "redo mantém conteúdo e não modifica o objeto anterior");
    }
}
