namespace sZIP.Domain
{
    public sealed class CompressionProgress
    {
        public CompressionProgress(
            string currentEntry,
            int completedEntries,
            int totalEntries,
            long processedBytes,
            long totalBytes)
        {
            CurrentEntry = currentEntry;
            CompletedEntries = completedEntries;
            TotalEntries = totalEntries;
            ProcessedBytes = processedBytes;
            TotalBytes = totalBytes;
        }

        public string CurrentEntry { get; }
        public int CompletedEntries { get; }
        public int TotalEntries { get; }
        public long ProcessedBytes { get; }
        public long TotalBytes { get; }

        public double Percentage => TotalBytes == 0
            ? (TotalEntries == 0 ? 0 : CompletedEntries * 100d / TotalEntries)
            : ProcessedBytes * 100d / TotalBytes;
    }
}
