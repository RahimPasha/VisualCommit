using System.Reflection;

namespace VisualCommit.App.Services;

/// <summary>The app's version as the build stamps it: the release's number, and the commit it was built from when the build knew it.</summary>
/// <param name="Version">The release's number, such as <c>0.1.0</c>.</param>
/// <param name="Commit">The commit's hash, 7 to 40 hexadecimal digits; null when the build did not name one.</param>
public sealed record AppVersionInfo(string Version, string? Commit)
{
    /// <summary>The commit's first 7 characters; null without a commit.</summary>
    public string? ShortCommit => Commit is null ? null : Commit[..Math.Min(7, Commit.Length)];

    /// <summary>The version as the app shows it: <c>0.1.0 (3dc0887)</c>, or <c>0.1.0</c> without a commit.</summary>
    public string Display => ShortCommit is { } commit ? $"{Version} ({commit})" : Version;

    /// <summary>
    /// The version of an assembly's informational version: <c>0.1.0+&lt;7 to 40 hexadecimal
    /// digits&gt;</c> gives the commit, any other part after <c>+</c> is dropped, and null or an
    /// empty text gives <c>0.0.0</c>.
    /// </summary>
    public static AppVersionInfo Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return new AppVersionInfo("0.0.0", null);
        }

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return new AppVersionInfo(informationalVersion.Trim(), null);
        }

        var version = informationalVersion[..plus].Trim();
        var metadata = informationalVersion[(plus + 1)..].Trim();
        var commit = metadata.Length is >= 7 and <= 40 && metadata.All(char.IsAsciiHexDigit) ? metadata : null;
        return new AppVersionInfo(version.Length == 0 ? "0.0.0" : version, commit);
    }
}

/// <summary>Who and what the app is, as its About section shows it: its name, its creator, its version and its copyright.</summary>
/// <param name="Name">The product's name.</param>
/// <param name="Creator">The person who made it.</param>
/// <param name="Version">Its version.</param>
/// <param name="Copyright">Its copyright line.</param>
public sealed record AppIdentity(string Name, string Creator, AppVersionInfo Version, string Copyright)
{
    /// <summary>The app's creator.</summary>
    public const string CreatorName = "Rahim Khajei";

    /// <summary>
    /// The identity <paramref name="assembly"/>'s attributes give: its product, <see cref="CreatorName"/>,
    /// its informational version (<see cref="AppVersionInfo.Parse"/>) and its copyright. A missing
    /// product gives the assembly's name, a missing copyright an empty line.
    /// </summary>
    public static AppIdentity FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return new AppIdentity(
            assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? assembly.GetName().Name ?? string.Empty,
            CreatorName,
            AppVersionInfo.Parse(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion),
            assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty);
    }
}
