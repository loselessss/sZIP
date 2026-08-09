namespace sZIP.Archive;

public sealed class ArchiveSecurityException : IOException
{
    public ArchiveSecurityException(string message) : base(message)
    {
    }
}
