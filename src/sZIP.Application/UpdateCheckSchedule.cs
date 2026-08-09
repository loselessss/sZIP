namespace sZIP.Application;

public sealed class UpdateCheckSchedule
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    public bool IsDue(string? lastCheckUtc, DateTimeOffset now)
    {
        if (!DateTimeOffset.TryParse(lastCheckUtc, out var checkedAt))
        {
            return true;
        }

        var current = now.ToUniversalTime();
        var previous = checkedAt.ToUniversalTime();
        return previous > current || current - previous >= CheckInterval;
    }

    public static string MarkChecked(DateTimeOffset now) =>
        now.ToUniversalTime().ToString("O");

    public static bool IsSkipped(string? skippedVersion, ReleaseVersion version) =>
        string.Equals(skippedVersion?.Trim(), version.ToString(), StringComparison.Ordinal);
}
