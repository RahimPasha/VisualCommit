namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// The left panel's order of refs (phase 3's left panel; docs/phase-6-design.md 5.5, KD1): their
/// names split at slashes into folders, and in each folder its subfolders first, then its refs,
/// each by <see cref="NameOrder"/>. <c>feature/x</c> comes before <c>alpha</c> (a folder before a
/// ref), <c>alpha</c> before <c>Main</c> (case ignored first). The left panel builds its trees in
/// this order, and the command palette lists the local branches in it.
/// </summary>
internal sealed class RefTreeOrder : IComparer<string>
{
    public static RefTreeOrder Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return x is null ? (y is null ? 0 : -1) : 1;
        }

        var xRest = x.AsSpan();
        var yRest = y.AsSpan();
        while (true)
        {
            var (xSlash, ySlash) = (xRest.IndexOf('/'), yRest.IndexOf('/'));
            var (xIsRef, yIsRef) = (xSlash < 0, ySlash < 0);
            if (xIsRef != yIsRef)
            {
                // A folder before a ref of the same folder.
                return xIsRef ? 1 : -1;
            }

            var byName = NameOrder.CompareNames(xIsRef ? xRest : xRest[..xSlash], yIsRef ? yRest : yRest[..ySlash]);
            if (byName != 0 || xIsRef)
            {
                return byName;
            }

            xRest = xRest[(xSlash + 1)..];
            yRest = yRest[(ySlash + 1)..];
        }
    }
}
