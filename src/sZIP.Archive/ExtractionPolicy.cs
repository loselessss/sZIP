using System.IO.Compression;

namespace sZIP.Archive
{
    public sealed class ExtractionPolicy
    {
        public ExtractionPolicy(
            int maxEntryCount = 10_000,
            long maxTotalBytes = 2L * 1024 * 1024 * 1024,
            long maxSingleFileBytes = 1L * 1024 * 1024 * 1024,
            double maxExpansionRatio = 20d)
        {
            MaxEntryCount = maxEntryCount;
            MaxTotalBytes = maxTotalBytes;
            MaxSingleFileBytes = maxSingleFileBytes;
            MaxExpansionRatio = maxExpansionRatio;
        }

        public int MaxEntryCount { get; }
        public long MaxTotalBytes { get; }
        public long MaxSingleFileBytes { get; }
        public double MaxExpansionRatio { get; }

        public void Validate(IReadOnlyCollection<ZipArchiveEntry> entries)
        {
            if (entries.Count > MaxEntryCount)
            {
                throw new ArchiveSecurityException(
                    $"The archive entry count exceeds the allowed limit({MaxEntryCount:N0} entries).");
            }

            long totalLength = 0;
            long totalCompressedLength = 0;

            foreach (var entry in entries)
            {
                if (entry.Length > MaxSingleFileBytes)
                {
                    throw new ArchiveSecurityException(
                        $"'{entry.FullName}' exceeds the per-file size limit.");
                }

                try
                {
                    totalLength = checked(totalLength + entry.Length);
                    totalCompressedLength = checked(totalCompressedLength + entry.CompressedLength);
                }
                catch (OverflowException)
                {
                    throw new ArchiveSecurityException("The estimated extraction size is invalid.");
                }
            }

            if (totalLength > MaxTotalBytes)
            {
                throw new ArchiveSecurityException(
                    $"The total extraction size exceeds the allowed limit({MaxTotalBytes:N0} bytes).");
            }

            if (totalLength > 0 && totalCompressedLength == 0)
            {
                throw new ArchiveSecurityException("An abnormal compressed size was detected.");
            }

            if (totalCompressedLength > 0 && totalLength / (double)totalCompressedLength > MaxExpansionRatio)
            {
                throw new ArchiveSecurityException(
                    $"The expansion ratio exceeds the safety limit({MaxExpansionRatio:0.#}x).");
            }
        }
    }
}
