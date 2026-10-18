namespace VisualCommit.Core.Git;

/// <summary>A configuration value read as git reads a boolean (docs/phase-6-design.md 8.7). Work package WP2 gives it its body.</summary>
public static class GitBoolean
{
    /// <summary>
    /// Whether <paramref name="value"/> is a boolean to git, and which: a bare key
    /// (<paramref name="bare"/>) is true and the empty value false; <c>true</c>, <c>yes</c>,
    /// <c>on</c>, <c>false</c>, <c>no</c>, <c>off</c> ignoring case; an integer as git parses it
    /// (a sign, decimal, <c>0x</c> hex or leading-<c>0</c> octal, a unit <c>k</c>, <c>m</c> or
    /// <c>g</c>, fitting in 32 bits), not zero true; a unit letter alone is none.
    /// </summary>
    public static bool TryParse(string? value, bool bare, out bool result) =>
        throw new NotImplementedException("Work package WP2 builds GitBoolean.TryParse (docs/phase-6-design.md 8.7).");
}
