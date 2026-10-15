using System.Net;
using System.Net.Http;
using SlashText.Services;

internal static class ReleaseTrustChecks
{
    internal static async Task RunAsync()
    {
        const string name = "SlashDesk-3.3.0-portable-win-x64.zip";
        const string origin = "https://github.com/lucasllira/SlashText/releases/download/v3.3.0/" + name;
        const string cdn = "https://release-assets.githubusercontent.com/github-production-release-asset/123/456?token=fixture";
        const string legacyCdn = "https://objects.githubusercontent.com/github-production-release-asset-123/456?token=fixture";
        var checks = 0;
        void Require(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        ReleaseDownloadPolicy.ValidateAsset(origin, "3.3.0", name); checks++;
        ReleaseDownloadPolicy.ValidateAsset(origin + ".sha256", "3.3.0", name + ".sha256"); checks++;
        ReleaseDownloadPolicy.ValidateAsset(origin.Replace("3.3.0", "3.2.0"), "3.2.0", name.Replace("3.3.0", "3.2.0")); checks++;
        foreach (var invalid in new[]
        {
            origin.Replace("https:", "http:"), origin.Replace("github.com", "github.com.attacker.invalid"),
            origin.Replace("github.com", "github.com:444"), origin.Replace("github.com", "user@github.com"),
            origin.Replace("lucasllira", "other"), origin.Replace("SlashText", "Other"),
            origin.Replace("/v3.3.0/", "/v3.2.0/"), origin + ".other", origin + "?download=1", origin + "#fragment",
            origin.Replace("github.com", "raw.githubusercontent.com"), cdn, "not a URL"
        })
        {
            var rejected = false;
            try { ReleaseDownloadPolicy.ValidateAsset(invalid, "3.3.0", name); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Invalid initial asset origin accepted");
        }
        foreach (var status in new[] { 301, 302, 303, 307, 308 })
        foreach (var target in new[] { cdn, legacyCdn })
        {
            using var handler = new Handler((request, call) => call == 1 ? Redirect(status, target) : new(HttpStatusCode.OK));
            using var client = new HttpClient(handler);
            using var result = await ReleaseDownloadPolicy.GetAsync(client, new(origin), CancellationToken.None);
            Require(result.IsSuccessStatusCode && handler.Calls == 2, "Official CDN redirect failed");
        }
        foreach (var target in new[]
        {
            "https://attacker.invalid/payload", "http://release-assets.githubusercontent.com/github-production-release-asset/123/456",
            cdn.Replace(".com/", ".com.attacker.invalid/"), cdn.Replace(".com/", ".com:444/"),
            cdn.Replace("https://", "https://user@"), cdn + "#fragment", cdn.Replace("github-production-release-asset", "arbitrary"),
            "https://raw.githubusercontent.com/lucasllira/SlashText/main/SlashDesk.exe", origin.Replace("lucasllira", "other"),
            "/other/repository/releases/download/v3.3.0/a.zip", "//attacker.invalid/asset", "file:///C:/inert"
        })
        {
            using var handler = new Handler((_, _) => Redirect(302, target));
            using var client = new HttpClient(handler);
            var rejected = false;
            try { using var result = await ReleaseDownloadPolicy.GetAsync(client, new(origin), CancellationToken.None); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected && handler.Calls == 1, "Untrusted redirect must fail before requesting its destination");
        }
        using (var handler = new Handler((_, _) => Redirect(302, cdn)))
        using (var client = new HttpClient(handler))
        {
            var rejected = false;
            try { using var result = await ReleaseDownloadPolicy.GetAsync(client, new(origin), CancellationToken.None); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected && handler.Calls == ReleaseDownloadPolicy.MaximumRedirects + 1, "Redirect loop is not bounded");
        }
        using (var handler = new Handler((_, _) => new(HttpStatusCode.Redirect)))
        using (var client = new HttpClient(handler))
        {
            var rejected = false;
            try { using var result = await ReleaseDownloadPolicy.GetAsync(client, new(origin), CancellationToken.None); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Redirect without Location accepted");
        }
        using (var handler = new Handler((_, _) => new(HttpStatusCode.OK) { RequestMessage = new(HttpMethod.Get, "https://attacker.invalid/inert") }))
        using (var client = new HttpClient(handler))
        {
            var rejected = false;
            try { using var result = await ReleaseDownloadPolicy.GetAsync(client, new(origin), CancellationToken.None); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Automatically followed redirect accepted");
        }
        using (var handler = new Handler((_, _) => new(HttpStatusCode.NotFound)))
        using (var client = new HttpClient(handler))
        {
            var rejected = false;
            try { using var result = await ReleaseDownloadPolicy.GetAsync(client, new(origin), CancellationToken.None); }
            catch (HttpRequestException) { rejected = true; }
            Require(rejected, "HTTP failure accepted");
        }
        using (var handler = new Handler((_, _) => new(HttpStatusCode.OK)))
        using (var client = new HttpClient(handler))
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            var rejected = false;
            try { using var result = await ReleaseDownloadPolicy.GetAsync(client, new(origin), canceled.Token); }
            catch (OperationCanceledException) { rejected = true; }
            Require(rejected && handler.Calls == 0, "Cancellation must prevent request");
        }
        Console.WriteLine($"Release download trust: PASS ({checks} assertions; synthetic HTTP only)");
    }

    private static HttpResponseMessage Redirect(int status, string location)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request, ++Calls));
        }
    }
}
