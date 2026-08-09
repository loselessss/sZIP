namespace sZIP.Application;

public readonly struct ReleaseVersion : IComparable<ReleaseVersion>, IEquatable<ReleaseVersion>
{
    public ReleaseVersion(int major, int minor, int patch)
    {
        if (major < 0 || minor < 0 || patch < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major));
        }

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public static bool TryParseTag(string? tag, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var normalized = tag!.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(1);
        }

        var metadataIndex = normalized.IndexOfAny(new[] { '-', '+' });
        if (metadataIndex >= 0)
        {
            normalized = normalized.Substring(0, metadataIndex);
        }

        var parts = normalized.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch)
            || major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public bool Equals(ReleaseVersion other) =>
        Major == other.Major && Minor == other.Minor && Patch == other.Patch;

    public override bool Equals(object? obj) => obj is ReleaseVersion other && Equals(other);
    public override int GetHashCode() => (Major, Minor, Patch).GetHashCode();
    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;
}
