using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SlashText.Services;

internal sealed record ThirdPartyNotice(string Id, string Name, string Version, string License,
    string Source, string Entry, string Sha256, int Length)
{
    public string DisplayName => $"{Name} · {Version} · {License}";
}

internal sealed class ThirdPartyNotices
{
    private static readonly Lazy<ThirdPartyNotices> Current = new(LoadEmbedded);
    private readonly Dictionary<string, string> _texts;
    internal IReadOnlyList<ThirdPartyNotice> Documents { get; }
    internal string ManifestJson { get; }
    private ThirdPartyNotices(IReadOnlyList<ThirdPartyNotice> documents, Dictionary<string, string> texts, string manifest)
    { Documents = documents; _texts = texts; ManifestJson = manifest; }
    internal static ThirdPartyNotices Load() => Current.Value;
    internal string Text(ThirdPartyNotice document) => _texts[document.Id];

    private static ThirdPartyNotices LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var manifest = assembly.GetManifestResourceStream("SlashText.ThirdParty.catalog.json") ?? throw new InvalidDataException("Notice catalog missing.");
        using var zip = assembly.GetManifestResourceStream("SlashText.ThirdParty.notices.zip") ?? throw new InvalidDataException("Notice texts missing.");
        return Read(manifest, zip);
    }

    internal static ThirdPartyNotices Read(Stream manifest, Stream zip)
    {
        if (manifest.CanSeek && manifest.Length > 1_000_000) throw new InvalidDataException("Notice manifest budget exceeded.");
        using var reader = new StreamReader(manifest, Encoding.UTF8, true, 4096, leaveOpen: true);
        var json = reader.ReadToEnd();
        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unknown notices schema.");
        var documents = parsed.RootElement.GetProperty("documents").Deserialize<List<ThirdPartyNotice>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty notice catalog.");
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        if (documents.Count is < 1 or > 128 || archive.Entries.Count != documents.Count) throw new InvalidDataException("Notice count differs.");
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var document in documents)
        {
            if (string.IsNullOrEmpty(document.Id) || !names.Add(document.Entry) || document.Entry != document.Id + ".txt" ||
                document.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-') || document.Length is < 20 or > 8_000_000)
                throw new InvalidDataException("Invalid notice identity or length.");
            total += document.Length;
            if (total > 16_000_000) throw new InvalidDataException("Notice budget exceeded.");
            var entries = archive.Entries.Where(e => e.FullName == document.Entry).ToArray();
            if (entries.Length != 1 || entries[0].Length != document.Length) throw new InvalidDataException("Notice entry differs.");
            using var source = entries[0].Open();
            var bytes = new byte[document.Length];
            source.ReadExactly(bytes);
            if (source.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != document.Sha256)
                throw new InvalidDataException("Notice digest differs.");
            texts.Add(document.Id, new UTF8Encoding(false, true).GetString(bytes));
        }
        return new ThirdPartyNotices(documents.AsReadOnly(), texts, json);
    }
}
