namespace SlashText.Services;

internal static class ShortcutShareMessage
{
    internal static async Task<IReadOnlyList<Models.Snippet>> RenameUntouchedDefaultAsync(
        SnippetMarkdownRepository repository, IReadOnlyList<Models.Snippet> snippets)
    {
        if (snippets.Any(item => string.Equals(item.Trigger, "/slashdesk", StringComparison.OrdinalIgnoreCase))) return snippets;
        var initial = snippets.FirstOrDefault(item => item.Id == Guid.Parse("3e387d61-2c04-4b36-988b-6ce918868fcc") &&
            item.Trigger == "/ola" && item.Name == "Divulgar SlashDesk" && item.Format == Models.SnippetFormat.Plain &&
            item.Content.Replace("\r\n", "\n").TrimEnd('\n') == Content);
        if (initial is null) return snippets;
        var replacement = new Models.Snippet
        {
            Id = initial.Id, Name = initial.Name, Trigger = "/slashdesk", Category = initial.Category,
            Content = initial.Content, Format = initial.Format, Enabled = initial.Enabled,
            IsFavorite = initial.IsFavorite, IsPinned = initial.IsPinned, ConfirmKeys = initial.ConfirmKeys.ToList(),
            HasLegacyIncompatibleTrigger = initial.HasLegacyIncompatibleTrigger
        };
        var next = snippets.Select(item => ReferenceEquals(item, initial) ? replacement : item).ToArray();
        try { await repository.SaveAsync(next); return next; }
        catch (Exception exception)
        {
            // A cosmetic default rename must never prevent using existing shortcuts.
            AppDiagnosticLog.Write("shortcuts.share_default.rename_failed", ("exceptionType", exception.GetType().Name));
            return snippets;
        }
    }

    internal const string Content = "Conheça o SlashDesk!\n\n" +
        "Atalhos de texto, Acento Rápido, capturas de tela e gravações em GIF/MP4 em um único app para Windows. " +
        "Portátil, com seus dados neste computador e sem upload obrigatório.\n\n" +
        "Baixe a versão oficial mais recente:\nhttps://github.com/lucasllira/SlashText/releases/latest";
}
