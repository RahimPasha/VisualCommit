namespace VisualCommit.App.Services;

/// <summary>
/// The texts the app carries inside its own assembly (D431, D433): its license (<c>LICENSE</c>),
/// <c>THIRD-PARTY-NOTICES.md</c> and the vendors' notice files of the <c>notices/</c> folder, each
/// embedded word for word by the project file, so that About shows them with nothing beside the
/// executable and the installers ship the same texts unchanged.
/// </summary>
public static class EmbeddedTexts
{
    /// <summary>The resource that holds the repository's <c>LICENSE</c>.</summary>
    public const string License = "VisualCommit.License.txt";

    /// <summary>The resource that holds the repository's <c>THIRD-PARTY-NOTICES.md</c>.</summary>
    public const string ThirdPartyNotices = "VisualCommit.ThirdPartyNotices.md";

    /// <summary>What every vendor notice's resource name starts with; its file's name in <c>notices/</c> follows.</summary>
    public const string VendorNoticePrefix = "VisualCommit.Notices.";

    /// <summary>
    /// The text of the resource <paramref name="resourceName"/>, with <c>\n</c> line ends whatever the
    /// checkout gave the file (a byte-order mark is dropped). Reads the assembly, so About calls it off
    /// the UI thread.
    /// </summary>
    /// <exception cref="FileNotFoundException">The app carries no such resource.</exception>
    public static string Read(string resourceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        using var stream = typeof(EmbeddedTexts).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"The app carries no text {resourceName}.", resourceName);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>The resource names of the vendors' notice files, in ordinal order of their names.</summary>
    public static IReadOnlyList<string> VendorNotices() =>
        [.. typeof(EmbeddedTexts).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(VendorNoticePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    /// <summary>The file name in <c>notices/</c> of a vendor notice's resource: the name after <see cref="VendorNoticePrefix"/>.</summary>
    public static string FileNameOf(string resourceName)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        return resourceName.StartsWith(VendorNoticePrefix, StringComparison.Ordinal) ? resourceName[VendorNoticePrefix.Length..] : resourceName;
    }
}
