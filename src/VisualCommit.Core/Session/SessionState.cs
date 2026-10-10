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

    /// <summary>How many recent repositories are kept.</summary>
    public const int MaxRecent = 10;
}

/// <summary>One tab.</summary>
/// <param name="RepositoryPath">The working tree of the repository open in it, or null for an empty tab.</param>
public sealed record TabState(string? RepositoryPath);

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
