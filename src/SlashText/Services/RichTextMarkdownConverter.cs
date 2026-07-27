using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SlashText.Models;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace SlashText.Services;

public static partial class RichTextMarkdownConverter
{
    public static void Load(RichTextBox editor, string content, SnippetFormat format)
    {
        editor.Document.Blocks.Clear();
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');

        foreach (var line in normalized.Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            if (format == SnippetFormat.Plain)
            {
                paragraph.Inlines.Add(new Run(line));
            }
            else
            {
                AddMarkdownInlines(paragraph.Inlines, line);
            }

            editor.Document.Blocks.Add(paragraph);
        }

        if (editor.Document.Blocks.Count == 0)
        {
            editor.Document.Blocks.Add(new Paragraph());
        }
    }

    public static string Save(RichTextBox editor, SnippetFormat format)
    {
        var lines = new List<string>();
        foreach (var block in editor.Document.Blocks)
        {
            if (block is Paragraph paragraph)
            {
                lines.Add(format == SnippetFormat.Plain
                    ? new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n')
                    : SerializeInlines(paragraph.Inlines));
            }
        }

        return string.Join("\n", lines).TrimEnd();
    }

    public static string ToPlainText(string markdown)
    {
        var value = markdown;
        value = ColorSpanPattern().Replace(value, match => match.Groups["text"].Value);
        value = LinkPattern().Replace(value, match => match.Groups["text"].Value);
        value = value.Replace("**", string.Empty)
            .Replace("__", string.Empty)
            .Replace("*", string.Empty)
            .Replace("_", string.Empty)
            .Replace("<u>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</u>", string.Empty, StringComparison.OrdinalIgnoreCase);
        return WebUtility.HtmlDecode(value);
    }

    public static string ToHtml(string markdown)
    {
        var escaped = WebUtility.HtmlEncode(markdown);
        escaped = EncodedColorSpanPattern().Replace(
            escaped,
            match => $"<span style=\"color:{match.Groups["color"].Value}\">{match.Groups["text"].Value}</span>");
        escaped = EncodedLinkPattern().Replace(
            escaped,
            match => $"<a href=\"{match.Groups["url"].Value}\">{match.Groups["text"].Value}</a>");
        escaped = EncodedBoldPattern().Replace(escaped, "<strong>${text}</strong>");
        escaped = EncodedUnderlinePattern().Replace(escaped, "<u>${text}</u>");
        escaped = EncodedItalicPattern().Replace(escaped, "<em>${text}</em>");
        return "<div style=\"font-family:'Segoe UI',sans-serif;font-size:11pt\">" +
               escaped.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "<br>") +
               "</div>";
    }

    private static void AddMarkdownInlines(InlineCollection target, string text)
    {
        var index = 0;
        foreach (Match match in InlinePattern().Matches(text))
        {
            if (match.Index > index)
            {
                target.Add(new Run(text[index..match.Index]));
            }

            if (match.Groups["bold"].Success)
            {
                target.Add(new Bold(new Run(match.Groups["bold"].Value)));
            }
            else if (match.Groups["italic"].Success)
            {
                target.Add(new Italic(new Run(match.Groups["italic"].Value)));
            }
            else if (match.Groups["underline"].Success)
            {
                target.Add(new Underline(new Run(match.Groups["underline"].Value)));
            }
            else if (match.Groups["linkText"].Success &&
                     Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var uri))
            {
                target.Add(new Hyperlink(new Run(match.Groups["linkText"].Value))
                {
                    NavigateUri = uri
                });
            }
            else if (match.Groups["colorText"].Success)
            {
                try
                {
                    target.Add(new Run(match.Groups["colorText"].Value)
                    {
                        Foreground = (Brush)new BrushConverter().ConvertFromString(
                            match.Groups["color"].Value)!
                    });
                }
                catch (FormatException)
                {
                    target.Add(new Run(match.Value));
                }
            }

            index = match.Index + match.Length;
        }

        if (index < text.Length)
        {
            target.Add(new Run(text[index..]));
        }
    }

    private static string SerializeInlines(InlineCollection inlines)
    {
        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            SerializeInline(builder, inline);
        }

        return builder.ToString();
    }

    private static void SerializeInline(StringBuilder builder, Inline inline)
    {
        switch (inline)
        {
            case LineBreak:
                builder.Append('\n');
                return;
            case Hyperlink link:
                builder.Append('[');
                foreach (var child in link.Inlines)
                {
                    SerializeInline(builder, child);
                }
                builder.Append("](").Append(link.NavigateUri).Append(')');
                return;
            case Span span when inline is not Run:
                foreach (var child in span.Inlines)
                {
                    SerializeInline(builder, child);
                }
                return;
            case Run run:
                SerializeRun(builder, run);
                return;
        }
    }

    private static void SerializeRun(StringBuilder builder, Run run)
    {
        var value = run.Text;
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var bold = run.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        var italic = run.FontStyle == FontStyles.Italic;
        var underline = run.TextDecorations?.Contains(TextDecorations.Underline[0]) == true;
        var color = run.Foreground is SolidColorBrush brush &&
                    brush.Color != Colors.Black &&
                    brush.Color.ToString() is var valueColor
            ? $"#{valueColor[3..]}"
            : null;

        if (color is not null)
        {
            builder.Append("<span style=\"color:").Append(color).Append("\">");
        }
        if (bold) builder.Append("**");
        if (italic) builder.Append('*');
        if (underline) builder.Append("__");
        builder.Append(value);
        if (underline) builder.Append("__");
        if (italic) builder.Append('*');
        if (bold) builder.Append("**");
        if (color is not null) builder.Append("</span>");
    }

    [GeneratedRegex(
        @"\*\*(?<bold>.+?)\*\*|\*(?<italic>.+?)\*|__(?<underline>.+?)__|\[(?<linkText>.+?)\]\((?<url>https?://[^\s)]+)\)|<span\s+style=""color:(?<color>#[0-9A-Fa-f]{6})"">(?<colorText>.*?)</span>",
        RegexOptions.IgnoreCase)]
    private static partial Regex InlinePattern();

    [GeneratedRegex(@"<span\s+style=""color:(?<color>#[0-9A-Fa-f]{6})"">(?<text>.*?)</span>", RegexOptions.IgnoreCase)]
    private static partial Regex ColorSpanPattern();

    [GeneratedRegex(@"\[(?<text>.*?)\]\((?<url>https?://[^\s)]+)\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"&lt;span\s+style=&quot;color:(?<color>#[0-9A-Fa-f]{6})&quot;&gt;(?<text>.*?)&lt;/span&gt;", RegexOptions.IgnoreCase)]
    private static partial Regex EncodedColorSpanPattern();

    [GeneratedRegex(@"\[(?<text>.*?)\]\((?<url>https?://[^\s)]+)\)")]
    private static partial Regex EncodedLinkPattern();

    [GeneratedRegex(@"\*\*(?<text>.+?)\*\*")]
    private static partial Regex EncodedBoldPattern();

    [GeneratedRegex(@"__(?<text>.+?)__")]
    private static partial Regex EncodedUnderlinePattern();

    [GeneratedRegex(@"\*(?<text>.+?)\*")]
    private static partial Regex EncodedItalicPattern();
}
