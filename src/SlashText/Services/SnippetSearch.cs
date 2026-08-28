using SlashText.Models;

namespace SlashText.Services;

public static class SnippetSearch
{
    public static bool Matches(Snippet snippet, string? query)
    {
        var value = query?.Trim();
        return string.IsNullOrWhiteSpace(value) ||
               snippet.Name.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
               snippet.Trigger.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               snippet.Category.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
               ReadableContent(snippet.Content).Contains(value, StringComparison.CurrentCultureIgnoreCase);
    }

    public static string ReadableContent(string content) =>
        RichTextMarkdownConverter.ToPlainText(content ?? string.Empty);
}
