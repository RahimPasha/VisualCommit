namespace VisualCommit.Core.Session;

/// <summary>
/// What the app restores when it starts again: the open tabs, the recent repositories, the
/// window and the side panels (D46). Stored as <c>session.json</c> in the data folder.
/// Immutable: change it through <see cref="ISessionStore.Update"/>. Every property has a
/// default, so that a file written by an older version still loads.
/// </summary>
public sealed record SessionState
{
    /// <summary>The open tabs, from left to right. Empty means one new, empty tab.</summary>
    public IReadOnlyList<TabState> Tabs { get; init; } = [];

    /// <summary>The index of the tab that was showing. Out of range means the first.</summary>
    public int ActiveTab { get; init; }

    /// <summary>Recently opened repositories, newest first, at most <see cref="MaxRecent"/>.</summary>
    public IReadOnlyList<RecentRepository> Recent { get; init; } = [];

    /// <summary>Where the window was and how big, or null when it has never been saved.</summary>
    public WindowPlacement? Window { get; init; }

    /// <summary>The left panel's width as the user last set it, or null for the default.</summary>
    public double? LeftPanelWidth { get; init; }

    /// <summary>The right panel's width as the user last set it, or null for the default.</summary>
    public double? RightPanelWidth { get; init; }

    /// <summary>The activity log's height as the user last dragged it (docs/phase-5-design.md 2.1), or null for the default.</summary>
    public double? ActivityLogHeight { get; init; }

    /// <summary>
    /// The hidden and soloed branches of each repository (docs/phase-5-design.md 8.4, D159), apart
    /// from the tabs so that closing a tab keeps them, most recently used first, at most
    /// <see cref="MaxGraphFilters"/>.
    /// </summary>
    public IReadOnlyList<RepositoryGraphFilter> GraphFilters { get; init; } = [];

    /// <summary>How many recent repositories are kept.</summary>
    public const int MaxRecent = 10;

    /// <summary>How many repositories' graph filters are kept, whatever the recent list holds (8.4).</summary>
    public const int MaxGraphFilters = 100;
}

/// <summary>One tab.</summary>
/// <param name="RepositoryPath">The working tree of the repository open in it, or null for an empty tab or a dashboard.</param>
/// <param name="WorkspaceId">
/// For a workspace's dashboard (docs/phase-6-design.md 1.11), its <c>Workspace.Id</c>, with a null
/// <paramref name="RepositoryPath"/> (a path beside an id is ignored); null for every other tab.
/// An older version ignores it and sees an empty tab.
/// </param>
public sealed record TabState(string? RepositoryPath, string? WorkspaceId = null);

/// <summary>
/// What one repository's graph hides or solos (docs/phase-5-design.md 8.1, 8.4), by full ref names,
/// since short names collide across kinds.
/// </summary>
/// <param name="RepositoryPath">The repository's working tree, normalised as <see cref="TabState.RepositoryPath"/> is.</param>
/// <param name="Hidden">The hidden branches, local and remote (<c>refs/heads/…</c>, <c>refs/remotes/…</c>), a hidden remote's known branches included.</param>
/// <param name="Soloed">The soloed branches; solo mode is on while there is one.</param>
/// <param name="HiddenRemotes">The remotes hidden as a whole: every branch of each, now and later.</param>
/// <param name="Upstreams">The upstreams that came with a local branch hidden or soloed with it, so that showing it again undoes both.</param>
/// <param name="Remotes">The repository's remotes as last known, so that a hidden remote branch's remote is known before the refs are read (by the longest remote name it starts with).</param>
/// <param name="LastUsed">When the filter last changed or its repository was last opened: the bound keeps the most recent.</param>
public sealed record RepositoryGraphFilter(
    string RepositoryPath,
    IReadOnlyList<string> Hidden,
    IReadOnlyList<string> Soloed,
    IReadOnlyList<string> HiddenRemotes,
    IReadOnlyList<BranchUpstream> Upstreams,
    IReadOnlyList<string> Remotes,
    DateTimeOffset LastUsed);

/// <summary>A local branch and the remote-tracking upstream that was hidden or soloed with it (8.1).</summary>
/// <param name="Branch">The local branch's full name.</param>
/// <param name="Upstream">The upstream's full name.</param>
public sealed record BranchUpstream(string Branch, string Upstream);

/// <summary>A repository in the recent list.</summary>
/// <param name="Path">Its working tree.</param>
/// <param name="Name">The name shown: the folder's name.</param>
/// <param name="LastOpened">When it was last opened in the app.</param>
public sealed record RecentRepository(string Path, string Name, DateTimeOffset LastOpened);

/// <summary>The main window's place on the screen, in logical pixels for the size and screen pixels for the position.</summary>
/// <param name="X">The left edge of the window, in screen pixels.</param>
/// <param name="Y">The top edge of the window, in screen pixels.</param>
/// <param name="Width">The width of the client area, in logical pixels, when not maximised.</param>
/// <param name="Height">The height of the client area, in logical pixels, when not maximised.</param>
/// <param name="IsMaximized">The window was maximised.</param>
public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool IsMaximized);

/// <summary>Holds the current session state and saves every change.</summary>
public interface ISessionStore
{
    /// <summary>The state as last loaded or changed.</summary>
    SessionState Current { get; }

    /// <summary>
    /// Applies <paramref name="change"/> to the current state and saves the result. A failed save
    /// is logged and does not throw; the change still applies for the running session.
    /// </summary>
    void Update(Func<SessionState, SessionState> change);
}
