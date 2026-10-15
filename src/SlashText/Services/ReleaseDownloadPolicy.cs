using System.IO;
using System.Net;
using System.Net.Http;

namespace SlashText.Services;

internal static class ReleaseDownloadPolicy
{
    internal const int MaximumRedirects = 3;

    internal static void ValidateAsset(string url, string version, string name)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsHttps(uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Length != 0 ||
            !uri.AbsolutePath.Equals($"/{UpdateService.Repository}/releases/download/v{version}/{name}", StringComparison.Ordinal))
            throw InvalidOrigin();
    }

    // Production callers must disable HttpClientHandler's automatic redirects:
    // inspect each Location before issuing the next request, never after download.
    internal static async Task<HttpResponseMessage> GetAsync(
        HttpClient client, Uri initial, CancellationToken cancellationToken)
    {
        var current = initial;
        for (var hop = 0; ; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            try
            {
                if (response.RequestMessage?.RequestUri is { } effective && effective != current)
                    throw InvalidOrigin();
                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                    HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    if (hop >= MaximumRedirects || response.Headers.Location is not { } location)
                        throw InvalidOrigin();
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (!IsHttps(next) || !(next == initial || IsReleaseCdn(next)))
                        throw InvalidOrigin();
                    current = next;
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    return response;
                }
            }
            catch
            {
                response.Dispose();
                throw;
            }
            response.Dispose();
        }
    }

    private static bool IsHttps(Uri uri) => uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 &&
        uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;

    private static bool IsReleaseCdn(Uri uri) =>
        (uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) &&
         uri.AbsolutePath.StartsWith("/github-production-release-asset/", StringComparison.Ordinal)) ||
        (uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) &&
         uri.AbsolutePath.StartsWith("/github-production-release-asset-", StringComparison.Ordinal));

    private static InvalidDataException InvalidOrigin() =>
        new(UiText.Get("A URL do artefato não pertence ao GitHub via HTTPS."));
}
