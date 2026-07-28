using SlashText.Models;
using SlashText.Services;
using System.Reflection;
using System.Runtime.InteropServices;

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
    Require(Directory.GetFiles(backups, "snippets-*.md").Length == 1, "backup diário consolidado");

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

    var code = "Antes\n```powershell\nGet-Date\n```\nDepois";
    Require(
        RichTextMarkdownConverter.ToHtml(code).Contains("<pre", StringComparison.Ordinal),
        "bloco de código HTML");
    Require(
        RichTextMarkdownConverter.ToPlainText(code).Contains("Get-Date", StringComparison.Ordinal),
        "fallback de código em texto simples");
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

static void Require(bool condition, string scenario)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Falha no cenário: {scenario}");
    }
}
