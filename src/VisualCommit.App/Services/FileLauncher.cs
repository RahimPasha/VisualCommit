using Avalonia.Controls;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.Services;

/// <summary>
/// <see cref="IFileLauncher"/> on the platform (docs/phase-3-design.md 2.32). A placeholder of
/// work package WP0 so that <c>AppSession</c> can create it; WP4 owns this file and builds it:
/// <c>TopLevel.Launcher.LaunchFileInfoAsync</c>, and Explorer, <c>open -R</c> or the file
/// manager's D-Bus call to show a file.
/// </summary>
public sealed class FileLauncher : IFileLauncher
{
    public FileLauncher(Func<TopLevel?> topLevel, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        TopLevel = topLevel;
        Log = log ?? NullAppLog.Instance;
    }

    private Func<TopLevel?> TopLevel { get; }

    private IAppLog Log { get; }

    public Task OpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Opening a file is not built yet (phase 3, WP4).");

    public Task RevealAsync(string fullPath, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Showing a file in its folder is not built yet (phase 3, WP4).");
}
