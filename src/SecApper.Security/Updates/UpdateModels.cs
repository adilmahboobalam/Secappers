using System;
using System.Text.Json.Serialization;

namespace SecApper.Security.Updates;

public class UpdateInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public string ReleaseDate { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("minSupportedVersion")]
    public string? MinSupportedVersion { get; set; }
}

public class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; }
    public UpdateInfo? Update { get; }
    public string CurrentVersion { get; }
    public string? ErrorMessage { get; }
    public bool IsSuccess => ErrorMessage == null;

    public UpdateCheckResult(bool isUpdateAvailable, UpdateInfo? update, string currentVersion, string? errorMessage = null)
    {
        IsUpdateAvailable = isUpdateAvailable;
        Update = update;
        CurrentVersion = currentVersion;
        ErrorMessage = errorMessage;
    }
}

public class UpdateProgress
{
    public long BytesDownloaded { get; }
    public long TotalBytes { get; }
    public double Percentage => TotalBytes > 0 ? Math.Round((double)BytesDownloaded / TotalBytes * 100, 1) : 0;
    public string StatusMessage { get; }

    public UpdateProgress(long bytesDownloaded, long totalBytes, string statusMessage = "")
    {
        BytesDownloaded = bytesDownloaded;
        TotalBytes = totalBytes;
        StatusMessage = statusMessage;
    }
}

public class SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int Build { get; }

    public SemVersion(int major, int minor, int patch, int build = 0)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Build = build;
    }

    public static bool TryParse(string? versionStr, out SemVersion result)
    {
        result = new SemVersion(0, 0, 0);
        if (string.IsNullOrWhiteSpace(versionStr))
            return false;

        string clean = versionStr.Trim().TrimStart('v', 'V');
        string[] parts = clean.Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 1 || !int.TryParse(parts[0], out int major))
            return false;

        int minor = 0;
        int patch = 0;
        int build = 0;

        if (parts.Length > 1) int.TryParse(parts[1], out minor);
        if (parts.Length > 2) int.TryParse(parts[2], out patch);
        if (parts.Length > 3) int.TryParse(parts[3], out build);

        result = new SemVersion(major, minor, patch, build);
        return true;
    }

    public static SemVersion Parse(string versionStr)
    {
        if (TryParse(versionStr, out var ver))
            return ver;
        throw new FormatException($"Invalid semantic version format: '{versionStr}'");
    }

    public int CompareTo(SemVersion? other)
    {
        if (other is null) return 1;
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        if (Patch != other.Patch) return Patch.CompareTo(other.Patch);
        return Build.CompareTo(other.Build);
    }

    public bool Equals(SemVersion? other)
    {
        if (other is null) return false;
        return Major == other.Major && Minor == other.Minor && Patch == other.Patch && Build == other.Build;
    }

    public override bool Equals(object? obj) => Equals(obj as SemVersion);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Build);

    public override string ToString() => Build > 0 ? $"{Major}.{Minor}.{Patch}.{Build}" : $"{Major}.{Minor}.{Patch}";

    public static bool operator ==(SemVersion? left, SemVersion? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(SemVersion? left, SemVersion? right) => !(left == right);
    public static bool operator <(SemVersion? left, SemVersion? right) => left is not null && right is not null && left.CompareTo(right) < 0;
    public static bool operator >(SemVersion? left, SemVersion? right) => left is not null && right is not null && left.CompareTo(right) > 0;
    public static bool operator <=(SemVersion? left, SemVersion? right) => left is not null && right is not null && left.CompareTo(right) <= 0;
    public static bool operator >=(SemVersion? left, SemVersion? right) => left is not null && right is not null && left.CompareTo(right) >= 0;
}
