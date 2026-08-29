using System.Windows.Media.Imaging;

namespace SlashText.Services;

public sealed record NotoEmojiItem(string Value, string Name, string AssetName);

public static class NotoEmojiCatalog
{
    private const string ResourcePrefix = "SlashText.Assets.NotoEmoji.";

    public static IReadOnlyList<NotoEmojiItem> Items { get; } =
    [
        new("❤️", "Coração", "emoji_u2764.png"),
        new("⭐", "Estrela", "emoji_u2b50.png"),
        new("❓", "Interrogação", "emoji_u2753.png"),
        new("✔️", "Confirmação", "emoji_u2714.png"),
        new("❌", "Erro", "emoji_u274c.png"),
        new("🔥", "Fogo", "emoji_u1f525.png"),
        new("👍", "Positivo", "emoji_u1f44d.png"),
        new("👎", "Negativo", "emoji_u1f44e.png"),
        new("👏", "Palmas", "emoji_u1f44f.png"),
        new("🙌", "Comemoração", "emoji_u1f64c.png"),
        new("👀", "Olhos", "emoji_u1f440.png"),
        new("💯", "Cem", "emoji_u1f4af.png"),
        new("🙂", "Sorriso leve", "emoji_u1f642.png"),
        new("😟", "Preocupado", "emoji_u1f61f.png"),
        new("😘", "Beijo", "emoji_u1f618.png"),
        new("😍", "Apaixonado", "emoji_u1f60d.png"),
        new("😂", "Rindo com lágrimas", "emoji_u1f602.png"),
        new("😭", "Chorando", "emoji_u1f62d.png"),
        new("😀", "Sorridente", "emoji_u1f600.png"),
        new("😃", "Muito sorridente", "emoji_u1f603.png"),
        new("😄", "Sorriso aberto", "emoji_u1f604.png"),
        new("😁", "Sorriso largo", "emoji_u1f601.png"),
        new("😆", "Riso", "emoji_u1f606.png"),
        new("😅", "Alívio", "emoji_u1f605.png"),
        new("🤣", "Gargalhada", "emoji_u1f923.png"),
        new("😉", "Piscando", "emoji_u1f609.png"),
        new("😮", "Surpreso", "emoji_u1f62e.png"),
        new("🤩", "Olhos de estrela", "emoji_u1f929.png"),
        new("🥳", "Festa", "emoji_u1f973.png"),
        new("🙃", "De cabeça para baixo", "emoji_u1f643.png"),
        new("😊", "Feliz", "emoji_u1f60a.png"),
        new("🥲", "Sorriso com lágrima", "emoji_u1f972.png"),
        new("🥹", "Emocionado", "emoji_u1f979.png"),
        new("😴", "Dormindo", "emoji_u1f634.png"),
        new("😎", "Óculos escuros", "emoji_u1f60e.png"),
        new("🎉", "Confetes", "emoji_u1f389.png")
    ];

    public static bool TryGet(string value, out NotoEmojiItem item)
    {
        var found = Items.FirstOrDefault(candidate => candidate.Value == value);
        if (found is null)
        {
            item = null!;
            return false;
        }
        item = found;
        return true;
    }

    public static BitmapSource CreateImageSource(string value)
    {
        if (!TryGet(value, out var item))
        {
            item = Items[0];
        }
        using var stream = Open(item);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static System.Drawing.Bitmap CreateBitmap(string value)
    {
        if (!TryGet(value, out var item))
        {
            item = Items[0];
        }
        using var stream = Open(item);
        using var source = new System.Drawing.Bitmap(stream);
        return new System.Drawing.Bitmap(source);
    }

    public static bool HasAsset(NotoEmojiItem item) =>
        typeof(NotoEmojiCatalog).Assembly.GetManifestResourceInfo(
            ResourcePrefix + item.AssetName) is not null;

    private static Stream Open(NotoEmojiItem item) =>
        typeof(NotoEmojiCatalog).Assembly.GetManifestResourceStream(
            ResourcePrefix + item.AssetName)
        ?? throw new InvalidOperationException($"Recurso Noto Emoji ausente: {item.AssetName}");
}
