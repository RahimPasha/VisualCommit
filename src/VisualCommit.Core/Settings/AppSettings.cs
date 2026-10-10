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

/// <summary>
/// The user's settings, stored as JSON in the data folder. Immutable: change it through
/// <see cref="ISettingsStore.Update"/>. Every property needs a default, so that a settings file
/// written by an older version still loads.
/// </summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.Dark;

    public FileListMode FileList { get; init; } = FileListMode.Flat;
}
