namespace sZIP.Archive;

public sealed class ArchivePasswordRequiredException : IOException
{
    public ArchivePasswordRequiredException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
