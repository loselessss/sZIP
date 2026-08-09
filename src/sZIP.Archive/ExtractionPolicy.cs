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
                    $"압축 파일의 항목 수가 허용 한도({MaxEntryCount:N0}개)를 초과합니다.");
            }

            long totalLength = 0;
            long totalCompressedLength = 0;

            foreach (var entry in entries)
            {
                if (entry.Length > MaxSingleFileBytes)
                {
                    throw new ArchiveSecurityException(
                        $"'{entry.FullName}'의 크기가 단일 파일 한도를 초과합니다.");
                }

                try
                {
                    totalLength = checked(totalLength + entry.Length);
                    totalCompressedLength = checked(totalCompressedLength + entry.CompressedLength);
                }
                catch (OverflowException)
                {
                    throw new ArchiveSecurityException("압축 해제 예상 크기가 올바르지 않습니다.");
                }
            }

            if (totalLength > MaxTotalBytes)
            {
                throw new ArchiveSecurityException(
                    $"전체 해제 크기가 허용 한도({MaxTotalBytes:N0}바이트)를 초과합니다.");
            }

            if (totalLength > 0 && totalCompressedLength == 0)
            {
                throw new ArchiveSecurityException("비정상적인 압축 크기가 감지되었습니다.");
            }

            if (totalCompressedLength > 0 && totalLength / (double)totalCompressedLength > MaxExpansionRatio)
            {
                throw new ArchiveSecurityException(
                    $"압축률이 안전 한도({MaxExpansionRatio:0.#}배)를 초과합니다.");
            }
        }
    }
}
