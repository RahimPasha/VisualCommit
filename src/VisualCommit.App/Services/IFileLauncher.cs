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
}

/// <summary>Puts text on the clipboard (docs/phase-3-design.md 2.32). The scripted walk-through replaces it with a fake.</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
}
