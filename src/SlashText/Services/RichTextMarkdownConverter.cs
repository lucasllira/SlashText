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
        AddMarkdownBlocks(editor.Document, normalized, format, includeImages: false);

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
            switch (block)
            {
                case Paragraph paragraph:
                    var paragraphText = format == SnippetFormat.Plain
                        ? new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n')
                        : SerializeInlines(paragraph.Inlines);
                    lines.Add(format == SnippetFormat.Markdown
                        ? ApplyParagraphAlignment(paragraphText, paragraph.TextAlignment)
                        : paragraphText);
                    break;
                case System.Windows.Documents.List list:
                    SerializeList(lines, list, format);
                    break;
                case Table table:
                    SerializeTable(lines, table, format);
                    break;
            }
        }

        return string.Join("\n", lines).TrimEnd();
    }

    public static string ToPlainText(string markdown)
    {
        var value = markdown;
        value = CodeFencePattern().Replace(value, match => match.Groups["code"].Value.TrimEnd());
        value = ImagePattern().Replace(value, match => $"[Imagem: {match.Groups["alt"].Value}]");
        value = RichSpanPattern().Replace(value, match => match.Groups["text"].Value);
        value = ParagraphPattern().Replace(value, match => match.Groups["text"].Value);
        value = LinkPattern().Replace(value, match => match.Groups["text"].Value);
        value = TableSeparatorLinePattern().Replace(value, string.Empty);
        value = ListPrefixPattern().Replace(value, string.Empty);
        value = TableLinePattern().Replace(
            value,
            match => string.Join(
                "\t",
                SplitTableRow(match.Value)));
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
            var prefix = match.Value.StartsWith('\n') ? "\n" : string.Empty;
            return $"{prefix}\u001E_CODE_{codeBlocks.Count - 1}_\u001E";
        });

        return "<div style=\"font-family:'Segoe UI',sans-serif;font-size:11pt\">" +
               MarkdownBlocksToHtml(markdown, codeBlocks) +
               "</div>";
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
        AddMarkdownBlocks(document, normalized, format, includeImages: true);
    }

    private static void AddMarkdownBlocks(
        FlowDocument document,
        string normalized,
        SnippetFormat format,
        bool includeImages)
    {
        var lines = normalized.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (format == SnippetFormat.Markdown &&
                index + 1 < lines.Length &&
                IsTableRow(lines[index]) &&
                TableSeparatorLinePattern().IsMatch(lines[index + 1]))
            {
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 6) };
                var rows = new List<string[]> { SplitTableRow(lines[index]) };
                index += 2;
                while (index < lines.Length && IsTableRow(lines[index]))
                {
                    rows.Add(SplitTableRow(lines[index]));
                    index++;
                }
                index--;

                var columnCount = rows.Max(row => row.Length);
                for (var column = 0; column < columnCount; column++)
                {
                    table.Columns.Add(new TableColumn());
                }

                var group = new TableRowGroup();
                table.RowGroups.Add(group);
                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var row = new TableRow();
                    group.Rows.Add(row);
                    for (var column = 0; column < columnCount; column++)
                    {
                        var paragraph = new Paragraph();
                        AddMarkdownInlines(
                            paragraph.Inlines,
                            column < rows[rowIndex].Length ? rows[rowIndex][column] : string.Empty);
                        var cell = new TableCell(paragraph)
                        {
                            BorderBrush = Brushes.Gray,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(7),
                            FontWeight = rowIndex == 0 ? FontWeights.SemiBold : FontWeights.Normal
                        };
                        row.Cells.Add(cell);
                    }
                }
                document.Blocks.Add(table);
                continue;
            }

            if (format == SnippetFormat.Markdown &&
                TryReadListItem(lines[index], out var ordered, out _))
            {
                var list = new System.Windows.Documents.List
                {
                    MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(20, 4, 0, 6)
                };
                while (index < lines.Length &&
                       TryReadListItem(lines[index], out var currentOrdered, out var itemText) &&
                       currentOrdered == ordered)
                {
                    var paragraph = new Paragraph();
                    AddMarkdownInlines(paragraph.Inlines, itemText);
                    list.ListItems.Add(new ListItem(paragraph));
                    index++;
                }
                index--;
                document.Blocks.Add(list);
                continue;
            }

            var line = lines[index];
            if (includeImages &&
                format == SnippetFormat.Markdown &&
                ImagePattern().Match(line.Trim()) is { Success: true } imageMatch &&
                TryResolveImage(imageMatch.Groups["path"].Value, out var imagePath))
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

            var alignment = TextAlignment.Left;
            if (format == SnippetFormat.Markdown &&
                ParagraphPattern().Match(line) is { Success: true } paragraphMatch)
            {
                line = paragraphMatch.Groups["text"].Value;
                alignment = paragraphMatch.Groups["align"].Value.ToLowerInvariant() switch
                {
                    "center" => TextAlignment.Center,
                    "right" => TextAlignment.Right,
                    "justify" => TextAlignment.Justify,
                    _ => TextAlignment.Left
                };
            }

            var block = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 4),
                TextAlignment = alignment
            };
            if (format == SnippetFormat.Plain)
            {
                block.Inlines.Add(new Run(line));
            }
            else
            {
                AddMarkdownInlines(block.Inlines, line);
            }
            document.Blocks.Add(block);
        }
    }

    private static void SerializeList(
        ICollection<string> lines,
        System.Windows.Documents.List list,
        SnippetFormat format)
    {
        var ordered = list.MarkerStyle is TextMarkerStyle.Decimal or
            TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin or
            TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;
        var number = 1;
        foreach (var item in list.ListItems)
        {
            foreach (var paragraph in item.Blocks.OfType<Paragraph>())
            {
                var value = format == SnippetFormat.Plain
                    ? new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n')
                    : SerializeInlines(paragraph.Inlines);
                lines.Add(format == SnippetFormat.Markdown
                    ? $"{(ordered ? $"{number++}." : "-")} {value}"
                    : value);
            }
        }
    }

    private static void SerializeTable(
        ICollection<string> lines,
        Table table,
        SnippetFormat format)
    {
        var rows = table.RowGroups
            .SelectMany(group => group.Rows)
            .Select(row => row.Cells
                .Select(cell =>
                {
                    var paragraph = cell.Blocks.OfType<Paragraph>().FirstOrDefault();
                    if (paragraph is null)
                    {
                        return string.Empty;
                    }
                    return format == SnippetFormat.Plain
                        ? new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n')
                        : SerializeInlines(paragraph.Inlines);
                })
                .ToArray())
            .ToList();
        if (rows.Count == 0)
        {
            return;
        }

        if (format == SnippetFormat.Plain)
        {
            foreach (var row in rows)
            {
                lines.Add(string.Join('\t', row));
            }
            return;
        }

        lines.Add("| " + string.Join(" | ", rows[0].Select(EscapeTableCell)) + " |");
        lines.Add("| " + string.Join(" | ", rows[0].Select(_ => "---")) + " |");
        foreach (var row in rows.Skip(1))
        {
            lines.Add("| " + string.Join(" | ", row.Select(EscapeTableCell)) + " |");
        }
    }

    private static string ApplyParagraphAlignment(string value, TextAlignment alignment) =>
        alignment switch
        {
            TextAlignment.Center => $"<p align=\"center\">{value}</p>",
            TextAlignment.Right => $"<p align=\"right\">{value}</p>",
            TextAlignment.Justify => $"<p align=\"justify\">{value}</p>",
            _ => value
        };

    private static bool TryReadListItem(string line, out bool ordered, out string text)
    {
        var match = ListLinePattern().Match(line);
        ordered = match.Success && match.Groups["number"].Success;
        text = match.Success ? match.Groups["text"].Value : string.Empty;
        return match.Success;
    }

    private static bool IsTableRow(string line) =>
        line.TrimStart().StartsWith('|') && line.TrimEnd().EndsWith('|');

    private static string[] SplitTableRow(string line) =>
        line.Trim().Trim('|')
            .Split('|')
            .Select(item => item.Trim().Replace("\\|", "|", StringComparison.Ordinal))
            .ToArray();

    private static string EscapeTableCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);

    private static string MarkdownBlocksToHtml(string markdown, IReadOnlyList<string> codeBlocks)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var html = new StringBuilder();
        for (var index = 0; index < lines.Length; index++)
        {
            var codeMatch = CodePlaceholderPattern().Match(lines[index]);
            if (codeMatch.Success &&
                int.TryParse(codeMatch.Groups["index"].Value, out var codeIndex) &&
                codeIndex >= 0 &&
                codeIndex < codeBlocks.Count)
            {
                html.Append(codeBlocks[codeIndex]);
                continue;
            }

            if (index + 1 < lines.Length &&
                IsTableRow(lines[index]) &&
                TableSeparatorLinePattern().IsMatch(lines[index + 1]))
            {
                html.Append("<table style=\"border-collapse:collapse;margin:6px 0\">");
                var header = SplitTableRow(lines[index]);
                html.Append("<tr>");
                foreach (var cell in header)
                {
                    html.Append("<th style=\"border:1px solid #9aa4b2;padding:6px 8px;text-align:left\">")
                        .Append(InlineToHtml(cell))
                        .Append("</th>");
                }
                html.Append("</tr>");
                index += 2;
                while (index < lines.Length && IsTableRow(lines[index]))
                {
                    ço-¢G§²ÚîÆ­yÕps["align"].Value)
                    .Append("\">")
                    .Append(InlineToHtml(paragraphMatch.Groups["text"].Value))
                    .Append("</div>");
            }
            else if (lines[index].Length == 0)
            {
                html.Append("<br>");
            }
            else
            {
                html.Append("<div>").Append(InlineToHtml(lines[index])).Append("</div>");
            }
        }
        return html.ToString();
    }

    private static string InlineToHtml(string value)
    {
        var spans = new List<(string Style, string Text)>();
        value = RichSpanPattern().Replace(value, match =>
        {
            spans.Add((SanitizeStyle(match.Groups["style"].Value), match.Groups["text"].Value));
            return $"\u001E_SPAN_{spans.Count - 1}_\u001E";
        });

        var escaped = WebUtility.HtmlEncode(value);
        escaped = EncodedLinkPattern().Replace(
            escaped,
            match => $"<a href=\"{match.Groups["url"].Value}\">{match.Groups["text"].Value}</a>");
        escaped = EncodedBoldPattern().Replace(escaped, "<strong>${text}</strong>");
        escaped = EncodedUnderlinePattern().Replace(escaped, "<u>${text}</u>");
        escaped = EncodedItalicPattern().Replace(escaped, "<em>${text}</em>");
        for (var index = 0; index < spans.Count; index++)
        {
            escaped = escaped.Replace(
                WebUtility.HtmlEncode($"\u001E_SPAN_{index}_\u001E"),
                $"<span style=\"{spans[index].Style}\">{InlineToHtml(spans[index].Text)}</span>",
                StringComparison.Ordinal);
        }
        return escaped;
    }

    private static string SanitizeStyle(string style) =>
        string.Join(
            ";",
            style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => Regex.IsMatch(
                    item,
                    @"^(?:color|background-color):#[0-9A-Fa-f]{6}$|^font-family:[A-Za-z0-9 ,'-]+$|^font-size:[0-9.]+px$",
                    RegexOptions.CultureInvariant)));

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
                AppPaths.DataDirectory,
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
            // Caminhos invÃ¡lidos sÃ£o exibidos apenas como texto alternativo.
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
            else if (match.Groups["styleText"].Success)
            {
                var span = new Span();
                AddMarkdownInlines(span.Inlines, match.Groups["styleText"].Value);
                ApplyTextStyle(span, match.Groups["style"].Value);
                target.Add(span);
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
                SerializeSpan(builder, span);
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

        var bold = run.ReadLocalValue(TextElement.FontWeightProperty) is FontWeight weight &&
                   weight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        var italic = run.ReadLocalValue(TextElement.FontStyleProperty) is FontStyle style &&
                     style == FontStyles.Italic;
        var underline = run.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations &&
                        decorations.Contains(TextDecorations.Underline[0]);
        var styles = StyleDeclarations(run);

        if (styles.Count > 0)
        {
            builder.Append("<span style=\"").Append(string.Join(';', styles)).Append("\">");
        }
        if (bold) builder.Append("**");
        if (italic) builder.Append('*');
        if (underline) builder.Append("__");
        builder.Append(value);
        if (underline) builder.Append("__");
        if (italic) builder.Append('*');
        if (bold) builder.Append("**");
        if (styles.Count > 0) builder.Append("</span>");
    }

    private static void SerializeSpan(StringBuilder builder, Span span)
    {
        var inner = new StringBuilder();
        foreach (var child in span.Inlines)
        {
            SerializeInline(inner, child);
        }

        var styles = StyleDeclarations(span);
        var bold = span is Bold ||
                   span.ReadLocalValue(TextElement.FontWeightProperty) is FontWeight weight &&
                   weight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight();
        var italic = span is Italic ||
                     span.ReadLocalValue(TextElement.FontStyleProperty) is FontStyle fontStyle &&
                     fontStyle == FontStyles.Italic;
        var underline = span is Underline ||
                        span.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations &&
                        decorations.Contains(TextDecorations.Underline[0]);

        if (styles.Count > 0)
        {
            builder.Append("<span style=\"").Append(string.Join(';', styles)).Append("\">");
        }
        if (bold) builder.Append("**");
        if (italic) builder.Append('*');
        if (underline) builder.Append("__");
        builder.Append(inner);
        if (underline) builder.Append("__");
        if (italic) builder.Append('*');
        if (bold) builder.Append("**");
        if (styles.Count > 0) builder.Append("</span>");
    }

    private static List<string> StyleDeclarations(TextElement element)
    {
        var styles = new List<string>();
        if (element.ReadLocalValue(TextElement.ForegroundProperty) is SolidColorBrush foreground)
        {
            styles.Add($"color:#{foreground.Color.R:X2}{foreground.Color.G:X2}{foreground.Color.B:X2}");
        }
        if (element.ReadLocalValue(TextElement.BackgroundProperty) is SolidColorBrush background &&
            background.Color.A > 0)
        {
            styles.Add($"background-color:#{background.Color.R:X2}{background.Color.G:X2}{background.Color.B:X2}");
        }
        if (element.ReadLocalValue(TextElement.FontFamilyProperty) is FontFamily family)
        {
            styles.Add($"font-family:{family.Source}");
        }
        if (element.ReadLocalValue(TextElement.FontSizeProperty) is double size)
        {
            styles.Add(
                $"font-size:{size.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}px");
        }
        return styles;
    }

    private static void ApplyTextStyle(TextElement element, string style)
    {
        foreach (var declaration in style.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = declaration[..separator].Trim().ToLowerInvariant();
            var value = declaration[(separator + 1)..].Trim();
            try
            {
                switch (name)
                {
                    case "color":
                        element.Foreground = (Brush)new BrushConverter().ConvertFromString(value)!;
                        break;
                    case "background-color":
                        element.Background = (Brush)new BrushConverter().ConvertFromString(value)!;
                        break;
                    case "font-family":
                        element.FontFamily = new FontFamily(value);
                        break;
                    case "font-size" when value.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
                                          double.TryParse(
                                              value[..^2],
                                              System.Globalization.NumberStyles.Float,
                                              System.Globalization.CultureInfo.InvariantCulture,
                                              out var size):
                        element.FontSize = size;
                        break;
                }
            }
            catch (Exception) when (name is "color" or "background-color" or "font-family")
            {
                // Uma declaraÃ§Ã£o invÃ¡lida Ã© ignorada, preservando o texto.
            }
        }
    }

    [GeneratedRegex(
        @"<span\s+style=""(?<style>[^""]+)"">(?<styleText>.*?)</span>|\*\*(?<bold>.+?)\*\*|\*(?<italic>.+?)\*|__(?<underline>.+?)__|\[(?<linkText>.+?)\]\((?<url>https?://[^\s)]+)\)|<span\s+style=""color:(?<color>#[0-9A-Fa-f]{6})"">(?<colorText>.*?)</span>",
        RegexOptions.IgnoreCase)]
    private static partial Regex InlinePattern();

    [GeneratedRegex(@"<span\s+style=""(?<style>[^""]+)"">(?<text>.*?)</span>", RegexOptions.IgnoreCase)]
    private static partial Regex RichSpanPattern();

    [GeneratedRegex(@"<p\s+align=""(?<align>left|center|right|justify)"">(?<text>.*?)</p>", RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphPattern();

    [GeneratedRegex(@"\[(?<text>.*?)\]\((?<url>https?://[^\s)]+)\)")]
    private static partial Regex LinkPattern();

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

    [GeneratedRegex(@"^(?:(?<number>\d+)\.|(?<bullet>-))\s+(?<text>.*)$")]
    private static partial Regex ListLinePattern();

    [GeneratedRegex(@"(?m)^(?:(?:\d+\.)|-)\s+")]
    private static partial Regex ListPrefixPattern();

    [GeneratedRegex(@"^\s*\|(?:\s*:?-{3,}:?\s*\|)+\s*$", RegexOptions.Multiline)]
    private static partial Regex TableSeparatorLinePattern();

    [GeneratedRegex(@"(?m)^\s*\|.*\|\s*$")]
    private static partial Regex TableLinePattern();

    [GeneratedRegex(@"^\u001E_CODE_(?<index>\d+)_\u001E$")]
    private static partial Regex CodePlaceholderPattern();
}
