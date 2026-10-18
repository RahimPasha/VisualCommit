namespace VisualCommit.Core.Settings;

/// <summary>The colour theme of the app (D194). Dark is the default.</summary>
public enum AppTheme
{
    Dark,
    Light,

    /// <summary>The operating system's light or dark mode, followed while the app runs (docs/phase-6-design.md 11.2).</summary>
    System,
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
    /// auto-fetch off (phase 3 design 6.1, D79, D85). The settings screen's General section sets it
    /// (docs/phase-6-design.md 7.3): a check box and 1 to 60 minutes, a value outside that range in
    /// <c>settings.json</c> kept until it is edited. The environment variable
    /// <c>VISUALCOMMIT_AUTO_FETCH_MINUTES</c> overrides it.
    /// </summary>
    public int AutoFetchMinutes { get; init; } = 5;

    /// <summary>The stash dialog's "Include untracked files" starts with the last choice (phase 3 design 6.2).</summary>
    public bool StashIncludeUntracked { get; init; } = true;

    /// <summary>
    /// The details panel's "All files": the list shows every file of the shown commit's tree, the
    /// unchanged ones without a status letter (docs/phase-5-design.md 4.5, D164). Off by default;
    /// it stays as the user left it.
    /// </summary>
    public bool ShowAllFiles { get; init; }

    /// <summary>
    /// Keep ignored files (D193, D196, docs/phase-6-design.md 9.1): a checkout, merge, rebase,
    /// cherry-pick, revert, stash apply or stash pop that would overwrite or delete an ignored file
    /// does not start. On by default, also for a file written before it existed. Each operation
    /// reads it when it runs. Off: they run as git runs them, which restores phase 5 exactly.
    /// </summary>
    public bool KeepIgnoredFiles { get; init; } = true;

    /// <summary>The editor "Open in editor" starts (docs/phase-6-design.md 12.6), or null while none is chosen: the file menus then have no such item.</summary>
    public EditorSetting? Editor { get; init; }

    /// <summary>
    /// The extensions "Open file" asks about before it opens a file, beyond its fixed list and, on
    /// Windows, <c>PATHEXT</c>'s (docs/phase-6-design.md 12.8): space-separated, each with its dot
    /// (<c>.py .pl</c>); empty by default.
    /// </summary>
    public string ExtraRunnableExtensions { get; init; } = string.Empty;
}

/// <summary>The editor "Open in editor" starts (docs/phase-6-design.md 12). Null in <see cref="AppSettings"/>: none chosen.</summary>
/// <param name="Id">A <c>KnownEditors</c> id, or <c>custom</c> for Other….</param>
/// <param name="Program">The program's absolute path.</param>
/// <param name="Arguments">Other…'s argument template, with <c>{path}</c> and <c>{line}</c>; null for a known editor, whose arguments come from the catalogue.</param>
public sealed record EditorSetting(string Id, string Program, string? Arguments = null);
