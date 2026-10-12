// Conflicts: the conflict resolver's actions (IConflictActions, docs/phase-4-design.md 11.6 to
// 11.9), the file menu's "Resolve conflict" and "Open in merge tool", and the questions about
// conflict markers that staging and Continue ask (5.1, 7.1, 7.2, D148). Texts in
// OperationTexts.Conflicts.cs. Built by work package WP4; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsConflictsTests.cs.

using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Conflicts;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations : IConflictActions
{
    private readonly HashSet<string> _conflictedAtStop = new(StringComparer.Ordinal);
    private string? _stopKey;
    private MergeToolRun? _runningMergeTool;

    /// <inheritdoc cref="IRepositoryActions.ResolveConflict"/>
    public void ResolveConflict(ChangedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!IsClosed)
        {
            Repository.OpenResolver(file);
        }
    }

    /// <inheritdoc cref="IRepositoryActions.OpenMergeToolAsync"/>
    /// <remarks>
    /// When the resolver is open on <paramref name="path"/>, its own <c>Merge tool…</c> runs
    /// (11.9): it asks first when its result is modified ("Keep editing" starts nothing), keeps the
    /// result read-only while the tool runs and moves on afterwards. Otherwise the tool runs as one
    /// operation (<see cref="IConflictActions.RunMergeToolAsync"/>).
    /// </remarks>
    public async Task OpenMergeToolAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Repository.IsResolverOpen && string.Equals(Repository.Resolver.Path, path, StringComparison.Ordinal))
        {
            var command = Repository.Resolver.RunMergeToolCommand;
            if (command.CanExecute(null))
            {
                await command.ExecuteAsync(null);
            }
            else
            {
                Log.Info($"{Repository.Name}: the merge tool for {path} was not started: the resolver is busy or has no merge tool.");
            }

            return;
        }

        await ((IConflictActions)this).RunMergeToolAsync(path);
    }

    /// <summary>
    /// Mark resolved (11.7): one operation "Resolve {file}" that reads the working-tree file
    /// (<see cref="IGitRepository.ReadConflictAsync"/>) and, when it is still the bytes the
    /// resolver expected, writes the result and stages it (<see cref="IGitRepository.WriteResolutionAsync"/>):
    /// "Resolved {file}" in the status bar, a failure in the error dialog "Resolving failed". The
    /// check runs inside the operation, just before the write, so a change made while the
    /// operation waited in the queue (when the watcher is paused, D81) is caught too. When the file
    /// changed, nothing is written, no dialog opens and the status bar says
    /// <c>{file} changed on disk: nothing was written</c> in the warning colour; the resolver asks
    /// next.
    /// </summary>
    async Task<MarkResolvedResult> IConflictActions.MarkResolvedAsync(ConflictEntry entry, byte[] content, byte[]? expected)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(content);
        var path = entry.Path;
        var file = WorkingChangesViewModel.FileName(path);
        byte[]? onDisk = null;
        var changed = false;
        var resolved = await ResolveAsync(path, async options =>
        {
            var now = await Git.ReadConflictAsync(entry, options);
            onDisk = now.WorkingTreeExists ? now.WorkingTree : null;
            if (expected is null ? onDisk is not null : onDisk is null || !onDisk.AsSpan().SequenceEqual(expected))
            {
                changed = true;
                Log.Info($"{Repository.Name}: {path} changed on disk since the resolver read it: its result was not written.");
                return OperationReport.Warning(ConflictResolverTexts.ChangedOnDiskNotWritten(file));
            }

            await Git.WriteResolutionAsync(path, content, options);
            return null;
        });

        return changed
            ? new MarkResolvedResult(MarkResolvedOutcome.ChangedOnDisk, onDisk)
            : new MarkResolvedResult(resolved ? MarkResolvedOutcome.Resolved : MarkResolvedOutcome.Failed);
    }

    /// <summary>A whole file from one side, "Keep the file", "Keep it" (11.8): one operation "Resolve {file}" (<see cref="IGitRepository.TakeSideAsync"/>).</summary>
    Task<bool> IConflictActions.TakeSideAsync(string path, ConflictSide side)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ResolveAsync(path, options => Git.TakeSideAsync(path, side, options));
    }

    /// <summary>"Delete it" (11.8): one operation "Resolve {file}" (<see cref="IGitRepository.RemoveConflictedAsync"/>).</summary>
    Task<bool> IConflictActions.RemoveAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ResolveAsync(path, options => Git.RemoveConflictedAsync(path, options));
    }

    /// <summary>
    /// The external merge tool (11.9, D149): one operation "Merge tool for {file}" that waits for
    /// the tool and that the user can cancel (<see cref="RunOptions.CanBeCancelled"/>: Cancel stops
    /// git and the tool). "Resolved {file} in {tool}" when git staged the file, "{tool} closed
    /// without resolving {file}" otherwise; a tool that did not run opens "Merge tool failed".
    /// Nothing runs while git's configuration names no tool (the menus disable it then).
    /// </summary>
    async Task<bool> IConflictActions.RunMergeToolAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var file = WorkingChangesViewModel.FileName(path);
        if (IsClosed)
        {
            return false;
        }

        string? tool;
        try
        {
            tool = await Git.ReadMergeToolAsync();
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            Log.Warning($"{Repository.Name}: merge.tool could not be read.", ex);
            tool = null;
        }

        if (tool is null)
        {
            Log.Info($"{Repository.Name}: Merge tool for {file}: not started, git's configuration names no merge tool.");
            return false;
        }

        var text = OperationTexts.MergeTool(file, tool);
        var staged = false;
        var ended = new TaskCompletionSource<bool>();
        try
        {
            await GuardAsync(text, () => RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Status,
                async context =>
                {
                    // While it runs, a resolver on the file is read-only, waiting for the tool (11.9).
                    var run = new MergeToolRun(path, tool, ended.Task);
                    _runningMergeTool = run;
                    try
                    {
                        MergeToolStarted?.Invoke(this, run);
                        var result = await Git.RunMergeToolAsync(path, tool, NetworkOptions.From(context), context.CancellationToken);
                        staged = result.Staged;
                        return OperationReport.Done(staged ? OperationTexts.ResolvedInTool(file, tool) : OperationTexts.ToolClosedWithoutResolving(tool, file));
                    }
                    finally
                    {
                        _runningMergeTool = null;
                    }
                },
                new RunOptions { CanBeCancelled = true }));
        }
        finally
        {
            ended.TrySetResult(staged);
        }

        return staged;
    }

    /// <inheritdoc/>
    public MergeToolRun? RunningMergeTool => _runningMergeTool;

    /// <inheritdoc/>
    public event EventHandler<MergeToolRun>? MergeToolStarted;

    /// <summary>One side in its working-tree form (11.6); null when that side has no file or the read failed (logged).</summary>
    async Task<byte[]?> IConflictActions.ReadSideAsync(ConflictEntry entry, ConflictSide side)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            return await Git.ReadConflictSideAsync(entry, side);
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            Log.Warning($"{Repository.Name}: the {side} side of {entry.Path} could not be read.", ex);
            return null;
        }
    }

    /// <summary>What the resolver reads of a conflicted file when it opens it (11.3): <see cref="IGitRepository.ReadConflictAsync"/>; a failure is the resolver's to show.</summary>
    Task<ConflictContent> IConflictActions.ReadAsync(ConflictEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Git.ReadConflictAsync(entry);
    }

    // ---- The questions about conflict markers (5.1, 7.1, 7.2, 14.3) ---------------------------------------

    /// <summary>
    /// Before conflicted files are staged ("Mark as resolved", a row's stage button, "Stage all",
    /// 7.1): asks 7.2's question when the working-tree file of one of <paramref name="files"/>'s
    /// conflicted files still has conflict markers (a line that starts with git's <c>&lt;</c>,
    /// <c>=</c>, <c>&gt;</c> or <c>|</c> marker of the path's marker size, as git's own check of
    /// staged markers finds them). Binary files, symbolic links and missing files are not read.
    /// True to go ahead: nothing to ask, or the user said yes.
    /// </summary>
    public async Task<bool> ConfirmStagingWithMarkersAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var withMarkers = new List<string>();
        foreach (var file in files.Where(file => file.Kind == FileChangeKind.Conflicted))
        {
            if (await HasMarkersInWorkingTreeAsync(file.Path))
            {
                withMarkers.Add(WorkingChangesViewModel.FileName(file.Path));
            }
        }

        return withMarkers.Count == 0 || await ConfirmAsync(OperationTexts.ConfirmStagingWithMarkers(withMarkers));
    }

    /// <summary>
    /// Before Continue (5.1) and before the stage panel's commit that concludes a merge or a single
    /// pick or revert (14.3): <c>git diff --cached --check</c> of the paths that were conflicted at
    /// <paramref name="state"/>'s stop (<see cref="NoteStatus"/>; the staged paths when the app saw
    /// none conflicted there) and 7.2's question when any has markers staged. True to go ahead.
    /// </summary>
    public async Task<bool> ConfirmStagedMarkersAsync(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (Status.Unstaged.Any(file => file.Kind == FileChangeKind.Conflicted))
        {
            // Files still conflict: Continue is refused with its reason, and nothing is committed.
            return true;
        }

        var paths = StopKey(state) == _stopKey && _conflictedAtStop.Count > 0
            ? [.. _conflictedAtStop.Order(StringComparer.Ordinal)]
            : Status.Staged.Select(file => file.Path).Distinct(StringComparer.Ordinal).ToList();
        if (paths.Count == 0)
        {
            return true;
        }

        IReadOnlyList<string> withMarkers;
        try
        {
            withMarkers = await Git.ReadStagedConflictMarkersAsync(paths);
        }
        catch (GitException ex)
        {
            // The check could not run: git's own Continue decides, as it would without the app.
            Log.Warning($"{Repository.Name}: the staged files could not be checked for conflict markers.", ex);
            return true;
        }

        return withMarkers.Count == 0 || await ConfirmAsync(OperationTexts.ConfirmContinueWithMarkers(withMarkers.Count));
    }

    /// <summary>
    /// Follows each status the repository shows (<see cref="RepositoryViewModel"/>'s
    /// <c>OnStatusApplied</c>): the paths that conflict while one stopped commit waits are kept,
    /// so that Continue checks them for staged markers after they were resolved (5.1). A new
    /// stopped commit, or nothing in progress, starts over.
    /// </summary>
    public void NoteStatus(WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var key = StopKey(status.InProgress);
        if (key != _stopKey)
        {
            _stopKey = key;
            _conflictedAtStop.Clear();
        }

        foreach (var file in status.Unstaged.Where(file => file.Kind == FileChangeKind.Conflicted))
        {
            _conflictedAtStop.Add(file.Path);
        }
    }

    /// <summary>Which stopped commit a state is (the kind, the commit, a rebase's place); null for conflicts without an operation.</summary>
    private static string? StopKey(InProgressState state) =>
        state.IsActive ? $"{state.Kind}:{state.Commit}:{state.Done}" : null;

    /// <summary>
    /// Whether the working-tree file of the conflicted <paramref name="path"/> still has conflict
    /// markers (7.1): read as the resolver reads it (<see cref="IGitRepository.ReadConflictAsync"/>,
    /// with the path's marker size); false for a binary file, a symbolic link, a missing file, a
    /// path the status does not list as conflicted, or a read that failed (logged).
    /// </summary>
    private async Task<bool> HasMarkersInWorkingTreeAsync(string path)
    {
        if ((Status.FindConflict(path) ?? (await Git.ReadStatusAsync()).FindConflict(path)) is not { } entry)
        {
            return false;
        }

        try
        {
            var content = await Git.ReadConflictAsync(entry);
            if (!content.WorkingTreeExists || content.OursBinary || content.TheirsBinary || content.IsSymbolicLink)
            {
                return false;
            }

            return ConflictText.HasMarkerLines(ConflictText.Decode(content.WorkingTree).Text, content.MarkerSize);
        }
        catch (Exception ex) when (ex is GitException or IOException)
        {
            Log.Warning($"{Repository.Name}: {path} could not be read for conflict markers.", ex);
            return false;
        }
    }

    /// <summary>
    /// One operation "Resolve {file}" (10.1) that runs <paramref name="resolve"/>: "Resolved
    /// {file}" in the status bar (secondary, 14.3), a failure in the error dialog "Resolving
    /// failed" with git's output. True when it succeeded.
    /// </summary>
    private Task<bool> ResolveAsync(string path, Func<NetworkOptions, Task> resolve) =>
        ResolveAsync(path, async options =>
        {
            await resolve(options);
            return (OperationReport?)null;
        });

    /// <summary>As <see cref="ResolveAsync(string, Func{NetworkOptions, Task})"/>, where <paramref name="resolve"/> may end the operation with a report of its own (null: "Resolved {file}").</summary>
    private async Task<bool> ResolveAsync(string path, Func<NetworkOptions, Task<OperationReport?>> resolve)
    {
        var file = WorkingChangesViewModel.FileName(path);
        var text = OperationTexts.Resolve(file);
        var result = await GuardAsync<OperationResult?>(
            text,
            async () => await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Status,
                async context => await resolve(NetworkOptions.From(context)) ?? OperationReport.Done(OperationTexts.Resolved(file))),
            fallback: null);
        return result?.Outcome == OperationOutcome.Succeeded;
    }
}
