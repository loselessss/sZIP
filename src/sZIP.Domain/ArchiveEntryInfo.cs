namespace sZIP.Domain
{
    public sealed class ArchiveEntryInfo
    {
        public ArchiveEntryInfo(
            string fullName,
            long length,
            long compressedLength,
            bool isDirectory,
            DateTimeOffset lastWriteTime,
            bool isEncrypted = false)
        {
            FullName = fullName;
            Length = length;
            CompressedLength = compressedLength;
            IsDirectory = isDirectory;
            LastWriteTime = lastWriteTime;
            IsEncrypted = isEncrypted;
        }

        public string FullName { get; }
        public long Length { get; }
        public long CompressedLength { get; }
        public bool IsDirectory { get; }
        public DateTimeOffset LastWriteTime { get; }
        public bool IsEncrypted { get; }
    }
}
