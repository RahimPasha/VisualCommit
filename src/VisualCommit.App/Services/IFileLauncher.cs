namespace VisualCommit.App.Services;

/// <summary>
/// Opens a file in its default application and shows a file in the file manager
/// (docs/phase-3-design.md 2.32). The scripted walk-through replaces it with a fake, so the
/// default test run never opens an application.
/// </summary>
public interface IFileLauncher
{
    /// <summary>Opens <paramref name="fullPath"/> in the application the system has for it.</summary>
    Task OpenAsync(string fullPath, CancellationToken cancellationToken = default);

    /// <summary>Shows <paramref name="fullPath"/> selected in the file manager ("Show in folder"); a missing file shows its folder.</summary>
    Task RevealAsync(string fullPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the chosen editor with <paramref name="command"/> ("Open in editor",
    /// docs/phase-6-design.md 12.4), off the UI thread, not waited for. Throws
    /// <see cref="FileNotFoundException"/> when its program is not there, and the start's own
    /// exception when it cannot start (12.7).
    /// </summary>
    Task OpenInEditorAsync(LaunchCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens <paramref name="url"/> in the default browser (docs/phase-7a-design.md 1.7): an
    /// <c>http</c> or <c>https</c> address only, through the platform's launcher on the UI thread;
    /// a warning is logged when the system could not open it.
    /// </summary>
    Task OpenUrlAsync(Uri url, CancellationToken cancellationToken = default);
}

/// <summary>Puts text on the clipboard (docs/phase-3-design.md 2.32). The scripted walk-through replaces it with a fake.</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
}
