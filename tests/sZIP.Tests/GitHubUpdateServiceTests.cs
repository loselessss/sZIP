using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using sZIP.Application;

namespace sZIP.Tests;

public sealed class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("en-US", "English notes")]
    [InlineData("ko-KR", "한국어 노트")]
    [InlineData("fr-FR", "English notes")]
    public void ReleaseNotesUseTheConfiguredLanguage(string language, string expected)
    {
        var notes = "<!-- sZIP:lang=en -->\nEnglish notes\n"
            + "<!-- sZIP:lang=ko -->\n한국어 노트";

        Assert.Equal(expected, ReleaseNotesLocalization.Select(notes, language));
    }

    [Fact]
    public void ReleaseNotesWithoutLanguageMarkersRemainCompatible()
    {
        Assert.Equal("Legacy notes", ReleaseNotesLocalization.Select("  Legacy notes  ", "ko-KR"));
    }

    [Fact]
    public async Task CheckSelectsKoreanReleaseNotes()
    {
        var body = "<!-- sZIP:lang=en -->\nEnglish notes\n"
            + "<!-- sZIP:lang=ko -->\n한국어 노트";
        using var client = new HttpClient(new StubHandler(_ => JsonResponse(
            ReleaseJson("1.4.0", new string('a', 64), 10, body))));
        using var service = new GitHubUpdateService(new ReleaseVersion(1, 3, 0), client,
            releaseNotesLanguage: "ko-KR");

        var update = await service.CheckAsync();

        Assert.Equal("한국어 노트", update!.ReleaseNotes);
    }

    [Fact]
    public async Task CheckSelectsVersionedInstallerAndDigest()
    {
        var installer = Encoding.ASCII.GetBytes("installer");
        var digest = Hash(installer);
        using var client = new HttpClient(new StubHandler(_ => JsonResponse(ReleaseJson("1.4.0", digest, installer.Length))));
        using var service = new GitHubUpdateService(new ReleaseVersion(1, 3, 0), client);

        var update = await service.CheckAsync();

        Assert.NotNull(update);
        Assert.Equal("1.4.0", update!.Version.ToString());
        Assert.Equal("sZIP_Setup_1.4.0.exe", update.Asset!.Name);
        Assert.Equal(digest, update.Asset.Sha256);
    }

    [Fact]
    public async Task CheckRejectsUntrustedAssetUrl()
    {
        var json = ReleaseJson("1.4.0", new string('a', 64), 10)
            .Replace("https://github.com/loselessss/sZIP/releases/download/", "https://example.com/");
        using var client = new HttpClient(new StubHandler(_ => JsonResponse(json)));
        using var service = new GitHubUpdateService(new ReleaseVersion(1, 3, 0), client);

        await Assert.ThrowsAsync<UpdateException>(() => service.CheckAsync());
    }

    [Fact]
    public async Task DownloadVerifiesHashAndReusesValidInstaller()
    {
        var installer = Encoding.ASCII.GetBytes("MZ verified installer");
        var digest = Hash(installer);
        var calls = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            calls++;
            return request.RequestUri!.Host == "api.github.com"
                ? JsonResponse(ReleaseJson("1.4.0", digest, installer.Length))
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) };
        }));
        var root = Path.Combine(Path.GetTempPath(), "sZIP-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var service = new GitHubUpdateService(new ReleaseVersion(1, 3, 0), client, root);
            var update = await service.CheckAsync();
            var first = await service.DownloadAsync(update!);
            var second = await service.DownloadAsync(update!);

            Assert.Equal(first, second);
            Assert.Equal(installer, File.ReadAllBytes(first));
            Assert.Equal(2, calls);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DownloadDeletesPartialFileOnDigestMismatch()
    {
        var expected = Encoding.ASCII.GetBytes("expected");
        var tampered = Encoding.ASCII.GetBytes("tampered");
        using var client = new HttpClient(new StubHandler(request =>
            request.RequestUri!.Host == "api.github.com"
                ? JsonResponse(ReleaseJson("1.4.0", Hash(expected), expected.Length))
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(tampered) }));
        var root = Path.Combine(Path.GetTempPath(), "sZIP-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var service = new GitHubUpdateService(new ReleaseVersion(1, 3, 0), client, root);
            var update = await service.CheckAsync();
            await Assert.ThrowsAsync<UpdateException>(() => service.DownloadAsync(update!));
            Assert.Empty(Directory.EnumerateFiles(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CleanupRemovesPartialOldAndDuplicateFutureInstallers()
    {
        var root = Path.Combine(Path.GetTempPath(), "sZIP-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "sZIP_Setup_1.2.0.exe"), "old");
            File.WriteAllText(Path.Combine(root, "sZIP_Setup_1.4.0.exe"), "future");
            File.WriteAllText(Path.Combine(root, "sZIP_Setup_1.5.0.exe"), "latest");
            File.WriteAllText(Path.Combine(root, "sZIP_Setup_1.6.0.exe.part"), "partial");
            using var service = new GitHubUpdateService(new ReleaseVersion(1, 3, 0),
                new HttpClient(new StubHandler(_ => throw new InvalidOperationException())), root);

            service.CleanupDownloads();

            Assert.Equal(new[] { "sZIP_Setup_1.5.0.exe" },
                Directory.EnumerateFiles(root).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string ReleaseJson(string version, string digest, int size, string body = "notes") =>
        "{\"tag_name\":\"v" + version + "\",\"name\":\"sZIP " + version
        + "\",\"body\":\"" + EscapeJson(body)
        + "\",\"html_url\":\"https://github.com/loselessss/sZIP/releases/tag/v"
        + version + "\",\"published_at\":\"2026-08-09T00:00:00Z\",\"assets\":[{\"name\":\"sZIP_Setup_"
        + version + ".exe\",\"browser_download_url\":\"https://github.com/loselessss/sZIP/releases/download/v"
        + version + "/sZIP_Setup_" + version + ".exe\",\"size\":" + size
        + ",\"digest\":\"sha256:" + digest + "\"}]}";

    private static string EscapeJson(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");

    private static string Hash(byte[] value)
    {
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(value)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(_response(request));
    }
}
