// File actions: docs/phase-3-design.md 2.30 (ignore), 2.32 (open, show in folder, copy SHA,
// SHAs, path, branch or tag name, URL), docs/phase-6-design.md 12 (Open in editor, Open file's
// question by the fixed list, the user's extensions and PATHEXT), 4b's file menus and the working-changes row's Stage all,
// Unstage all and Discard all, which delegate to phase 2's code in RepositoryViewModel (stage,
// unstage, discard with its confirmation and snapshot, open diff; D62, D67, D70, D76). The
// launcher and the clipboard come from TabServices (Launcher, Clipboard); none of these but
// ignore is an operation in the queue. Built by the sub-package WP7-stashtags; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsFilesTests.cs (through OperationsHarness,
// whose FakeFileLauncher and FakeClipboard record what was asked).

using System.ComponentModel;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Undo;
using VisualCommit.Git;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>
    /// "Stage" and "Mark as resolved": phase 2's stage, its failure in the stage panel. A conflicted
    /// file whose working-tree file still has conflict markers asks first (docs/phase-4-design.md
    /// 7.1, 7.2, D148).
    /// </summary>
    public async Task StageFilesAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!IsClosed && await ConfirmStagingWithMarkersAsync(files))
        {
            await StagePanelWriteAsync(() => Repository.StageAsync(files));
        }
    }

    /// <summary>"Unstage": phase 2's unstage (a rename unstages both its paths).</summary>
    public Task UnstageFilesAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return StagePanelWriteAsync(() => Repository.UnstageAsync(files));
    }

    /// <summary>"Discard changes…": phase 2's discard, with its own confirmation, snapshot and restore bar (D62, D67).</summary>
    public Task DiscardFilesAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return StagePanelWriteAsync(() => Repository.DiscardAsync(files));
    }

    /// <summary>The working-changes row's "Stage all": the unstaged list's header button of phase 2, asking first about conflict markers (7.1).</summary>
    public Task StageAllAsync() => StageFilesAsync(Status.Unstaged);

    /// <summary>The working-changes row's "Unstage all": the staged list's header button of phase 2.</summary>
    public Task UnstageAllAsync() => StagePanelWriteAsync(() => Repository.UnstageAsync(Status.Staged));

    /// <summary>The working-changes row's "Discard all changes…": phase 2's Discard all, with its confirmation.</summary>
    public Task DiscardAllAsync() => StagePanelWriteAsync(() => Repository.DiscardAsync(Status.Unstaged));

    /// <summary>"Ignore &lt;file name&gt;" (2.30): <c>/&lt;path&gt;</c>, escaped, appended to .gitignore.</summary>
    public Task IgnoreFileAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return IgnoreAsync(path, OperationTexts.IgnoreFileLabel(FileNameOf(path)), () => GitignorePattern.ForPath(path.TrimEnd('/')));
    }

    /// <summary>"Ignore all *.&lt;ext&gt; files" (2.30): <c>*.&lt;ext&gt;</c>, escaped, appended to .gitignore.</summary>
    public Task IgnoreExtensionAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (ExtensionOf(path) is not { } extension)
        {
            Log.Warning($"{Repository.Name}: Ignore all files of {path}'s extension: the name has no extension.");
            return Task.CompletedTask;
        }

        return IgnoreAsync(path, OperationTexts.IgnoreExtensionLabel(extension), () => GitignorePattern.ForExtension(extension));
    }

    /// <summary>
    /// "Open in editor" (docs/phase-6-design.md 12.1, 12.4): the working tree's file in the chosen
    /// editor, never asking first (an editor shows a file and runs nothing), at its top. No
    /// operation and no undo entry, as Open file (D168); the log line <c>Open in editor: {command}</c>.
    /// When the editor cannot start, the error dialog says why, with "Open settings…", which opens
    /// the settings screen on Tools (12.7).
    /// </summary>
    public async Task OpenInEditorAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (IsClosed)
        {
            return;
        }

        if (Services?.Settings.Current.Editor is not { } editor)
        {
            Log.Info($"{Repository.Name}: Open in editor: no editor is chosen.");
            return;
        }

        var known = editor.Id == KnownEditors.CustomId ? null : KnownEditors.Find(editor.Id);
        if (known is null && editor.Id != KnownEditors.CustomId)
        {
            // An id the catalogue no longer knows means none chosen (12.6): the menu has no item then.
            Log.Info($"{Repository.Name}: Open in editor: the editor {editor.Id} is not known.");
            return;
        }

        // Off the UI thread (D59): a file on a share that does not answer holds the check for minutes (0.9).
        var fullPath = FullPathOf(path);
        var fileExists = Services.FileExists;
        if (!await Task.Run(() => fileExists(fullPath)))
        {
            // The menu disables the item then ("The file is not in the working tree"); it may have gone since.
            Log.Warning($"{Repository.Name}: Open in editor: {path} is not in the working tree.");
            return;
        }

        if (IsClosed)
        {
            return;
        }

        if (Services.Launcher is not { } launcher)
        {
            Log.Info($"{Repository.Name}: Open in editor: there is no launcher.");
            return;
        }

        var name = EditorName(editor, known);
        try
        {
            var command = EditorCommands.Build(editor, known, fullPath, line: null);
            Log.Info($"Open in editor: {command.DisplayText}");
            await launcher.OpenInEditorAsync(command);
        }
        catch (FileNotFoundException ex)
        {
            Log.Warning($"{Repository.Name}: Open in editor: {name} is not at {editor.Program}.", ex);
            await ShowEditorFailureAsync(OperationTexts.EditorIsGone(name, editor.Program));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or EditorCommandException or UnauthorizedAccessException)
        {
            Log.Warning($"{Repository.Name}: Open in editor: {name} could not be started.", ex);
            await ShowEditorFailureAsync(OperationTexts.EditorCouldNotStart(name, ex.Message));
        }
    }

    /// <summary>How the editor failure's dialog names an editor (12.7): the catalogue's name, or Other's program's file name.</summary>
    public static string EditorName(EditorSetting editor, KnownEditor? known)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return known?.Name ?? Path.GetFileName(editor.Program.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>12.7's dialog, without git's output (D114), with "Open settings…", which opens the settings screen on Tools.</summary>
    private async Task ShowEditorFailureAsync(string summary)
    {
        var dialog = new ErrorDialogViewModel(
            OperationTexts.EditorFailedTitle,
            summary,
            Repository.Name,
            extraAction: OperationTexts.OpenSettingsButton,
            clipboard: Services?.Clipboard,
            log: Log);
        if (await ShowDialogAsync(dialog) && Services?.OpenSettings is { } openSettings)
        {
            await openSettings(SettingsSection.Tools);
        }
    }

    /// <summary>
    /// "Open file" (2.32): the working tree's version in the system's application for it. A file
    /// that the system would run rather than show (<see cref="CanRunPrograms(string, UnixFileMode?, IReadOnlySet{string})"/>:
    /// the fixed list, the user's extensions and, on Windows, <c>PATHEXT</c>'s, docs/phase-6-design.md
    /// 12.8), as a cloned repository can hold, is opened only after the user said yes.
    /// </summary>
    public async Task OpenFileAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var fullPath = FullPathOf(path);

        // Off the UI thread (D59), as Open in editor's: a file on a share that does not answer
        // holds the check, and the read of its mode, for minutes (docs/phase-6-design.md 0.9).
        var fileExists = Services?.FileExists ?? File.Exists;
        var (exists, unixMode) = await Task.Run(() => fileExists(fullPath) ? (true, ReadUnixFileMode(fullPath)) : (false, (UnixFileMode?)null));
        if (!exists)
        {
            // The menu disables the item then ("The file is not in the working tree"); it may have gone since.
            Log.Warning($"{Repository.Name}: Open file: {path} is not in the working tree.");
            return;
        }

        if (IsClosed)
        {
            return;
        }

        if (CanRunPrograms(fullPath, unixMode, ExtraRunnableExtensions()) && !await ConfirmAsync(OperationTexts.ConfirmOpenRunnable(FileNameOf(path))))
        {
            return;
        }

        await LaunchAsync("Open file", launcher => launcher.OpenAsync(fullPath));
    }

    /// <summary>
    /// Whether the system's "open" of a file named <paramref name="fileName"/> runs something
    /// (2.32): a program, a script, an installer or a shortcut by its extension (Windows, macOS
    /// and Linux types alike), or, given its <paramref name="unixMode"/> (null on Windows), a file
    /// with an executable bit.
    /// </summary>
    public static bool CanRunPrograms(string fileName, UnixFileMode? unixMode) => CanRunPrograms(fileName, unixMode, EmptyExtensions);

    /// <summary>
    /// <see cref="CanRunPrograms(string, UnixFileMode?)"/> with <paramref name="extraExtensions"/>
    /// (with their dot) besides the fixed list (docs/phase-6-design.md 12.8): the user's own and, on
    /// Windows, <c>PATHEXT</c>'s; case is ignored.
    /// </summary>
    public static bool CanRunPrograms(string fileName, UnixFileMode? unixMode, IReadOnlySet<string> extraExtensions)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(extraExtensions);
        const UnixFileMode Executable = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        var extension = Path.GetExtension(fileName);
        return RunnableExtensions.Contains(extension)
            || (extension.Length > 0 && extraExtensions.Any(extra => string.Equals(extra, extension, StringComparison.OrdinalIgnoreCase)))
            || (unixMode is { } mode && (mode & Executable) != 0);
    }

    /// <summary>
    /// The extensions Open file always asks about (D130, docs/phase-6-design.md 12.8), in the order
    /// the settings screen lists them: a floor the user cannot remove.
    /// </summary>
    public static IReadOnlyList<string> FixedRunnableExtensions { get; } =
    [
        ".exe", ".bat", ".cmd", ".com", ".ps1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".msi", ".scr", ".pif", ".cpl",
        ".lnk", ".url", ".reg", ".jar", ".app", ".command", ".sh", ".desktop",
    ];

    /// <summary>
    /// <c>PATHEXT</c>'s extensions in lower case, in its order, without duplicates (12.8): each entry
    /// of <paramref name="pathExt"/> split at <c>;</c>, given its dot; empty for null.
    /// </summary>
    public static IReadOnlyList<string> PathExtExtensions(string? pathExt) =>
        [.. (pathExt ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => (entry.StartsWith('.') ? entry : "." + entry).ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)];

    /// <summary>The extensions <see cref="CanRunPrograms(string, UnixFileMode?)"/> takes as runnable, with their dot.</summary>
    private static readonly HashSet<string> RunnableExtensions = new(FixedRunnableExtensions, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> EmptyExtensions = [];

    /// <summary>
    /// What Open file asks about beyond its fixed list, read at each call (12.8): the user's
    /// extensions (<c>AppSettings.ExtraRunnableExtensions</c>, read as the settings screen reads its
    /// box: <see cref="RunnableExtensionText.Parse"/>, so a hand-edited <c>py,pl</c> counts too) and,
    /// on Windows, <c>PATHEXT</c>'s, from the session's catalogues (<see cref="TabServices.Tools"/>);
    /// none without them.
    /// </summary>
    private HashSet<string> ExtraRunnableExtensions()
    {
        var extensions = new HashSet<string>(RunnableExtensionText.Parse(Services?.Settings.Current.ExtraRunnableExtensions), StringComparer.OrdinalIgnoreCase);

        // The session's catalogues hold PATHEXT (12.9: the machine's, or the harnesses' fixed one); null off Windows.
        extensions.UnionWith(PathExtExtensions(Services?.Tools?.PathExtensions()));

        return extensions;
    }

    /// <summary>A file's Unix permissions, or null on Windows or when they cannot be read.</summary>
    private static UnixFileMode? ReadUnixFileMode(string fullPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return File.GetUnixFileMode(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>"Show in folder" (2.32): the file selected in the file manager (a missing one shows its folder), or the working tree's folder for null.</summary>
    public Task ShowInFolderAsync(string? path)
    {
        var fullPath = path is null ? Repository.Path : FullPathOf(path);
        return LaunchAsync("Show in folder", launcher => launcher.RevealAsync(fullPath));
    }

    /// <summary>"Copy SHA" (2.32): the full id.</summary>
    public Task CopyShaAsync(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        return CopyAsync("Copy SHA", sha);
    }

    /// <summary>"Copy SHAs" (2.32): the full ids, one per line, in the graph's row order (top first); ids the graph has not loaded follow in the order given.</summary>
    public Task CopyShasAsync(IReadOnlyList<string> shas)
    {
        ArgumentNullException.ThrowIfNull(shas);
        var graph = Repository.Graph;
        var ordered = shas
            .Distinct(StringComparer.Ordinal)
            .Select((sha, given) => (Sha: sha, Row: graph.IndexOf(sha), Given: given))
            .OrderBy(item => item.Row < 0 ? int.MaxValue : item.Row)
            .ThenBy(item => item.Given)
            .Select(item => item.Sha);
        return CopyAsync("Copy SHAs", string.Join("\n", ordered));
    }

    /// <summary>
    /// "Copy path" (2.32): the repository-relative path as git gives it, with <c>/</c>. A backslash
    /// in it is part of a name (Linux and macOS allow one) and is kept.
    /// </summary>
    public Task CopyPathAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return CopyAsync("Copy path", path);
    }

    /// <summary>The activity log's "Copy command", "Copy output" and "Copy title" (docs/phase-5-design.md 2.2): the text as the log shows it.</summary>
    public Task CopyTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CopyAsync("Copy", text);
    }

    /// <summary>"Copy branch name" and "Copy tag name" (2.32): the ref's short name.</summary>
    public Task CopyRefNameAsync(string fullRef)
    {
        ArgumentNullException.ThrowIfNull(fullRef);
        return CopyAsync("Copy name", ShortName(fullRef));
    }

    /// <summary>"Copy URL" (2.23, 2.32, D103): the remote's fetch URL as configured, its user information removed.</summary>
    public async Task CopyRemoteUrlAsync(string remote)
    {
        ArgumentNullException.ThrowIfNull(remote);
        if (IsClosed)
        {
            return;
        }

        try
        {
            var info = (await Git.ReadRemotesAsync()).FirstOrDefault(candidate => string.Equals(candidate.Name, remote, StringComparison.Ordinal));
            if (info is null)
            {
                Log.Warning($"{Repository.Name}: Copy URL: there is no remote {remote}.");
                return;
            }

            await CopyAsync("Copy URL", GitCommand.RemoveCredentials(info.FetchUrl));
        }
        catch (Exception ex) when (ex is GitException or IOException or OperationCanceledException)
        {
            Log.Warning($"{Repository.Name}: Copy URL: the URL of {remote} could not be read.", ex);
        }
    }

    /// <summary>Opens a working-tree file's diff, as a click on its row does (D63).</summary>
    public void OpenDiff(ChangedFile file, bool staged)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!IsClosed)
        {
            Repository.OpenDiff(file, staged);
        }
    }

    /// <summary>The file part of a path, a folder's without its slash ("Ignore run.log").</summary>
    private static string FileNameOf(string path) => WorkingChangesViewModel.FileName(path.TrimEnd('/'));

    /// <summary>The extension of a path's file name without its dot, or null when the name has none (".gitignore" and "TODO" have none).</summary>
    private static string? ExtensionOf(string path)
    {
        var name = FileNameOf(path);
        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? name[(dot + 1)..] : null;
    }

    /// <summary>A repository-relative path (with <c>/</c>) as a full path with the platform's separators.</summary>
    private string FullPathOf(string path) =>
        Path.Combine(Repository.Path, path.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// One of phase 2's stage-panel writes, as the stage panel's own buttons run it: its failure in
    /// the stage panel's error text, nothing in the status bar (1.4). Nothing once the tab closed.
    /// </summary>
    private async Task StagePanelWriteAsync(Func<Task<string?>> write)
    {
        if (IsClosed)
        {
            return;
        }

        Repository.Changes.ErrorText = null;
        Repository.Changes.ErrorText = await write();
    }

    /// <summary>2.30 as one local operation: the line is built (escaped) and appended; the status is read again.</summary>
    private Task IgnoreAsync(string path, string label, Func<string> pattern)
    {
        string line;
        try
        {
            line = pattern();
        }
        catch (ArgumentException ex)
        {
            // A line break in the name: the user is told, as an operation refused before git runs (D119).
            Log.Warning($"{Repository.Name}: {label}: {path} cannot be written as a .gitignore line.", ex);
            var refused = OperationTexts.Ignore(label, path);
            return GuardAsync(refused, () => RefuseAsync(refused, OperationTexts.IgnoreNameNotWritable, RefreshScope.None));
        }

        var text = OperationTexts.Ignore(label, line);
        return GuardAsync(text, async () =>
        {
            if (IsClosed)
            {
                return;
            }

            if (!Status.IsUntracked(path))
            {
                // The menus disable the item ("Only untracked files can be ignored"); the file may have been added since.
                Log.Warning($"{Repository.Name}: {label}: {path} is not untracked; nothing is ignored.");
                return;
            }

            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Status,
                async _ =>
                {
                    await Git.AppendToGitignoreAsync(line);
                    return OperationReport.Done(OperationTexts.Ignored(line));
                },
                new RunOptions { Undo = UndoScope.Ignore(line) });
        });
    }

    /// <summary>Starts the launcher (2.32): not an operation; a failure is logged, the tab being closed or no launcher (view-model tests) does nothing.</summary>
    private async Task LaunchAsync(string action, Func<IFileLauncher, Task> launch)
    {
        if (IsClosed)
        {
            return;
        }

        if (Services?.Launcher is not { } launcher)
        {
            Log.Info($"{Repository.Name}: {action}: there is no launcher.");
            return;
        }

        try
        {
            await launch(launcher);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning($"{Repository.Name}: {action} failed.", ex);
        }
    }

    /// <summary>Puts <paramref name="text"/> on the clipboard (2.32): not an operation; a failure is logged.</summary>
    private async Task CopyAsync(string action, string text)
    {
        if (IsClosed)
        {
            return;
        }

        if (Services?.Clipboard is not { } clipboard)
        {
            Log.Info($"{Repository.Name}: {action}: there is no clipboard.");
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning($"{Repository.Name}: {action} failed.", ex);
        }
    }
}
