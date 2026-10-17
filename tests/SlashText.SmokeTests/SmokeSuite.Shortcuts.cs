using SlashText.Models;
using SlashText.Services;

internal static partial class SmokeSuite
{
    private static async Task RunShortcutFormattingChecksAsync(string root, TemplateEngine engine, DateTimeOffset reference)
    {
        var emotePath = "assets/emote-" + new string('a', 64) + ".png";
        foreach (var unicode in new[] { "😀", "❤️", "👩🏽‍💻", "🇧🇷", "🫪" })
        {
            var token = $"![Emoji: {unicode}]({emotePath})";
            Require(RichTextMarkdownConverter.ToPlainText("Antes " + token + " Depois") == "Antes " + unicode + " Depois", "Emoji Unicode preservado em texto simples: " + unicode);
            Require(RichTextMarkdownConverter.ToPlainText(CodeBlockMarkdown.Write(new CodeBlockContent("text", token))) == token, "Código mantém token de imagem literal");
        }
        Require(RichTextMarkdownConverter.ToPlainText($"![Emoji: Meu desenho]({emotePath})") == "[Imagem: Emoji: Meu desenho]", "Imagem personalizada conserva descrição em app somente texto");
        Require(RichTextMarkdownConverter.ToPlainText("![Foto](assets/foto.png)") == "[Imagem: Foto]", "Fallback de imagens normais permanece legível");
        var defaultShare = new Snippet { Id = Guid.Parse("3e387d61-2c04-4b36-988b-6ce918868fcc"), Name = "Divulgar SlashDesk", Trigger = "/ola", Content = ShortcutShareMessage.Content, IsFavorite = true };
        var shareRepository = new SnippetMarkdownRepository(Path.Combine(root, "share-default.md"));
        await shareRepository.SaveAsync([defaultShare]);
        var renamedShare = await ShortcutShareMessage.RenameUntouchedDefaultAsync(shareRepository, await shareRepository.LoadAsync());
        Require(renamedShare.Single().Trigger == "/slashdesk" && renamedShare.Single().IsFavorite && (await shareRepository.LoadAsync()).Single().Content == ShortcutShareMessage.Content, "Somente comando do texto inicial intacto é atualizado");
        defaultShare.Content += " personalizado";
        await shareRepository.SaveAsync([defaultShare]); var personalizedBytes = await File.ReadAllBytesAsync(Path.Combine(root, "share-default.md"));
        var personalizedResult = await ShortcutShareMessage.RenameUntouchedDefaultAsync(shareRepository, await shareRepository.LoadAsync());
        var personalizedAfter = await File.ReadAllBytesAsync(Path.Combine(root, "share-default.md"));
        Require(personalizedResult.Single().Trigger == "/ola" && personalizedBytes.SequenceEqual(personalizedAfter), "Texto personalizado não é migrado");
        defaultShare.Content = ShortcutShareMessage.Content;
        await shareRepository.SaveAsync([defaultShare, new Snippet { Name = "Meu", Trigger = "/slashdesk", Content = "meu conteúdo" }]);
        var conflictedBytes = await File.ReadAllBytesAsync(Path.Combine(root, "share-default.md"));
        await ShortcutShareMessage.RenameUntouchedDefaultAsync(shareRepository, await shareRepository.LoadAsync());
        var conflictedAfter = await File.ReadAllBytesAsync(Path.Combine(root, "share-default.md"));
        Require(conflictedBytes.SequenceEqual(conflictedAfter), "Comando /slashdesk existente não é substituído");

        var literalCode = "  a_b * 2 <tag> &amp; {{nome}} {{tab}}\n\t```\n  final  \n";
        var fencedCode = CodeBlockMarkdown.Write(new CodeBlockContent("javascript", literalCode));
        var code = "Antes\n" + fencedCode + "\nDepois";
        var parsedCode = CodeBlockMarkdown.Read(code).Single();
        Require(parsedCode.Content.Code == literalCode && parsedCode.Content.Language == "javascript", "Código preserva espaços, símbolos, variáveis e crases dentro de cerca mais longa");
        Require(RichTextMarkdownConverter.ToPlainText(code) == "Antes\n" + literalCode + "\nDepois", "Fallback do código é literal, sem remover Markdown nem decodificar entidades");
        Require(RichTextMarkdownConverter.ToHtml(code).Contains(System.Net.WebUtility.HtmlEncode(literalCode), StringComparison.Ordinal), "HTML codifica código sem remover espaços nem interpretar tags");
        Require(engine.GetFillableFields("{{fora}}\n" + fencedCode, literalCodeBlocks: true).Select(f => f.Name).SequenceEqual(new[] { "fora" }), "Somente variáveis fora do código são solicitadas");
        Require(engine.Render("{{fora}}\n" + fencedCode, new Dictionary<string, string> { ["fora"] = "Valor", ["nome"] = "não substituir" }, literalCodeBlocks: true) == "Valor\n" + fencedCode, "Expansão não substitui variáveis nem tabulação do código");
        Require(engine.Render("{{nome}}", new Dictionary<string, string> { ["nome"] = "Texto normal" }) == "Texto normal", "Texto simples mantém variáveis existentes");
        foreach (var payload in new[] { "", "\n", "\t espaços  ", "\r\nlinha\r\nfim\r\n", "~~~\n````\n" })
            Require(CodeBlockMarkdown.Read(CodeBlockMarkdown.Write(new CodeBlockContent("text", payload))).Single().Content.Code == CodeBlockMarkdown.Normalize(payload), "Cerca de código roundtrip literal");
        var flagged = new Snippet { Name = "Código favorito", Trigger = "/codigo", Format = SnippetFormat.Markdown, Content = code, IsFavorite = true, IsPinned = true };
        var codeRepository = new SnippetMarkdownRepository(Path.Combine(root, "code-fixture.md"), Path.Combine(root, "code-backups"));
        await codeRepository.SaveAsync([flagged]);
        var reopenedCode = (await codeRepository.LoadAsync()).Single();
        Require(reopenedCode.Content == code && reopenedCode.IsFavorite && reopenedCode.IsPinned, "Código, favorito e fixado persistem juntos em snippets.md");
        Require(
            RichTextMarkdownConverter.ToHtml(code).Contains("<pre", StringComparison.Ordinal),
            "bloco de código HTML");
        Require(
            RichTextMarkdownConverter.ToPlainText(code).Contains("a_b", StringComparison.Ordinal),
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
    }
}
