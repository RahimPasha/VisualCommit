using System.Globalization;
using System.Text.RegularExpressions;

namespace VisualCommit.Core.Git;

/// <summary>The version of a git executable, as reported by <c>git --version</c>.</summary>
public readonly partial record struct GitVersion(int Major, int Minor, int Patch) : IComparable<GitVersion>
{
    /// <summary>The oldest git the app works with (decision D2).</summary>
    public static GitVersion Minimum { get; } = new(2, 30, 0);

    public bool IsSupported => this >= Minimum;

    /// <summary>
    /// Reads the version out of <c>git --version</c> output, such as
    /// "git version 2.36.0.windows.1" or "git version 2.39.3 (Apple Git-146)".
    /// </summary>
    public static bool TryParse(string? text, out GitVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = VersionPattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        version = new GitVersion(
            int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture),
            match.Groups[3].Success ? int.Parse(match.Groups[3].ValueSpan, CultureInfo.InvariantCulture) : 0);
        return true;
    }

    public int CompareTo(GitVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public static bool operator <(GitVersion left, GitVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(GitVersion left, GitVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(GitVersion left, GitVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(GitVersion left, GitVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    [GeneratedRegex(@"(?<!\d)(\d{1,4})\.(\d{1,4})(?:\.(\d{1,4}))?")]
    private static partial Regex VersionPattern();
}
