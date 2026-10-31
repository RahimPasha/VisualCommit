using VisualCommit.App.Services;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Stands in for the app's file launcher in the scripted walk-through and the headless tests
/// (docs/phase-3-design.md 2.32, 7.4): it opens nothing and shows nothing in a file manager, and
/// records what the app asked for, in order, for a test to assert. The default test run must
/// never open an application on the desktop. Since phase 6 it records each editor start too
/// (docs/phase-6-design.md 12.4): the program, the arguments as a list and the working directory,
/// and checks no program; <see cref="EditFailure"/> makes the next start throw, as a program that
/// cannot start does (12.7).
/// </summary>
public sealed class FakeFileLauncher : IFileLauncher
{
    private readonly Lock _gate = new();
    private readonly List<FileLaunch> _requests = [];
    private Exception? _editFailure;

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

    /// <summary>The editor starts the app asked for, in order ("Open in editor"), each as the command it built.</summary>
    public IReadOnlyList<LaunchCommand> Edits => [.. Requests.Where(request => request.Kind == FileLaunchKind.Edit).Select(request => request.Command!)];

    /// <summary>
    /// Thrown by the next editor start, after it was recorded, then cleared: a
    /// <see cref="FileNotFoundException"/> for a program that is gone, a
    /// <see cref="System.ComponentModel.Win32Exception"/> for one that cannot start (12.7).
    /// </summary>
    public Exception? EditFailure
    {
        get
        {
            lock (_gate)
            {
                return _editFailure;
            }
        }

        set
        {
            lock (_gate)
            {
                _editFailure = value;
            }
        }
    }

    public Task OpenAsync(string fullPath, CancellationToken cancellationToken = default) =>
        Record(new FileLaunch(FileLaunchKind.Open, fullPath));

    public Task RevealAsync(string fullPath, CancellationToken cancellationToken = default) =>
        Record(new FileLaunch(FileLaunchKind.Reveal, fullPath));

    /// <summary>
    /// Records an <c>http</c> or <c>https</c> address; any other fails the task with the real
    /// launcher's <see cref="ArgumentException"/> (<see cref="FileLauncher.ThrowIfNotWebAddress"/>)
    /// and is not recorded, as the real launcher opens nothing.
    /// </summary>
    public Task OpenUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        try
        {
            FileLauncher.ThrowIfNotWebAddress(url);
        }
        catch (ArgumentException refused)
        {
            return Task.FromException(refused);
        }

        return Record(new FileLaunch(FileLaunchKind.OpenUrl, url.AbsoluteUri));
    }

    public Task OpenInEditorAsync(LaunchCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        Exception? failure;
        lock (_gate)
        {
            _requests.Add(new FileLaunch(FileLaunchKind.Edit, command.FileName, command));
            failure = _editFailure;
            _editFailure = null;
        }

        return failure is null ? Task.CompletedTask : Task.FromException(failure);
    }

    private Task Record(FileLaunch request)
    {
        ArgumentNullException.ThrowIfNull(request.FullPath);
        lock (_gate)
        {
            _requests.Add(request);
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

    /// <summary>Start the chosen editor on a file (docs/phase-6-design.md 12.4).</summary>
    Edit,

    /// <summary>Open a web address in the default browser (docs/phase-7a-design.md 1.7); its <see cref="FileLaunch.FullPath"/> is the address.</summary>
    OpenUrl,
}

/// <summary>One request the app made of the file launcher.</summary>
/// <param name="Kind">Open, show in the file manager, or start the editor.</param>
/// <param name="FullPath">The full path the app passed, as it passed it; for <see cref="FileLaunchKind.Edit"/> the editor's program; for <see cref="FileLaunchKind.OpenUrl"/> the address.</param>
/// <param name="Command">For <see cref="FileLaunchKind.Edit"/>, the command the app built; null otherwise.</param>
public sealed record FileLaunch(FileLaunchKind Kind, string FullPath, LaunchCommand? Command = null);
