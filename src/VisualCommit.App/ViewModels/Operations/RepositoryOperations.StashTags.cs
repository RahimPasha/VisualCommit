// Stashes and tags: docs/phase-3-design.md 2.24 to 2.28 (stash save with its dialog, apply,
// pop, drop; tag create and delete), and their texts in 2.33 (OperationTexts). Pushing and
// deleting a tag on a remote is in RepositoryOperations.Remotes.cs (2.29). Built by the
// sub-package WP7-stashtags on the helpers of RepositoryOperations.Run.cs; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsStashTagsTests.cs.

using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>What git says when local changes keep a stash from applying (2.25).</summary>
    private const string StashOverwriteMarker = "would be overwritten by merge";

    /// <summary>
    /// What git says when a stash's untracked files cannot be restored because files of their names
    /// exist (each listed as "&lt;file&gt; already exists, no checkout"): its tracked changes are
    /// applied by then, and the stash is kept (2.25).
    /// </summary>
    private const string StashUntrackedMarker = "could not restore untracked files from stash";

    /// <summary>A HEAD that is not unborn, for <see cref="StashRefusal"/> before the refs are read: git then says whether there is a first commit.</summary>
    private static readonly HeadState HeadNotRead = new(null, "HEAD");

    /// <summary>The toolbar's last Pop: the stash it pops and its task (UI thread only).</summary>
    private (string Sha, Task Task)? _newestPop;

    /// <summary>
    /// "Stash" and "Stash changes…" (2.24): refused for the reasons that disable the toolbar's
    /// Stash and every "Stash changes…" item (<see cref="StashRefusal"/>, D119), otherwise the
    /// stash dialog with the last choice of "Include untracked files" (6.2), which is saved when
    /// the dialog is confirmed, then <c>git stash push</c>. Nothing to stash is no failure: the
    /// status bar says so.
    /// </summary>
    public Task StashAsync() => GuardAsync(OperationTexts.Stash, async () =>
    {
        if (IsClosed)
        {
            return;
        }

        if (StashRefusal(Status) is { } reason)
        {
            await RefuseAsync(OperationTexts.Stash, reason, RefreshScope.Status);
            return;
        }

        var includeUntracked = Services?.Settings.Current.StashIncludeUntracked ?? true;
        if (await ShowDialogAsync(new StashDialogViewModel(includeUntracked)) is not { } answer)
        {
            return;
        }

        Services?.Settings.Update(settings => settings with { StashIncludeUntracked = answer.IsOptionChecked });
        await RunAsync(OperationTexts.Stash, OperationKind.Local, RefreshScope.Refs, async context =>
        {
            // The status may have changed while the dialog was open or the operation waited.
            if (StashRefusal(await Git.ReadStatusAsync()) is { } refused)
            {
                return Refusal(OperationTexts.Stash, refused);
            }

            var stash = await Git.StashAsync(answer.Message, answer.IsOptionChecked, NetworkOptions.From(context));
            return OperationReport.Done(stash is null ? OperationTexts.NothingToStash : OperationTexts.Stashed);
        });
    });

    /// <summary>"Apply stash" (2.25): <c>git stash apply stash@{n}</c>, the stash found again by its commit.</summary>
    public Task ApplyStashAsync(string stash) => ApplyOrPopAsync(stash, pop: false);

    /// <summary>"Pop stash" (2.25): <c>git stash pop stash@{n}</c>; a pop that conflicts keeps the stash.</summary>
    public Task PopStashAsync(string stash) => ApplyOrPopAsync(stash, pop: true);

    /// <summary>
    /// The toolbar's Pop (2.25): the newest stash, <c>stash@{0}</c>. A second click while the pop
    /// of that same stash has not ended (a double click, before the refs are read again) gets
    /// that pop's task: it would pop the stash the first one already popped.
    /// </summary>
    public Task PopNewestStashAsync()
    {
        if (IsClosed)
        {
            return Task.CompletedTask;
        }

        if (Refs?.Stashes.FirstOrDefault(entry => entry.Index == 0) is not { } newest)
        {
            Log.Info($"{Repository.Name}: Pop: there is no stash to pop.");
            return Task.CompletedTask;
        }

        if (_newestPop is { Task.IsCompleted: false } pending && string.Equals(pending.Sha, newest.Sha, StringComparison.Ordinal))
        {
            Log.Info($"{Repository.Name}: Pop: {newest.Name} is being popped already.");
            return pending.Task;
        }

        var pop = PopStashAsync(newest.Sha);
        _newestPop = (newest.Sha, pop);
        return pop;
    }

    /// <summary>"Drop stash…" (2.26): confirms, backs the stash commit up as <c>stash-&lt;time&gt;</c> and drops it.</summary>
    public Task DropStashAsync(string stash)
    {
        ArgumentNullException.ThrowIfNull(stash);
        var entry = FindStash(stash);
        var name = entry?.Name ?? OperationTexts.Short(stash);
        var text = OperationTexts.DropStash(name);
        return GuardAsync(text, async () =>
        {
            if (IsClosed)
            {
                return;
            }

            if (entry is null)
            {
                await StashGoneAsync(text);
                return;
            }

            await ConfirmAndRunAsync(
                OperationTexts.ConfirmDropStash(name, entry.Message),
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                _ => Task.FromResult<BackupRequest?>(new BackupRequest("stash", [new BackupEntry(null, stash)])),
                async (_, _) =>
                {
                    // Its name now: another operation may have moved it in the list meanwhile.
                    var now = FindStash(stash)?.Name ?? name;
                    await Git.DropStashAsync(stash);
                    return OperationReport.Done(OperationTexts.DroppedStash(now));
                },
                new RunOptions { DescribeFailure = failure => StashFailureDisplay(text, failure, name, pop: false) });
        });
    }

    /// <summary>
    /// "Create tag here…" and "Create tag at HEAD…" (2.27): the tag dialog at the commit
    /// <paramref name="target"/> names (a full SHA, a full ref name, or null for HEAD), then a
    /// lightweight tag, or an annotated one with the message as typed. The tag goes on the commit
    /// the dialog named, peeled (D121).
    /// </summary>
    public Task CreateTagAsync(string? target) => GuardAsync(OperationTexts.CreateTagUnnamed, async () =>
    {
        if (IsClosed)
        {
            return;
        }

        var commit = await ResolveTagTargetAsync(target);
        if (commit is null)
        {
            Log.Warning($"{Repository.Name}: Create tag: {target ?? "HEAD"} names no commit.");
            return;
        }

        var context = "At " + await DescribeCommitAsync(commit);
        if (await ShowDialogAsync(NewTagDialog(context)) is not { } answer)
        {
            return;
        }

        var tag = answer.Name;
        var message = answer.Message;
        await RunAsync(OperationTexts.CreateTag(tag), OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await Git.CreateTagAsync(tag, commit, message);
            return OperationReport.Done(OperationTexts.CreatedTag(tag));
        });
    });

    /// <summary>"Delete &lt;t&gt;…" (2.28): confirms, backs the tag object up as <c>tag-&lt;time&gt;/&lt;t&gt;</c> and deletes the local tag.</summary>
    public Task DeleteTagAsync(string tagRef)
    {
        ArgumentNullException.ThrowIfNull(tagRef);
        var tag = ShortName(tagRef);
        var text = OperationTexts.DeleteTag(tag);
        return GuardAsync(text, async () =>
        {
            if (IsClosed)
            {
                return;
            }

            await ConfirmAndRunAsync(
                OperationTexts.ConfirmDeleteTag(tag),
                text,
                OperationKind.Local,
                RefreshScope.Refs,

                // The full ref name keeps the tag object of an annotated tag, with its message.
                _ => Task.FromResult<BackupRequest?>(new BackupRequest("tag", [new BackupEntry(tag, TagsPrefix + tag)])),
                async (_, _) =>
                {
                    await Git.DeleteTagAsync(tag);
                    return OperationReport.Done(OperationTexts.DeletedTag(tag));
                });
        });
    }

    private Task ApplyOrPopAsync(string stash, bool pop)
    {
        ArgumentNullException.ThrowIfNull(stash);
        var entry = FindStash(stash);
        var name = entry?.Name ?? OperationTexts.Short(stash);
        var text = pop ? OperationTexts.PopStash(name) : OperationTexts.ApplyStash(name);
        return GuardAsync(text, async () =>
        {
            if (IsClosed)
            {
                return;
            }

            if (entry is null)
            {
                await StashGoneAsync(text);
                return;
            }

            await RunAsync(
                text,
                OperationKind.Local,
                RefreshScope.Refs,
                async context =>
                {
                    // Its name now: another operation may have moved it in the list meanwhile.
                    var now = FindStash(stash)?.Name ?? name;
                    var result = await Git.ApplyStashAsync(stash, pop ? StashApplyMode.Pop : StashApplyMode.Apply, NetworkOptions.From(context));
                    return result == ApplyResult.Conflicts
                        ? OperationReport.Conflicts(OperationTexts.ApplyStashConflicts(await CountConflictedFilesAsync(), pop))
                        : OperationReport.Done(pop ? OperationTexts.PoppedStash(now) : OperationTexts.AppliedStash(now));
                },
                new RunOptions { DescribeFailure = failure => StashFailureDisplay(text, failure, name, pop) });
        });
    }

    /// <summary>
    /// 2.25's summaries: a stash no longer listed, local changes in the way, untracked files whose
    /// names are taken (the tracked changes applied all the same); null for 2.33's default.
    /// </summary>
    private static FailureDisplay? StashFailureDisplay(OperationText text, GitFailure failure, string name, bool pop)
    {
        if (failure.GitOutput.Contains(OperationTexts.StashGone, StringComparison.Ordinal))
        {
            return new FailureDisplay(text.ErrorTitle, OperationTexts.StashGone);
        }

        if (failure.GitOutput.Contains(StashUntrackedMarker, StringComparison.OrdinalIgnoreCase))
        {
            return new FailureDisplay(text.ErrorTitle, OperationTexts.StashUntrackedNotRestored(name, pop));
        }

        return failure.GitOutput.Contains(StashOverwriteMarker, StringComparison.Ordinal)
            ? new FailureDisplay(text.ErrorTitle, OperationTexts.StashWouldOverwrite(name))
            : null;
    }

    /// <summary>
    /// Why a stash is refused before git runs (D119): the toolbar's and the menus' reason
    /// (<see cref="ToolbarViewModel.StashReason"/>, in 2.24's order), except "There are no changes
    /// to stash", which is left to git: <paramref name="status"/> can be older than the working
    /// tree, and git's "No local changes to save" is the message "There was nothing to stash".
    /// </summary>
    private string? StashRefusal(WorkingTreeStatus status) =>
        ToolbarViewModel.StashReason(Refs?.Head ?? HeadNotRead, status) is { } reason
        && !string.Equals(reason, ToolbarViewModel.NothingToStashText, StringComparison.Ordinal)
            ? reason
            : null;

    /// <summary>A stash the menu named is no longer listed (2.25): the operation fails at once, without a git call.</summary>
    private Task StashGoneAsync(OperationText text) =>
        RunAsync(text, OperationKind.Local, RefreshScope.Refs, _ => Task.FromResult(Refusal(text, OperationTexts.StashGone)));

    /// <summary>An operation refused before git runs (D119): "&lt;Title&gt; failed", and the error dialog with <paramref name="reason"/> and no git output.</summary>
    private Task RefuseAsync(OperationText text, string reason, RefreshScope refresh) =>
        RunAsync(text, OperationKind.Local, refresh, _ => Task.FromResult(Refusal(text, reason)));

    private static OperationReport Refusal(OperationText text, string reason) =>
        Failed(text, new GitFailure(GitFailureKind.Other, reason, string.Empty, string.Empty, null));

    /// <summary>The commit a tag made at <paramref name="target"/> goes on: HEAD's, a SHA, or a ref's commit (an annotated tag peeled).</summary>
    private async Task<string?> ResolveTagTargetAsync(string? target)
    {
        if (target is null)
        {
            return Refs?.Head.Sha ?? await Git.ResolveAsync("HEAD^{commit}");
        }

        if (IsFullSha(target))
        {
            return target;
        }

        return FindRef(target)?.TargetSha ?? await Git.ResolveAsync(target + "^{commit}");
    }
}
