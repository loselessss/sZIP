namespace sZIP.Watcher;

public static class ArchiveStabilityProbe
{
    public static async Task<bool> WaitUntilReadyAsync(
        string path,
        ArchiveWatchOptions options,
        CancellationToken cancellationToken)
    {
        long previousLength = -1;
        DateTime previousWriteTime = DateTime.MinValue;
        DateTimeOffset? stableSince = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            FileInfo file;
            try
            {
                file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.Length > options.MaxArchiveBytes)
                {
                    return false;
                }
            }
            catch (IOException)
            {
                await Task.Delay(options.RequiredProbeInterval, cancellationToken);
                continue;
            }

            if (file.Length == previousLength && file.LastWriteTimeUtc == previousWriteTime)
            {
                stableSince ??= DateTimeOffset.UtcNow;
                if (DateTimeOffset.UtcNow - stableSince >= options.RequiredStableTime && CanOpenExclusively(path))
                {
                    return !options.RequireZipSignature || HasZipSignature(path);
                }
            }
            else
            {
                previousLength = file.Length;
                previousWriteTime = file.LastWriteTimeUtc;
                stableSince = DateTimeOffset.UtcNow;
            }

            await Task.Delay(options.RequiredProbeInterval, cancellationToken);
        }

        return false;
    }

    private static bool CanOpenExclusively(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasZipSignature(string path)
    {
        var signature = new byte[4];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Read(signature, 0, signature.Length) != signature.Length)
        {
            return false;
        }

        return signature[0] == 0x50 && signature[1] == 0x4B
            && ((signature[2] == 0x03 && signature[3] == 0x04)
                || (signature[2] == 0x05 && signature[3] == 0x06)
                || (signature[2] == 0x07 && signature[3] == 0x08));
    }
}
