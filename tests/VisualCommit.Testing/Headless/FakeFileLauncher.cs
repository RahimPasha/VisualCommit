using VisualCommit.App.Services;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Stands in for the app's file launcher in the scripted walk-through and the headless tests
/// (docs/phase-3-design.md 2.32, 7.4): it opens nothing and shows nothing in a file manager, and
/// records what the app asked for, in order, for a test to assert. The default test run must
/// never open an application on the desktop.
/// </summary>
public sealed class FakeFileLauncher : IFileLauncher
{
    private readonly Lock _gate = new();
    private readonly List<FileLaunch> _requests = [];

    /// <summary>Everything the app asked for, in order.</summary>
    public IReadOnlyList<FileLaunch> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>The files the app asked to open, in order ("Open file").</summary>
    public IReadOnlyList<string> Opened => [.. Requests.Where(request => request.Kind == FileLaunchKind.Open).Select(request => request.FullPath)];

    /// <summary>The files the app asked to show in the file manager, in order ("Show in folder").</summary>
    public IReadOnlyList<string> Revealed => [.. Requests.Where(request => request.Kind == FileLaunchKind.Reveal).Select(request => request.FullPath)];

    public Task OpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
        Record(FileLaunchKind.Open, fullPath);

    public Task RevealAsync(string fullPath, CancellationToken cancellationToken = default) =>
        Record(FileLaunchKind.Reveal, fullPath);

    private Task Record(FileLaunchKind kind, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        lock (_gate)
        {
            _requests.Add(new FileLaunch(kind, fullPath));
        }

        return Task.CompletedTask;
    }
}

/// <summary>What <see cref="FakeFileLauncher"/> was asked to do.</summary>
public enum FileLaunchKind
{
    /// <summary>Open the file in the application the system has for it.</summary>
    Open,

    /// <summary>Show the file selected in the file manager.</summary>
    Reveal,
}

/// <summary>One request the app made of the file launcher.</summary>
/// <param name="Kind">Open or show in the file manager.</param>
/// <param name="FullPath">The full path the app passed, as it passed it.</param>
public sealed record FileLaunch(FileLaunchKind Kind, string FullPath);
