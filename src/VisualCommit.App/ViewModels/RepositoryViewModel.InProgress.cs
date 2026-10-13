using System.Globalization;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// Work package WP9's part of the repository's view model (docs/phase-3-design.md section 5, 6.3),
/// with phase 4's (WP4: docs/phase-4-design.md 5.1 to 5.4, 7.2): the conflict banner and the
/// working-changes row's text, which follow every status read (<see cref="OnStatusApplied"/>);
/// the Abort and Skip questions; the stage panel's Continue and the message it fills in for a
/// stopped commit; the questions about conflict markers; the left panel's selection, which
/// follows the graph's (<see cref="OnGraphSelectionChanged"/>, D98); and what auto-fetch needs to
/// know of the repository.
/// </summary>
public sealed partial class RepositoryViewModel : IConflictMarkerQuestions
{
    private ConflictBannerViewModel? _banner;
    private IInProgressActions? _inProgressActions;

    /// <summary>The banner above the graph or the diff (section 5).</summary>
    public ConflictBannerViewModel Banner => _banner ??= new ConflictBannerViewModel(AbortInProgressAsync, ContinueInProgressAsync, SkipInProgressAsync);

    /// <summary>
    /// Where the banner's Abort, Continue and Skip go after their questions: <see cref="Operations"/>
    /// once it implements <see cref="IInProgressActions"/> (WP7), or what a test set.
    /// </summary>
    public IInProgressActions? InProgressActions
    {
        get => _inProgressActions ?? (object?)Operations as IInProgressActions;
        set => _inProgressActions = value;
    }

    /// <summary>How long ago opening the repository began: auto-fetch's first fetch comes one interval after it (6.1).</summary>
    internal TimeSpan TimeSinceOpening => _sinceOpening.Elapsed;

    /// <summary>
    /// <c>MERGE_MSG</c> as git left it in the repository's git folder (section 5), or null when
    /// there is none. The stage panel fills an empty message with
    /// <see cref="ReadOperationMessageAsync"/>, which leaves out git's comment block.
    /// </summary>
    public async Task<string?> ReadMergeMessageAsync()
    {
        var path = System.IO.Path.Combine(Repository.GitDirectory, "MERGE_MSG");
        try
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, _lifetime.Token) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// The message the stage panel fills in for <paramref name="state"/>'s stopped commit
    /// (docs/phase-4-design.md 5.3, <see cref="IGitRepository.ReadOperationMessageAsync"/>):
    /// <c>MERGE_MSG</c> less git's comment block, a rebase's own message, or a group's; null when
    /// there is none or it cannot be read.
    /// </summary>
    public async Task<string?> ReadOperationMessageAsync(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            return await Repository.ReadOperationMessageAsync(state, options: null, _lifetime.Token);
        }
        catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _log.Warning($"{Name}: the message of the stopped commit could not be read.", ex);
            return null;
        }
    }

    /// <summary>
    /// The stage panel's "Continue cherry-pick", "Continue revert" or "Continue rebase" (5.3): the
    /// banner's Continue for the state shown, with <paramref name="message"/> (null: the
    /// operation's own). Its failures open the error dialog; the stage panel shows nothing.
    /// </summary>
    public async Task<string?> ContinueAsync(string? message)
    {
        var state = Status.InProgress;
        if (!state.IsActive || InProgressActions is not { } actions)
        {
            return null;
        }

        try
        {
            await actions.ContinueAsync(state, message);
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: continuing the {ConflictBannerViewModel.KindName(state.Kind)} from the stage panel failed.", ex);
        }

        return null;
    }

    /// <inheritdoc/>
    public Task<bool> ConfirmStagingAsync(IReadOnlyList<ChangedFile> files) => Operations.ConfirmStagingWithMarkersAsync(files);

    /// <inheritdoc/>
    public Task<bool> ConfirmConcludingAsync(InProgressState state) => Operations.ConfirmStagedMarkersAsync(state);

    /// <summary>What the working-changes row says (section 5): "Working changes · 2 conflicts" while files conflict.</summary>
    public static string WorkingRowTextFor(int conflicts) => conflicts switch
    {
        0 => "Working changes",
        1 => "Working changes · 1 conflict",
        _ => string.Create(CultureInfo.InvariantCulture, $"Working changes · {conflicts} conflicts"),
    };

    partial void OnStatusApplied(WorkingTreeStatus status)
    {
        var conflicts = status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);
        WorkingRowText = WorkingRowTextFor(conflicts);
        Operations.NoteStatus(status);
        Operations.NoteUndoState(status);
        Banner.Show(status.InProgress, conflicts, OntoNameOf(status.InProgress), Refs?.Head.BranchName ?? "HEAD", status.HasUnstagedTrackedChanges);
    }

    /// <summary>
    /// The name of a branch that points at a rebase's new base (docs/phase-4-design.md 5.2), from
    /// the refs as last read: a local branch first, then a remote branch, each in git's order;
    /// null for any other operation, or when no branch points there (the banner then shows the
    /// short SHA).
    /// </summary>
    private string? OntoNameOf(InProgressState state)
    {
        if (state is not { Kind: InProgressKind.Rebase, Onto: { } onto } || Refs is not { } refs)
        {
            return null;
        }

        return refs.Refs.FirstOrDefault(gitRef => gitRef.Kind == RefKind.LocalBranch && gitRef.TargetSha == onto)?.Name
            ?? refs.Refs.FirstOrDefault(gitRef => gitRef.Kind == RefKind.RemoteBranch && gitRef.TargetSha == onto)?.Name;
    }

    partial void OnGraphSelectionChanged(GraphSelection selection) => LeftPanel.SyncSelection(selection.Single);

    /// <summary>
    /// The banner's Abort: asks section 5's question (with the branch a "Check out and rebase"
    /// checks out again, 1.3), then hands the abort to <see cref="InProgressActions"/>.
    /// </summary>
    private async Task AbortInProgressAsync(InProgressState state)
    {
        try
        {
            if (!await AskAsync(Operations.AbortConfirmation(state)))
            {
                return;
            }

            if (InProgressActions is not { } actions)
            {
                _log.Warning($"{Name}: the banner's Abort has nothing to run.");
                return;
            }

            await actions.AbortAsync(state);
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: aborting the {ConflictBannerViewModel.KindName(state.Kind)} failed.", ex);
        }
    }

    /// <summary>
    /// The banner's Continue (docs/phase-4-design.md 5.4): hands it to <see cref="InProgressActions"/>
    /// with the stage panel's message, exactly as the stage panel's own Continue does
    /// (<see cref="WorkingChangesViewModel.ContinueMessage"/>: the message the user changed; null,
    /// the operation's own, while the boxes hold what was filled in or the summary is empty).
    /// </summary>
    private async Task ContinueInProgressAsync(InProgressState state)
    {
        try
        {
            if (InProgressActions is not { } actions)
            {
                _log.Warning($"{Name}: the banner's Continue has nothing to run.");
                return;
            }

            await Changes.ContinueWithMessageAsync(message => actions.ContinueAsync(state, message));
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: continuing the {ConflictBannerViewModel.KindName(state.Kind)} failed.", ex);
        }
    }

    /// <summary>
    /// The banner's Skip (5.4): asks 7.2's question ("Skip {short}?", D144), as Abort asks its own,
    /// then hands it to <see cref="InProgressActions"/>, which saves the backup and skips. A
    /// message the user typed in the stage panel for the skipped commit goes with it
    /// (<see cref="WorkingChangesViewModel.SkipClearingMessageAsync"/>).
    /// </summary>
    private async Task SkipInProgressAsync(InProgressState state)
    {
        try
        {
            if (!await AskAsync(OperationTexts.ConfirmSkip(state)))
            {
                return;
            }

            if (InProgressActions is not { } actions)
            {
                _log.Warning($"{Name}: the banner's Skip has nothing to run.");
                return;
            }

            await Changes.SkipClearingMessageAsync(() => actions.SkipAsync(state));
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: skipping in the {ConflictBannerViewModel.KindName(state.Kind)} failed.", ex);
        }
    }
}
