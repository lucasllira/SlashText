using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SlashText.Models;

namespace SlashText.Services;

public sealed partial class SnippetMarkdownRepository
{
    private const string Header =
        "# Meus atalhos\n\n<!-- Arquivo gerado pelo SlashText. A edição manual é opcional. -->\n\n";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _filePath;
    private readonly string _backupDirectory;

    public SnippetMarkdownRepository(string? filePath = null, string? backupDirectory = null)
    {
        _filePath = filePath ?? AppPaths.SnippetsFile;
        _backupDirectory = backupDirectory ?? AppPaths.BackupsDirectory;
    }

    public async Task<IReadOnlyList<Snippet>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            await SaveAsync([], cancellationToken);
            return [];
        }

        var markdown = await File.ReadAllTextAsync(_filePath, cancellationToken);
        var snippets = new List<Snippet>();

        foreach (Match match in SnippetPattern().Matches(markdown))
        {
            var metadata = JsonSerializer.Deserialize<SnippetMetadata>(
                match.Groups["metadata"].Value,
                JsonOptions) ?? throw new InvalidDataException("Metadados de atalho inválidos.");

            var snippet = new Snippet
            {
                Id = metadata.Id,
                Name = metadata.Name,
                Trigger = match.Groups["trigger"].Value.Trim(),
                Category = metadata.Category,
                Format = metadata.Format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
                    ? SnippetFormat.Markdown
                    : SnippetFormat.Plain,
                Enabled = metadata.Enabled,
                ConfirmKeys = metadata.ConfirmKeys,
                Content = match.Groups["content"].Value.Replace("\r\n", "\n").TrimEnd()
            };

            Validate(snippet, snippets);
            snippets.Add(snippet);
        }

        if (markdown.Contains("<!-- slashtext:", StringComparison.Ordinal) && snippets.Count == 0)
        {
            throw new InvalidDataException("O arquivo contém atalhos, mas nenhum pôde ser interpretado.");
        }

        return snippets;
    }

    public async Task SaveAsync(
        IEnumerable<Snippet> snippets,
        CancellationToken cancellationToken = default)
    {
        var ordered = snippets
            .OrderBy(item => item.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Trigger, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            Validate(ordered[index], ordered.Take(index));
        }

        var markdown = Serialize(ordered);
        var directory = Path.GetDirectoryName(_filePath) ?? AppPaths.BaseDirectory;
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(_backupDirectory);

        if (File.Exists(_filePath))
        {
            var backupName = $"snippets-{DateTime.Now:yyyyMMdd-HHmmssfff}.md";
            File.Copy(_filePath, Path.Combine(_backupDirectory, backupName), overwrite: false);
        }

        var temporaryFile = Path.Combine(directory, $".snippets-{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryFile,
                markdown,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            File.Move(temporaryFile, _filePath, overwrite: true);
            PruneBackups(20);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private static string Serialize(IEnumerable<Snippet> snippets)
    {
        var builder = new StringBuilder(Header);

        foreach (var snippet in snippets)
        {
            var metadata = new SnippetMetadata(
                snippet.Id,
                snippet.Name,
                snippet.Category,
                snippet.Format == SnippetFormat.Markdown ? "markdown" : "plain",
                snippet.Enabled,
                snippet.ConfirmKeys);

            var fenceLength = Math.Max(3, LongestBacktickRun(snippet.Content) + 1);
            var fence = new string('`', fenceLength);

            builder.Append("## ").AppendLine(snippet.Trigger);
            builder.Append("<!-- slashtext:")
                .Append(JsonSerializer.Serialize(metadata, JsonOptions))
                .AppendLine(" -->");
            builder.Append(fence)
                .AppendLine(snippet.Format == SnippetFormat.Markdown ? "markdown" : "text");
            builder.AppendLine(snippet.Content.TrimEnd());
            builder.AppendLine(fence);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void Validate(Snippet snippet, IEnumerable<Snippet> existing)
    {
        if (!TriggerPattern().IsMatch(snippet.Trigger))
        {
            throw new InvalidDataException(
                $"O atalho '{snippet.Trigger}' deve começar com / e usar letras, números, hífen ou sublinhado.");
        }

        if (string.IsNullOrWhiteSpace(snippet.Name))
        {
            throw new InvalidDataException($"O atalho '{snippet.Trigger}' precisa de um nome.");
        }

        if (existing.Any(item =>
                item.Trigger.Equals(snippet.Trigger, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException($"O atalho '{snippet.Trigger}' está duplicado.");
        }
    }

    private void PruneBackups(int keep)
    {
        foreach (var file in new DirectoryInfo(_backupDirectory)
                     .GetFiles("snippets-*.md")
                     .OrderByDescending(file => file.CreationTimeUtc)
                     .Skip(keep))
        {
            file.Delete();
        }
    }

    private static int LongestBacktickRun(string value)
    {
        var longest = 0;
        var current = 0;

        foreach (var character in value)
        {
            current = character == '`' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }

    [GeneratedRegex(
        @"(?ms)^##[ \t]+(?<trigger>/[A-Za-zÀ-ÿ0-9_-]+)[ \t]*\r?\n<!-- slashtext:(?<metadata>\{[^\r\n]+\}) -->[ \t]*\r?\n(?<fence>`{3,})(?:markdown|text)\r?\n(?<content>.*?)\r?\n\k<fence>[ \t]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SnippetPattern();

    [GeneratedRegex(@"^/[A-Za-zÀ-ÿ0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TriggerPattern();

    private sealed record SnippetMetadata(
        Guid Id,
        string Name,
        string Category,
        string Format,
        bool Enabled,
        List<string> ConfirmKeys);
}

