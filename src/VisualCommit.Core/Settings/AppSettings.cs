namespace VisualCommit.Core.Settings;

/// <summary>The colour theme of the app. Dark is the default.</summary>
public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>How the commit details list the changed files (D44). Flat is the default.</summary>
public enum FileListMode
{
    /// <summary>One row per file: its name, then its folder.</summary>
    Flat,

    /// <summary>Files inside folders that open and close.</summary>
    Tree,
}

/// <summary>How the diff view shows a file's changes (D61). Inline is the default.</summary>
public enum DiffMode
{
    /// <summary>One column, the removed lines of a change above the added ones.</summary>
    Inline,

    /// <summary>The old file on the left, the new one on the right.</summary>
    SideBySide,
}

/// <summary>
/// The user's settings, stored as JSON in the data folder. Immutable: change it through
/// <see cref="ISettingsStore.Update"/>. Every property needs a default, so that a settings file
/// written by an older version still loads.
/// </summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.Dark;

    public FileListMode FileList { get; init; } = FileListMode.Flat;

    public DiffMode DiffMode { get; init; } = DiffMode.Inline;

    /// <summary>
    /// How often the active tab's repository is fetched in the background, in minutes; 0 turns
    /// auto-fetch off (phase 3 design 6.1, D79, D85). No screen sets it in phase 3: it is edited
    /// in <c>settings.json</c>. The environment variable <c>VISUALCOMMIT_AUTO_FETCH_MINUTES</c>
    /// overrides it.
    /// </summary>
    public int AutoFetchMinutes { get; init; } = 5;

    /// <summary>The stash dialog's "Include untracked files" starts with the last choice (phase 3 design 6.2).</summary>
    public bool StashIncludeUntracked { get; init; } = true;
}
