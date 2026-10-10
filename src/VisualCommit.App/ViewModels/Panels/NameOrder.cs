namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// The order of names in the left panel and the file tree: ordinal ignoring case, and names that
/// differ only in case by ordinal, so that the order is the same on every machine and culture.
/// </summary>
internal sealed class NameOrder : IComparer<string>
{
    public static NameOrder Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var ignoringCase = StringComparer.OrdinalIgnoreCase.Compare(x, y);
        return ignoringCase != 0 ? ignoringCase : StringComparer.Ordinal.Compare(x, y);
    }
}
