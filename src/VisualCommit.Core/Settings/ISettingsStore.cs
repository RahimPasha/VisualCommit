namespace VisualCommit.Core.Settings;

/// <summary>Holds the current settings and saves every change.</summary>
public interface ISettingsStore
{
    /// <summary>The settings as last loaded or changed.</summary>
    AppSettings Current { get; }

    /// <summary>
    /// Applies <paramref name="change"/> to the current settings and saves the result. A failed
    /// save is logged and does not throw; the change still applies for the running session.
    /// </summary>
    void Update(Func<AppSettings, AppSettings> change);
}
