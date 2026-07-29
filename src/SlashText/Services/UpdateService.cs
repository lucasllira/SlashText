using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace SlashText.Services;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string Message,
    string? Url);

public sealed class UpdateService
{
    private static readonly Uri LatestRelease =
        new("https://api.github.com/repos/lucasllira/SlashText/releases/latest");

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = Assembly.GetExecutingAssembly().GetName().Version ??
                      new Version(0, 0);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SlashDesk", current.ToString(3)));
        using var response = await client.GetAsync(LatestRelease, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new UpdateCheckResult(
                false,
                current.ToString(3),
                null,
                "Ainda não existe uma versão publicada no GitHub Releases.",
                "https://github.com/lucasllira/SlashText/actions");
        }
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var tag = document.RootElement.GetProperty("tag_name").GetString() ?? "0.0.0";
        var url = document.RootElement.TryGetProperty("html_url", out var link)
            ? link.GetString()
            : "https://github.com/lucasllira/SlashText/releases";
        var normalized = tag.TrimStart('v', 'V');
        var latest = Version.TryParse(normalized, out var parsed) ? parsed : current;
        var available = latest > current;
        return new UpdateCheckResult(
            available,
            current.ToString(3),
            latest.ToString(3),
            available
                ? $"SlashDesk {latest.ToString(3)} está disponível."
                : "Você já está usando a versão mais recente.",
            url);
    }
}
