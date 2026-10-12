using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The banner above the graph while an operation is in progress or files have conflicts
/// (docs/phase-3-design.md section 5, D89, D110, D113; docs/phase-4-design.md 5.1, 5.2, 10.3,
/// D144): what is in progress, how many files conflict, and the buttons Continue, Skip and Abort.
/// <see cref="RepositoryViewModel"/> shows each status it reads here (<see cref="Show"/>); Continue
/// hands the state to <see cref="IInProgressActions.ContinueAsync"/>, Skip and Abort ask their
/// questions first. Which buttons show, whether they are enabled and why not, the texts and the
/// tooltip are built by the static methods, which the tests, the stage panel and the operations
/// use too.
/// </summary>
public sealed partial class ConflictBannerViewModel : ObservableObject
{
    /// <summary>The detail of every Abort question (section 5): the backup of 2.15.</summary>
    public const string AbortDetail = "A backup is saved first, so this can be undone.";

    /// <summary>The Continue button's label (14.3).</summary>
    public const string ContinueText = "Continue";

    /// <summary>The Skip button's label (14.3).</summary>
    public const string SkipText = "Skip";

    private readonly Func<InProgressState, Task> _abort;
    private readonly Func<InProgressState, Task> _continue;
    private readonly Func<InProgressState, Task> _skip;

    /// <param name="abort">Asks the Abort question and aborts: <see cref="RepositoryViewModel"/>'s.</param>
    /// <param name="continue">Continue (docs/phase-4-design.md 5.4): <see cref="RepositoryViewModel"/>'s, which hands it to <see cref="IInProgressActions.ContinueAsync"/>.</param>
    /// <param name="skip">Skip (5.4): <see cref="RepositoryViewModel"/>'s, which asks 7.2's question and hands it to <see cref="IInProgressActions.SkipAsync"/>.</param>
    public ConflictBannerViewModel(Func<InProgressState, Task> abort, Func<InProgressState, Task> @continue, Func<InProgressState, Task> skip)
    {
        ArgumentNullException.ThrowIfNull(abort);
        ArgumentNullException.ThrowIfNull(@continue);
        ArgumentNullException.ThrowIfNull(skip);
        _abort = abort;
        _continue = @continue;
        _skip = skip;
    }

    /// <summary>The kinds of stop the banner tells apart (5.1's rows).</summary>
    private enum Stop
    {
        /// <summary>Conflicted files without an operation (a stash that did not apply cleanly).</summary>
        ConflictsOnly,

        Merge,

        /// <summary>A single cherry-pick or revert (no list), not empty.</summary>
        Single,

        /// <summary>A pick or revert of a list with its pick head, not empty.</summary>
        InList,

        /// <summary>A pick or revert that changes nothing, single or in a list; an empty revert of a list.</summary>
        Empty,

        /// <summary>A list paused between commits: the stopped pick was committed in a terminal (<see cref="InProgressState.AbortKeepsHead"/>, D110).</summary>
        Paused,

        /// <summary>A list stopped on a commit git could not apply without a conflict (section 4's <c>Refused</c>).</summary>
        Refused,

        Rebase,

        ApplyMailbox,
    }

    /// <summary>The banner is shown: an operation is in progress, or files have conflicts.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>What it says (<c>BannerText</c>).</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    /// <summary>The tooltip of its icon and text (10.3).</summary>
    [ObservableProperty]
    public partial string ToolTip { get; private set; } = string.Empty;

    /// <summary>The Abort button's label ("Abort merge"), or empty when there is no button (conflicts without an operation).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAbort))]
    [NotifyCanExecuteChangedFor(nameof(AbortCommand))]
    public partial string AbortText { get; private set; } = string.Empty;

    /// <summary>The Abort button is shown.</summary>
    public bool HasAbort => AbortText.Length > 0;

    /// <summary>The Continue button (<c>BannerContinueButton</c>) is shown (5.1).</summary>
    [ObservableProperty]
    public partial bool HasContinue { get; private set; }

    /// <summary>Continue is enabled; otherwise <see cref="ContinueToolTip"/> says why.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial bool CanContinue { get; private set; }

    /// <summary>Why Continue is disabled (10.4), shown as its tooltip on the disabled button; null while it is enabled.</summary>
    [ObservableProperty]
    public partial string? ContinueToolTip { get; private set; }

    /// <summary>The Skip button (<c>BannerSkipButton</c>) is shown (5.1).</summary>
    [ObservableProperty]
    public partial bool HasSkip { get; private set; }

    /// <summary>Skip is enabled; otherwise <see cref="SkipToolTip"/> says why.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SkipCommand))]
    public partial bool CanSkip { get; private set; }

    /// <summary>Why Skip is disabled (10.4); null while it is enabled.</summary>
    [ObservableProperty]
    public partial string? SkipToolTip { get; private set; }

    /// <summary>The operation the banner shows.</summary>
    public InProgressState State { get; private set; } = InProgressState.None;

    /// <summary>How many files have conflicts.</summary>
    public int Conflicts { get; private set; }

    /// <summary>The name of a branch that points at a rebase's new base (<see cref="InProgressState.Onto"/>), from the refs; null when none does (docs/phase-4-design.md 5.2).</summary>
    public string? OntoName { get; private set; }

    /// <summary>
    /// Shows a newly read status: <paramref name="state"/>, its <paramref name="conflicts"/>
    /// conflicted files, for a rebase <paramref name="ontoName"/>, the name of a branch that
    /// points at its new base (null when none does; docs/phase-4-design.md 5.2, 14.3), and
    /// <paramref name="currentName"/>, what HEAD is on as the menus name it (<c>{cur}</c> of the
    /// tooltip, 10.3; "HEAD" when null).
    /// </summary>
    public void Show(InProgressState state, int conflicts, string? ontoName, string? currentName = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        Conflicts = conflicts;
        OntoName = ontoName;
        var text = TextFor(state, conflicts, ontoName);
        IsVisible = text is not null;
        Text = text ?? string.Empty;
        AbortText = state.IsActive ? AbortButtonText(state.Kind) : string.Empty;
        ToolTip = ToolTipFor(state, conflicts, currentName ?? "HEAD");

        var (hasContinue, continueReason) = ContinueFor(state, conflicts);
        (HasContinue, CanContinue, ContinueToolTip) = (hasContinue, hasContinue && continueReason is null, continueReason);
        var (hasSkip, skipReason) = SkipFor(state);
        (HasSkip, CanSkip, SkipToolTip) = (hasSkip, hasSkip && skipReason is null, skipReason);
    }

    /// <summary>
    /// What the banner says (docs/phase-3-design.md section 5's table, as docs/phase-4-design.md 5.2
    /// changes it), or null when it is hidden: nothing in progress and nothing conflicted. Counts
    /// use the singular for one (D113). <paramref name="ontoName"/> names a rebase's new base; its
    /// short SHA when null.
    /// </summary>
    public static string? TextFor(InProgressState state, int conflicts, string? ontoName = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var conflictText = ConflictCount(conflicts);
        switch (StopOf(state, conflicts))
        {
            case null:
                return null;

            case Stop.ConflictsOnly:
                return conflicts == 1
                    ? $"{conflictText} Resolve it, then stage it."
                    : $"{conflictText} Resolve them, then stage them.";

            case Stop.Merge:
                var merging = state.Subject is { } subject ? $"Merging: \"{subject}\"."
                    : state.Commit is { } commit ? $"Merging {Short(commit)}."
                    : "Merging.";
                return conflicts > 0 ? $"{merging} {conflictText}" : $"{merging} Conflicts resolved: commit to finish the merge.";

            case Stop.Rebase:
                return RebaseText(state, conflicts, ontoName);

            case Stop.ApplyMailbox:
                return conflicts > 0
                    ? $"Applying patches (git am). {conflictText}"
                    : "Applying patches (git am). Continue in a terminal (git am --continue), or abort.";

            case var stop:
                return PickText(stop.Value, state, conflicts, conflictText);
        }
    }

    /// <summary>
    /// The tooltip of the banner's icon and text (10.3): what Continue, Skip and Abort do for
    /// <paramref name="state"/>; <paramref name="currentName"/> is <c>{cur}</c>.
    /// </summary>
    public static string ToolTipFor(InProgressState state, int conflicts, string currentName)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(currentName);
        var kind = KindName(state.Kind);
        return StopOf(state, conflicts) switch
        {
            Stop.Merge => $"Continue commits the merge with the message in the stage panel. Abort puts {currentName} back where it was before the merge.",
            Stop.Single or Stop.Empty when !InList(state) =>
                $"Continue commits the resolved files with the message in the stage panel. Skip leaves this commit out. Abort puts {currentName} back where it was before the {kind}.",
            Stop.InList or Stop.Empty or Stop.Refused =>
                $"Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts {currentName} back where it was before the {kind}: the commits it already made are dropped too.",
            Stop.Paused => $"Continue picks the commits that wait. Abort ends the {kind} and keeps {currentName} and the commits made so far.",
            Stop.Rebase => $"Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts {state.Branch ?? "HEAD"} back where it was before the rebase.",
            Stop.ApplyMailbox => "Resolve the files and stage them, then continue in a terminal (git am --continue), or abort.",
            _ => "Click a conflicted file to resolve it, then mark it resolved.",
        };
    }

    /// <summary>
    /// Whether the banner offers Continue for <paramref name="state"/> with
    /// <paramref name="conflicts"/> conflicted files (5.1), and why it is disabled (10.4; null
    /// while it is enabled). The stage panel's "Continue …" button follows it (5.3).
    /// </summary>
    public static (bool Shown, string? DisabledReason) ContinueFor(InProgressState state, int conflicts)
    {
        ArgumentNullException.ThrowIfNull(state);
        var resolveFirst = conflicts > 0 ? ResolveConflictsFirst(conflicts) : null;
        var shortSha = Short(state.Commit ?? string.Empty);
        switch (StopOf(state, conflicts))
        {
            case Stop.Merge or Stop.Single or Stop.InList:
                return (true, resolveFirst);

            case Stop.Empty:
                return (true, ChangesNothing(state.Kind));

            case Stop.Paused:
                return (true, null);

            case Stop.Refused:
                return (true, $"Git would leave {shortSha} out without a word: skip it or abort");

            case Stop.Rebase:
                switch (state.StopKind)
                {
                    case RebaseStopKind.Rescheduled or RebaseStopKind.ExecFailed or RebaseStopKind.OwnAmendFailed or RebaseStopKind.Edit:
                        return (true, null);

                    case var _ when state.NextIsSquash:
                        return (true, resolveFirst ?? (state.StopKind == RebaseStopKind.Empty
                            ? $"{shortSha} changes nothing now, and git would join the commits squashed into it to the commit before it: abort, or change it"
                            : null));

                    case RebaseStopKind.Empty:
                        return (true, null);

                    default:
                        return (true, resolveFirst);
                }

            default:
                // A merge's Skip, git am and conflicts without an operation are continued elsewhere.
                return (false, null);
        }
    }

    /// <summary>Whether the banner offers Skip for <paramref name="state"/> (5.1), and why it is disabled (10.4; null while enabled).</summary>
    public static (bool Shown, string? DisabledReason) SkipFor(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var shortSha = Short(state.Commit ?? string.Empty);
        return StopOf(state, 0) switch
        {
            Stop.Single or Stop.InList or Stop.Empty or Stop.Refused => (true, null),
            Stop.Paused => (true, "There is nothing to skip: the stopped commit was committed"),
            Stop.Rebase => state.StopKind switch
            {
                RebaseStopKind.Rescheduled => (true, $"Skip would try {shortSha} again: move or remove what is in the way, or abort"),
                RebaseStopKind.ExecFailed => (true, "There is nothing to skip: continue to go on"),
                RebaseStopKind.Edit => (true, "Skip does not leave out a commit that is being edited: continue or abort"),
                RebaseStopKind.OwnAmendFailed => (true, null),
                _ when state.NextIsSquash => (true, $"Skipping {shortSha} would join the commits squashed into it to the commit before it: abort, or resolve it and continue"),
                _ => (true, null),
            },
            _ => (false, null),
        };
    }

    /// <summary>"Resolve the conflicts first (1 file)": Continue's reason while files conflict (10.4, D113).</summary>
    public static string ResolveConflictsFirst(int conflicts) =>
        $"{ToolbarViewModel.ResolveConflictsText} ({OperationTexts.Count(conflicts, "file")})";

    /// <summary>"This cherry-pick changes nothing: skip it" (or revert): Continue's reason for an empty stop, and the stage panel's (5.1, 5.3).</summary>
    public static string ChangesNothing(InProgressKind kind) => $"This {KindName(kind)} changes nothing: skip it";

    /// <summary>
    /// Whether <paramref name="state"/> is a list of picks or reverts (5.3): the stage panel's
    /// button continues it ("Continue cherry-pick") instead of committing it. A pick or revert
    /// with commits after it, or git's <c>sequencer/</c> folder.
    /// </summary>
    public static bool InList(InProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Kind is InProgressKind.CherryPick or InProgressKind.Revert && (state.Sequenced || state.Remaining > 0 || state.AbortKeepsHead);
    }

    /// <summary>The Abort button's label (section 5's table): "Abort merge", …, "Abort" for <c>git am</c>.</summary>
    public static string AbortButtonText(InProgressKind kind) => kind switch
    {
        InProgressKind.Merge => "Abort merge",
        InProgressKind.CherryPick => "Abort cherry-pick",
        InProgressKind.Revert => "Abort revert",
        InProgressKind.Rebase => "Abort rebase",
        InProgressKind.ApplyMailbox => "Abort",
        _ => string.Empty,
    };

    /// <summary>The operation's name in running text: "merge", "cherry-pick", "revert", "rebase", "git am".</summary>
    public static string KindName(InProgressKind kind) => ToolbarViewModel.InProgressName(kind);

    /// <summary>
    /// The question the Abort button asks (section 5, 2.15, D110; docs/phase-4-design.md 1.3,
    /// 12.1, 14.3): title "Abort the &lt;kind&gt;?", the question for the kind, the backup's
    /// detail, and the danger button "Abort &lt;kind&gt;". A pick or revert of a list (git's
    /// <c>sequencer/</c>, <see cref="InProgressState.Sequenced"/>) also drops the commits it
    /// already made, unless <see cref="InProgressState.AbortKeepsHead"/> (HEAD moved since, and git
    /// keeps it). <paramref name="returnBranch"/>: the branch a rebase begun with "Check out {A}
    /// and rebase it" checks out again after the abort (<c>rebase-merge/visualcommit-return</c>),
    /// by its short name; null for none.
    /// </summary>
    public static ConfirmationRequest AbortConfirmation(InProgressState state, string? returnBranch = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var kind = KindName(state.Kind);
        var title = state.Kind == InProgressKind.ApplyMailbox ? "Abort git am?" : $"Abort the {kind}?";
        string question;
        switch (state.Kind)
        {
            case InProgressKind.Merge:
                question = state.Subject is { } subject
                    ? $"Abort merging \"{subject}\"? Changes made to resolve conflicts are lost."
                    : "Abort the merge? Changes made to resolve conflicts are lost.";
                break;

            case InProgressKind.CherryPick:
            case InProgressKind.Revert:
                if (state.AbortKeepsHead)
                {
                    var done = state.Kind == InProgressKind.CherryPick ? "picked" : "reverted";
                    var left = state.Remaining switch
                    {
                        0 => string.Empty,
                        1 => $" The 1 commit left is not {done}.",
                        _ => string.Create(CultureInfo.InvariantCulture, $" The {state.Remaining} commits left are not {done}."),
                    };
                    question = $"Abort the {kind}?{left} HEAD and the commits made so far stay as they are.";
                }
                else
                {
                    question = $"Abort the {kind}? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.";
                    if (state.Sequenced)
                    {
                        question += $" Commits this {kind} already made are dropped too.";
                    }
                }

                break;

            case InProgressKind.Rebase:
                var again = returnBranch is null ? "." : $", and {returnBranch} is checked out again.";
                question = state.Branch is { } branch
                    ? $"Abort the rebase of {branch}? The branch goes back to where it was before it started{again}"
                    : $"Abort the rebase? HEAD goes back to where it was before it started{again}";
                break;

            case InProgressKind.ApplyMailbox:
                question = "Abort git am? HEAD goes back to where it was before it started, and the patches not applied yet are dropped.";
                break;

            default:
                throw new ArgumentException("Nothing is in progress: there is nothing to abort.", nameof(state));
        }

        return new ConfirmationRequest(title, question, AbortDetail, $"Abort {kind}");
    }

    /// <summary>"1 file has conflicts." / "N files have conflicts." (D113).</summary>
    public static string ConflictCount(int conflicts) => conflicts == 1
        ? "1 file has conflicts."
        : string.Create(CultureInfo.InvariantCulture, $"{conflicts} files have conflicts.");

    /// <summary>The Abort button: asks section 5's question, then aborts. Disabled while it runs.</summary>
    [RelayCommand(CanExecute = nameof(HasAbort))]
    private Task Abort() => State.IsActive ? _abort(State) : Task.CompletedTask;

    /// <summary>Continue (docs/phase-4-design.md 5.1, 5.4) for the state shown. Disabled while it runs, and while <see cref="CanContinue"/> is false.</summary>
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private Task Continue() => State.IsActive ? _continue(State) : Task.CompletedTask;

    /// <summary>Skip (5.1, 5.4) for the state shown, after its question. Disabled while it runs, and while <see cref="CanSkip"/> is false.</summary>
    [RelayCommand(CanExecute = nameof(CanSkip))]
    private Task Skip() => State.IsActive ? _skip(State) : Task.CompletedTask;

    /// <summary>Which of 5.1's rows <paramref name="state"/> is; null when the banner is hidden.</summary>
    private static Stop? StopOf(InProgressState state, int conflicts)
    {
        switch (state.Kind)
        {
            case InProgressKind.None:
                return conflicts > 0 ? Stop.ConflictsOnly : null;
            case InProgressKind.Merge:
                return Stop.Merge;
            case InProgressKind.Rebase:
                return Stop.Rebase;
            case InProgressKind.ApplyMailbox:
                return Stop.ApplyMailbox;
            case InProgressKind.CherryPick or InProgressKind.Revert:
                // A list without a pick head names the commit that waits (design 8): committed in
                // a terminal (paused), an empty revert (MERGE_MSG left), or refused by git.
                if ((state.Sequenced && !state.HasPickHead) || state.Commit is null)
                {
                    return state.AbortKeepsHead || state.Commit is null ? Stop.Paused
                        : state.Empty ? Stop.Empty
                        : Stop.Refused;
                }

                return state.Empty ? Stop.Empty : InList(state) ? Stop.InList : Stop.Single;
            default:
                return null;
        }
    }

    private static string PickText(Stop stop, InProgressState state, int conflicts, string conflictText)
    {
        var cherryPick = state.Kind == InProgressKind.CherryPick;
        var (doing, done, name, verb) = cherryPick
            ? ("Cherry-picking", "Cherry-pick", "cherry-pick", "picks")
            : ("Reverting", "Revert", "revert", "reverts");
        var what = state.Subject is { } subject ? $"{Short(state.Commit ?? string.Empty)} \"{subject}\"" : Short(state.Commit ?? string.Empty);
        var waiting = state.Remaining switch
        {
            0 => string.Empty,
            1 => $" 1 more commit waits: Continue {verb} it.",
            _ => string.Create(CultureInfo.InvariantCulture, $" {state.Remaining} more commits wait: Continue {verb} them."),
        };

        switch (stop)
        {
            case Stop.Paused:
                return state.Remaining switch
                {
                    0 => $"{done} paused with no commits left: continue to end it.",
                    1 => $"{done} paused: 1 more commit waits. Continue {verb} it.",
                    _ => string.Create(CultureInfo.InvariantCulture, $"{done} paused: {state.Remaining} more commits wait. Continue {verb} them."),
                };

            case Stop.Refused:
                return $"{done} stopped at {what}: git could not {(cherryPick ? "pick" : "revert")} it. Skip it, or abort.";

            case Stop.Empty:
                var empty = cherryPick
                    ? $"{doing} {what} changes nothing: its changes are already here. Skip it to go on."
                    : $"{doing} {what} changes nothing: it is already undone. Skip it to go on.";
                return empty + waiting;

            case Stop.InList:
                return (conflicts > 0 ? $"{doing} {what}. {conflictText}" : $"{doing} {what}. Conflicts resolved: continue to commit it.") + waiting;

            default:
                return conflicts > 0 ? $"{doing} {what}. {conflictText}" : $"{doing} {what}. Conflicts resolved: commit to finish the {name}.";
        }
    }

    /// <summary>A rebase's text (5.2): its branch and new base, the stopped commit and where it is in the rebase, then what to do.</summary>
    private static string RebaseText(InProgressState state, int conflicts, string? ontoName)
    {
        var onto = ontoName ?? (state.Onto is { } target ? Short(target) : null);
        var head = (state.Branch, onto) switch
        {
            ({ } branch, { } name) => $"Rebasing {branch} onto {name}",
            ({ } branch, null) => $"Rebasing {branch}",
            (null, { } name) => $"Rebasing onto {name}",
            _ => "Rebasing",
        };
        var shortSha = state.Commit is { } commit ? Short(commit) : null;
        var what = (shortSha, state.Subject) switch
        {
            ({ } sha, { } subject) => $" {sha} \"{subject}\"",
            ({ } sha, null) => $" {sha}",
            _ => string.Empty,
        };
        var position = state.Total > 0 ? string.Create(CultureInfo.InvariantCulture, $" ({state.Done} of {state.Total})") : string.Empty;

        switch (state.StopKind)
        {
            case RebaseStopKind.Rescheduled:
                return $"{head}: git could not apply{what}{position}: an untracked file is in the way. Move or remove it, then continue.";
            case RebaseStopKind.OwnAmendFailed:
                return $"{head}: the new message of {shortSha} was not applied{position}. Continue tries again; Skip goes on without it.";
            case RebaseStopKind.ExecFailed:
                return $"{head}: a command failed after {shortSha}{position}. Continue goes on.";
            case RebaseStopKind.Edit:
                return $"{head}: stopped to edit{what}{position}. Continue when ready.";
        }

        var stopped = $"{head}: stopped at{what}{position}.";
        if (conflicts > 0)
        {
            return $"{stopped} {ConflictCount(conflicts)}";
        }

        if (state.StopKind == RebaseStopKind.Empty)
        {
            return state.NextIsSquash
                ? $"{stopped} It changes nothing now: abort, or change it."
                : $"{stopped} It changes nothing now: continue to leave it out.";
        }

        return $"{stopped} Conflicts resolved: continue to commit it and go on.";
    }

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
