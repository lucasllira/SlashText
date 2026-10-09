using System.Text;
using System.Text.RegularExpressions;

namespace SlashText.Services;

public sealed record CodeBlockContent(string Language, string Code);
public sealed record CodeBlockSpan(int Start, int Length, CodeBlockContent Content, string Source);

/// <summary>Fences are parsed before Markdown/variables so source characters remain literal.</summary>
public static partial class CodeBlockMarkdown
{
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static IReadOnlyList<CodeBlockSpan> Read(string text)
    {
        var result = new List<CodeBlockSpan>();
        var position = 0;
        while (position < text.Length)
        {
            var end = text.IndexOf('\n', position); if (end < 0) end = text.Length;
            var opening = FenceStart().Match(text[position..end].TrimEnd('\r'));
            if (!opening.Success || end == text.Length) { position = end + 1; continue; }
            var fence = opening.Groups["fence"].Value;
            var contentStart = end + 1; var closing = contentStart; var found = false;
            while (closing < text.Length)
            {
                var closingEnd = text.IndexOf('\n', closing); if (closingEnd < 0) closingEnd = text.Length;
                if (text[closing..closingEnd].TrimEnd('\r', ' ', '\t') == fence)
                {
                    var contentEnd = Math.Max(contentStart, closing - 1);
                    if (contentEnd > contentStart && text[contentEnd - 1] == '\r') contentEnd--;
                    var source = text[position..closingEnd];
                    result.Add(new CodeBlockSpan(position, closingEnd - position,
                        new CodeBlockContent(opening.Groups["language"].Value, Normalize(text[contentStart..contentEnd])), source));
                    position = closingEnd + 1; found = true; break;
                }
                closing = closingEnd + 1;
            }
            if (!found) position = end + 1;
        }
        return result;
    }

    public static string Write(CodeBlockContent content)
    {
        var code = Normalize(content.Code);
        var longest = 0; var current = 0;
        foreach (var character in code) { current = character == '`' ? current + 1 : 0; longest = Math.Max(longest, current); }
        var fence = new string('`', Math.Max(3, longest + 1));
        var language = LanguageName().IsMatch(content.Language) ? content.Language : "text";
        return fence + language + "\n" + code + "\n" + fence;
    }

    public static string Transform(string text, Func<string, string> outside, Func<CodeBlockSpan, string> code)
    {
        var builder = new StringBuilder(); var position = 0;
        foreach (var span in Read(text))
        {
            builder.Append(outside(text[position..span.Start])).Append(code(span));
            position = span.Start + span.Length;
        }
        return builder.Append(outside(text[position..])).ToString();
    }

    [GeneratedRegex(@"^(?<fence>`{3,}|~{3,})(?<language>[A-Za-z0-9_+#.-]*)[ \t]*$")]
    private static partial Regex FenceStart();
    [GeneratedRegex(@"^[A-Za-z0-9_+#.-]*$")]
    private static partial Regex LanguageName();
}
