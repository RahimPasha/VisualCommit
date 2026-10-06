namespace VisualCommit.Core.Settings;

/// <summary>The colour theme of the app. Dark is the default.</summary>
public enum AppTheme
{
    Dark,
    Light,
}

/// <summary>
/// The user's settings, stored as JSON in the data folder. Immutable: change it through
/// <see cref="ISettingsStore.Update"/>. Every property needs a default, so that a settings file
/// written by an older version still loads.
/// </summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.Dark;
}
