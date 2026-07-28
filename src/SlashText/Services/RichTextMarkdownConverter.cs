using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        value = CodeFencePattern().Replace(value, match => match.Groups["code"].Value.TrimEnd());
        value = ImagePattern().Replace(value, match => $"[Imagem: {match.Groups["alt"].Value}]");
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
        var codeBlocks = new List<string>();
        markdown = CodeFencePattern().Replace(markdown, match =>
        {
            var language = WebUtility.HtmlEncode(match.Groups["language"].Value);
            var code = WebUtility.HtmlEncode(match.Groups["code"].Value.TrimEnd());
            codeBlocks.Add(
                $"<pre style=\"background:#151821;color:#F4F6FB;padding:12px;border-radius:8px;" +
                $"font-family:Consolas,monospace;white-space:pre-wrap\"><code data-language=\"{language}\">{code}</code></pre>");
            return $"\u001E_CODE_{codeBlocks.Count - 1}_\u001E";
        });

        var escaped = WebUtility.HtmlEncode(markdown);
        escaped = EncodedImagePattern().Replace(escaped, BuildImageHtml);
        escaped = EncodedColorSpanPattern().Replace(
            escaped,
            match => $"<span style=\"color:{match.Groups["color"].Value}\">{match.Groups["text"].Value}</span>");
        escaped = EncodedLinkPattern().Replace(
            escaped,
            match => $"<a href=\"{match.Groups["url"].Value}\">{match.Groups["text"].Value}</a>");
        escaped = EncodedBoldPattern().Replace(escaped, "<strong>${text}</strong>");
        escaped = EncodedUnderlinePattern().Replace(escaped, "<u>${text}</u>");
        escaped = EncodedItalicPattern().Replace(escaped, "<em>${text}</em>");
        escaped = escaped.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "<br>");
        for (var index = 0; index < codeBlocks.Count; index++)
        {
            escaped = escaped.Replace(
                WebUtility.HtmlEncode($"\u001E_CODE_{index}_\u001E"),
                codeBlocks[index],
                StringComparison.Ordinal);
        }

        return "<div style=\"font-family:'Segoe UI',sans-serif;font-size:11pt\">" + escaped + "</div>";
    }

    public static void BuildPreview(FlowDocument document, string markdown, SnippetFormat format)
    {
        document.Blocks.Clear();
        if (format == SnippetFormat.Plain)
        {
            document.Blocks.Add(new Paragraph(new Run(markdown))
            {
                Margin = new Thickness(0)
            });
            return;
        }

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var position = 0;
        foreach (Match match in CodeFencePattern().Matches(normalized))
        {
            AddPreviewParagraphs(document, normalized[position..match.Index]);
            var code = match.Groups["code"].Value.TrimEnd();
            var language = match.Groups["language"].Value;
            var copyButton = new Button
            {
                Content = "Copiar código",
                Padding = new Thickness(9, 5, 9, 5),
                Tag = code,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            copyButton.Click += (_, _) => System.Windows.Clipboard.SetText((string)copyButton.Tag);

            var header = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(copyButton, Dock.Right);
            header.Children.Add(copyButton);
            header.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(language) ? "código" : language,
                Foreground = Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center
            });

            var panel = new StackPanel();
            panel.Children.Add(header);
            panel.Children.Add(new TextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 9, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            document.Blocks.Add(new BlockUIContainer(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(21, 24, 33)),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 6, 0, 8),
                Child = panel
            }));
            position = match.Index + match.Length;
        }
        AddPreviewParagraphs(document, normalized[position..]);
    }

    private static void AddPreviewParagraphs(FlowDocument document, string text)
    {
        foreach (var line in text.Trim('\n').Split('\n'))
        {
            var imageMatch = ImagePattern().Match(line.Trim());
            if (imageMatch.Success && TryResolveImage(imageMatch.Groups["path"].Value, out var imagePath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath);
                bitmap.EndInit();
                document.Blocks.Add(new BlockUIContainer(new Image
                {
                    Source = bitmap,
                    MaxWidth = 520,
                    MaxHeight = 320,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    ToolTip = imageMatch.Groups["alt"].Value
                })
                {
                    Margin = new Thickness(0, 6, 0, 8)
                });
                continue;
            }

            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            AddMarkdownInlines(paragraph.Inlines, line);
            document.Blocks.Add(paragraph);
        }
    }

    private static string BuildImageHtml(Match match)
    {
        var alt = match.Groups["alt"].Value;
        var relativePath = WebUtility.HtmlDecode(match.Groups["path"].Value);
        if (!TryResolveImage(relativePath, out var fullPath))
        {
            return $"[Imagem: {alt}]";
        }

        try
        {
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            var mediaType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            var base64 = Convert.ToBase64String(File.ReadAllBytes(fullPath));
            return $"<img alt=\"{alt}\" src=\"data:{mediaType};base64,{base64}\" " +
                   "style=\"max-width:100%;height:auto\">";
        }
        catch (IOException)
        {
            return $"[Imagem: {alt}]";
        }
    }

    private static bool TryResolveImage(string relativePath, out string fullPath)
    {
        try
        {
            var root = Path.GetFullPath(AppPaths.AssetsDirectory);
            var candidate = Path.GetFullPath(Path.Combine(
                AppPaths.BaseDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (candidate.StartsWith(
                    root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(candidate))
            {
                fullPath = candidate;
                return true;
            }
        }
        catch (Exception)
        {
            // Caminhos inválidos são exibidos apenas como texto alternativo.
        }

        fullPath = string.Empty;
        return false;
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

    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<path>assets/[^\s)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<path>assets/[^\s)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex EncodedImagePattern();

    [GeneratedRegex(
        @"(?:^|\n)```(?<language>[A-Za-z0-9_+#.-]*)[ \t]*\n(?<code>[\s\S]*?)\n```(?=\n|$)",
        RegexOptions.CultureInvariant)]
    private static partial Regex CodeFencePattern();
}
