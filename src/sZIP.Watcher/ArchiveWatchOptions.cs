namespace sZIP.Watcher
{
    public sealed class ArchiveWatchOptions
    {
        public ArchiveWatchOptions(
            string rootPath,
            bool includeSubdirectories = true,
            long maxArchiveBytes = 200L * 1024 * 1024,
            TimeSpan? stableFor = null,
            TimeSpan? probeInterval = null,
            TimeSpan? reconcileInterval = null,
            IEnumerable<string>? supportedExtensions = null,
            bool requireZipSignature = true)
        {
            RootPath = rootPath;
            IncludeSubdirectories = includeSubdirectories;
            MaxArchiveBytes = maxArchiveBytes;
            RequiredStableTime = stableFor ?? TimeSpan.FromSeconds(5);
            RequiredProbeInterval = probeInterval ?? TimeSpan.FromSeconds(1);
            ReconcileInterval = reconcileInterval ?? TimeSpan.FromSeconds(10);
            SupportedExtensions = new HashSet<string>(
                supportedExtensions ?? new[] { ".zip" },
                StringComparer.OrdinalIgnoreCase);
            RequireZipSignature = requireZipSignature;
        }

        public string RootPath { get; }
        public bool IncludeSubdirectories { get; }
        public long MaxArchiveBytes { get; }
        public TimeSpan RequiredStableTime { get; }
        public TimeSpan RequiredProbeInterval { get; }
        public TimeSpan ReconcileInterval { get; }
        public IReadOnlyCollection<string> SupportedExtensions { get; }
        public bool RequireZipSignature { get; }

        public bool Supports(string path) =>
            SupportedExtensions.Contains(Path.GetExtension(path));
    }
}
