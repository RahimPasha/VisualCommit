// File actions: docs/phase-3-design.md 2.30 (ignore), 2.32 (open, show in folder, copy SHA,
// SHAs, path, branch or tag name, URL), 4b's file menus and the working-changes row's Stage all,
// Unstage all and Discard all, which delegate to phase 2's code in RepositoryViewModel (stage,
// unstage, discard with its confirmation and snapshot, open diff; D62, D67, D70, D76). The
// launcher and the clipboard come from TabServices (Launcher, Clipboard); none of these but
// ignore is an operation in the queue. Built by the sub-package WP7-stashtags; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsTests.Files.cs (through OperationsHarness,
// whose FakeFileLauncher and FakeClipboard record what was asked).

using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>"Stage" and "Mark as resolved": phase 2's stage, its failure in the stage panel.</summary>
    public Task StageFilesAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return StagePanelWriteAsync(() => Repository.StageAsync(files));
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

    /// <summary>The working-changes row's "Stage all": the unstaged list's header button of phase 2.</summary>
    public Task StageAllAsync() => StagePanelWriteAsync(() => Repository.StageAsync(Status.Unstaged));

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

    /// <summary>"Open file" (2.32): the working tree's version in the system's application for it.</summary>
    public Task OpenFileAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var fullPath = FullPathOf(path);
        if (!File.Exists(fullPath))
        {
            // The menu disables the item then ("The file is not in the working tree"); it may have gone since.
            Log.Warning($"{Repository.Name}: Open file: {path} is not in the working tree.");
            return Task.CompletedTask;
        }

        return LaunchAsync("Open file", launcher => launcher.OpenAsync(fullPath));
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

    /// <summary>"Copy path" (2.32): the repository-relative path with <c>/</c>.</summary>
    public Task CopyPathAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return CopyAsync("Copy path", path.Replace('\\', '/'));
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
            Log.Warning($"{Repository.Name}: {label}: {path} cannot be written as a .gitignore line.", ex);
            return Task.CompletedTask;
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

            await RunAsync(text, OperationKind.Local, RefreshScope.Status, async _ =>
            {
                await Git.AppendToGitignoreAsync(line);
                return OperationReport.Done(OperationTexts.Ignored(line));
            });
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
