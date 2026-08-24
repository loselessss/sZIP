using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace sZIP.Application;

public class UpdateException : Exception
{
    public UpdateException(string message) : base(message) { }
    public UpdateException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class UpdateCancelledException : UpdateException
{
    public UpdateCancelledException() : base("업데이트 다운로드를 취소했습니다.") { }
}

public sealed class ReleaseAsset
{
    public ReleaseAsset(string name, Uri downloadUrl, long size, string sha256)
    {
        Name = name;
        DownloadUrl = downloadUrl;
        Size = size;
        Sha256 = sha256;
    }

    public string Name { get; }
    public Uri DownloadUrl { get; }
    public long Size { get; }
    public string Sha256 { get; }
}

public sealed class AvailableUpdate
{
    public AvailableUpdate(ReleaseVersion version, string tagName, string releaseName,
        string releaseNotes, Uri releaseUrl, string publishedAt, ReleaseAsset? asset)
    {
        Version = version;
        TagName = tagName;
        ReleaseName = releaseName;
        ReleaseNotes = releaseNotes;
        ReleaseUrl = releaseUrl;
        PublishedAt = publishedAt;
        Asset = asset;
    }

    public ReleaseVersion Version { get; }
    public string TagName { get; }
    public string ReleaseName { get; }
    public string ReleaseNotes { get; }
    public Uri ReleaseUrl { get; }
    public string PublishedAt { get; }
    public ReleaseAsset? Asset { get; }
}

public sealed class UpdateDownloadProgress
{
    public UpdateDownloadProgress(long completedBytes, long totalBytes, double bytesPerSecond)
    {
        CompletedBytes = completedBytes;
        TotalBytes = totalBytes;
        BytesPerSecond = bytesPerSecond;
    }

    public long CompletedBytes { get; }
    public long TotalBytes { get; }
    public double BytesPerSecond { get; }
}

public sealed class GitHubUpdateService : IDisposable
{
    public const string Repository = "loselessss/sZIP";
    private static readonly Uri LatestReleaseApi = new(
        "https://api.github.com/repos/loselessss/sZIP/releases/latest");
    private static readonly Regex InstallerNamePattern = new(
        @"^sZIP_Setup_(\d+\.\d+\.\d+)\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DigestPattern = new(
        @"^sha256:([0-9a-fA-F]{64})$", RegexOptions.CultureInvariant);
    private const int MaximumReleaseJsonBytes = 2 * 1024 * 1024;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string _downloadRoot;

    public GitHubUpdateService(ReleaseVersion currentVersion, HttpClient? client = null,
        string? downloadRoot = null)
    {
        CurrentVersion = currentVersion;
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        _client = client ?? CreateClient(currentVersion);
        _ownsClient = client is null;
        _downloadRoot = downloadRoot ?? Path.Combine(Path.GetTempPath(), "sZIP", "updates");
    }

    public ReleaseVersion CurrentVersion { get; }

    public async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("sZIP", CurrentVersion.ToString()));

        byte[] payload;
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            payload = await response.Content.ReadAsByteArrayAsync();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateException("GitHub 릴리스 확인 시간이 초과되었습니다.");
        }
        catch (HttpRequestException exception)
        {
            throw new UpdateException("GitHub 릴리스 정보를 확인하지 못했습니다.", exception);
        }

        if (payload.Length > MaximumReleaseJsonBytes)
        {
            throw new UpdateException("GitHub 릴리스 응답이 허용 크기를 초과했습니다.");
        }

        ReleaseResponse release;
        try
        {
            release = new JavaScriptSerializer().Deserialize<ReleaseResponse>(
                System.Text.Encoding.UTF8.GetString(payload))
                ?? throw new UpdateException("GitHub 릴리스 응답이 비어 있습니다.");
        }
        catch (InvalidOperationException exception)
        {
            throw new UpdateException("GitHub 릴리스 응답 형식이 올바르지 않습니다.", exception);
        }

        if (!ReleaseVersion.TryParseTag(release.tag_name, out var latestVersion))
        {
            throw new UpdateException("GitHub 릴리스 버전을 확인할 수 없습니다.");
        }
        if (latestVersion.CompareTo(CurrentVersion) <= 0)
        {
            return null;
        }
        if (!TryTrustedGitHubUrl(release.html_url, false, out var releaseUrl))
        {
            throw new UpdateException("GitHub 릴리스 주소를 신뢰할 수 없습니다.");
        }

        var releaseName = string.IsNullOrWhiteSpace(release.name)
            ? release.tag_name ?? string.Empty
            : release.name!;
        return new AvailableUpdate(latestVersion, release.tag_name ?? string.Empty,
            releaseName,
            release.body ?? string.Empty, releaseUrl!, release.published_at ?? string.Empty,
            SelectInstaller(release.assets, latestVersion));
    }

    private static ReleaseAsset? SelectInstaller(ReleaseAssetResponse[]? assets, ReleaseVersion version)
    {
        var expectedName = $"sZIP_Setup_{version}.exe";
        var item = assets?.FirstOrDefault(asset =>
            string.Equals(asset.name, expectedName, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return null;
        }
        if (Path.GetFileName(item.name) != item.name
            || !InstallerNamePattern.IsMatch(item.name ?? string.Empty)
            || !TryTrustedGitHubUrl(item.browser_download_url, true, out var downloadUrl))
        {
            throw new UpdateException("릴리스 설치 파일 정보가 안전하지 않습니다.");
        }

        var match = DigestPattern.Match(item.digest ?? string.Empty);
        return new ReleaseAsset(item.name!, downloadUrl!, Math.Max(0, item.size),
            match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty);
    }

    public async Task<string> DownloadAsync(AvailableUpdate update,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var asset = update.Asset ?? throw new UpdateException("이 릴리스에는 Windows 설치 파일이 없습니다.");
        if (string.IsNullOrEmpty(asset.Sha256))
        {
            throw new UpdateException("설치 파일의 SHA-256 정보가 없어 자동 설치할 수 없습니다.");
        }

        Directory.CreateDirectory(_downloadRoot);
        var destination = Path.Combine(_downloadRoot, asset.Name);
        var partial = destination + ".part";
        if (File.Exists(destination))
        {
            if (await IsValidInstallerAsync(destination, asset, cancellationToken))
            {
                progress?.Report(new UpdateDownloadProgress(new FileInfo(destination).Length, asset.Size, 0));
                return destination;
            }
            File.Delete(destination);
        }

        long completed = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("sZIP", CurrentVersion.ToString()));
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var source = await response.Content.ReadAsStreamAsync();
            using var destinationStream = new FileStream(partial, FileMode.Create, FileAccess.Write,
                FileShare.None, 1024 * 1024, useAsync: true);
            using var sha256 = SHA256.Create();
            var buffer = new byte[1024 * 1024];
            int read;
            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destinationStream.WriteAsync(buffer, 0, read, cancellationToken);
                sha256.TransformBlock(buffer, 0, read, null, 0);
                completed += read;
                progress?.Report(new UpdateDownloadProgress(completed, asset.Size,
                    completed / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001)));
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            await destinationStream.FlushAsync(cancellationToken);
            if (asset.Size > 0 && completed != asset.Size)
            {
                throw new UpdateException($"설치 파일 크기가 다릅니다: {completed:N0} / {asset.Size:N0} bytes");
            }
            var actualHash = BitConverter.ToString(sha256.Hash!).Replace("-", string.Empty).ToLowerInvariant();
            if (!string.Equals(actualHash, asset.Sha256, StringComparison.Ordinal))
            {
                throw new UpdateException("설치 파일 SHA-256 검증에 실패했습니다.");
            }
        }
        catch (OperationCanceledException)
        {
            TryDelete(partial);
            throw new UpdateCancelledException();
        }
        catch
        {
            TryDelete(partial);
            throw;
        }

        if (File.Exists(destination)) File.Delete(destination);
        File.Move(partial, destination);
        return destination;
    }

    public IReadOnlyList<string> CleanupDownloads()
    {
        var removed = new List<string>();
        if (!Directory.Exists(_downloadRoot)) return removed;
        var future = new List<(ReleaseVersion Version, string Path)>();
        foreach (var path in Directory.EnumerateFiles(_downloadRoot))
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".exe.part", StringComparison.OrdinalIgnoreCase))
            {
                var installerName = name.Substring(0, name.Length - 5);
                if (InstallerNamePattern.IsMatch(installerName) && TryDelete(path)) removed.Add(path);
                continue;
            }
            var match = InstallerNamePattern.Match(name);
            if (!match.Success || !ReleaseVersion.TryParseTag(match.Groups[1].Value, out var version)) continue;
            if (version.CompareTo(CurrentVersion) <= 0)
            {
                if (TryDelete(path)) removed.Add(path);
            }
            else future.Add((version, path));
        }
        foreach (var item in future.OrderByDescending(item => item.Version).Skip(1))
        {
            if (TryDelete(item.Path)) removed.Add(item.Path);
        }
        return removed;
    }

    public static void LaunchInstaller(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) || !InstallerNamePattern.IsMatch(Path.GetFileName(fullPath)))
        {
            throw new UpdateException("실행할 업데이트 설치 파일이 올바르지 않습니다.");
        }
        var arguments = "/SP- /CLOSEAPPLICATIONS /DELETEINSTALLER=\""
            + fullPath.Replace("\"", "\\\"") + "\"";
        Process.Start(new ProcessStartInfo(fullPath, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(fullPath)!
        });
    }

    private static async Task<bool> IsValidInstallerAsync(string path, ReleaseAsset asset,
        CancellationToken cancellationToken)
    {
        if (asset.Size > 0 && new FileInfo(path).Length != asset.Size) return false;
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, useAsync: true);
        var hash = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);
        var actual = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        return string.Equals(actual, asset.Sha256, StringComparison.Ordinal);
    }

    private static bool TryTrustedGitHubUrl(string? value, bool releaseAsset, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || !string.Equals(parsed.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || !parsed.AbsolutePath.StartsWith("/loselessss/sZIP/releases/", StringComparison.OrdinalIgnoreCase)
            || releaseAsset && parsed.AbsolutePath.IndexOf("/download/", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }
        uri = parsed;
        return true;
    }

    private static HttpClient CreateClient(ReleaseVersion version)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("sZIP", version.ToString()));
        return client;
    }

    private static bool TryDelete(string path)
    {
        try { File.Delete(path); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private sealed class ReleaseResponse
    {
        public string? tag_name { get; set; }
        public string? name { get; set; }
        public string? body { get; set; }
        public string? html_url { get; set; }
        public string? published_at { get; set; }
        public ReleaseAssetResponse[]? assets { get; set; }
    }

    private sealed class ReleaseAssetResponse
    {
        public string? name { get; set; }
        public string? browser_download_url { get; set; }
        public long size { get; set; }
        public string? digest { get; set; }
    }
}
