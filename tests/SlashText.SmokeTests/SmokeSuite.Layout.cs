using SlashText.Services;

internal static partial class SmokeSuite
{
    private static void RunLayoutChecks()
    {
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
            NotoEmojiCatalog.Items.Count == 3972 && NotoEmojiCatalog.QuickItems.Count == 36 &&
            NotoEmojiCatalog.Items.Select(item => item.Value).Distinct().Count() == NotoEmojiCatalog.Items.Count &&
            NotoEmojiCatalog.Categories.Count == 9,
            "catálogo Noto completo contém 3972 opções únicas, 9 categorias e 36 acessos rápidos");
        Require(
            NotoEmojiCatalog.Items.All(NotoEmojiCatalog.HasAsset),
            "todos os emojis Noto possuem PNG incorporado");
        Require(NotoEmojiCatalog.TryGet("❤️", out var qualifiedHeart) &&
                NotoEmojiCatalog.TryGet("❤", out var plainHeart) && qualifiedHeart == plainHeart &&
                NotoEmojiCatalog.QuickItems.All(item => NotoEmojiCatalog.TryGet(item.Value, out _)),
            "sequências legadas/qualificadas e todos os acessos rápidos resolvem o mesmo catálogo completo");
        Require(SlashText.Views.CaptureEmojiPicker.Search("gato", "Animais e natureza").Count > 0 &&
                SlashText.Views.CaptureEmojiPicker.Search("", "Bandeiras").All(i => i.Category == "Bandeiras") &&
                NotoEmojiCatalog.TryGet("👍🏽", out _) && NotoEmojiCatalog.TryGet("🇧🇷", out _),
            "catálogo pesquisável inclui animais, categorias, variantes de pele e bandeiras");

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
    }
}
