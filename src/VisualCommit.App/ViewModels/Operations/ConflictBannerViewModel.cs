using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The banner above the graph while an operation is in progress or files have conflicts
/// (docs/phase-3-design.md section 5, D89, D110, D113): what is in progress, how many files
/// conflict, and the Abort button. <see cref="RepositoryViewModel"/> shows each status it reads
/// here (<see cref="Show"/>); the button asks the question of section 5 and hands the abort to
/// <see cref="IInProgressActions"/>. The texts are built by the static methods, which the tests
/// and WP7's operations use too.
/// </summary>
public sealed partial class ConflictBannerViewModel : ObservableObject
{
    /// <summary>The banner's tooltip while an operation is in progress (section 5).</summary>
    public const string OperationToolTip =
        "Continue and Skip, and the conflict resolver, come in a later version. Resolve the files in your editor and stage them, or abort.";

    /// <summary>The banner's tooltip for conflicts without an operation, which offer no Abort.</summary>
    public const string ConflictsToolTip =
        "The conflict resolver comes in a later version. Resolve the files in your editor and stage them.";

    /// <summary>The detail of every Abort question (section 5): the backup of 2.15.</summary>
    public const string AbortDetail = "A backup is saved first, so this can be undone.";

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

    /// <summary>The banner is shown: an operation is in progress, or files have conflicts.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>What it says (<c>BannerText</c>).</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    /// <summary>Its tooltip.</summary>
    [ObservableProperty]
    public partial string ToolTip { get; private set; } = OperationToolTip;

    /// <summary>The Abort button's label ("Abort merge"), or empty when there is no button (conflicts without an operation).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAbort))]
    [NotifyCanExecuteChangedFor(nameof(AbortCommand))]
    public partial string AbortText { get; private set; } = string.Empty;

    /// <summary>The Abort button is shown.</summary>
    public bool HasAbort => AbortText.Length > 0;

    /// <summary>The operation the banner shows.</summary>
    public InProgressState State { get; private set; } = InProgressState.None;

    /// <summary>How many files have conflicts.</summary>
    public int Conflicts { get; private set; }

    /// <summary>The name of a branch that points at a rebase's new base (<see cref="InProgressState.Onto"/>), from the refs; null when none does (docs/phase-4-design.md 5.2).</summary>
    public string? OntoName { get; private set; }

    /// <summary>
    /// Shows a newly read status: <paramref name="state"/>, its <paramref name="conflicts"/>
    /// conflicted files, and for a rebase <paramref name="ontoName"/>, the name of a branch that
    /// points at its new base (null when none does; docs/phase-4-design.md 5.2).
    /// </summary>
    public void Show(InProgressState state, int conflicts, string? ontoName)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        Conflicts = conflicts;
        OntoName = ontoName;
        var text = TextFor(state, conflicts);
        IsVisible = text is not null;
        Text = text ?? string.Empty;
        AbortText = state.IsActive ? AbortButtonText(state.Kind) : string.Empty;
        ToolTip = state.IsActive ? OperationToolTip : ConflictsToolTip;
    }

    /// <summary>
    /// What the banner says (section 5's table), or null when it is hidden: nothing in progress
    /// and nothing conflicted. Counts use the singular for one (D113).
    /// </summary>
    public static string? TextFor(InProgressState state, int conflicts)
    {
        ArgumentNullException.ThrowIfNull(state);
        var conflictText = ConflictCount(conflicts);
        switch (state.Kind)
        {
            case InProgressKind.None:
                return conflicts == 0
                    ? null
                    : conflicts == 1
                        ? $"{conflictText} Resolve it, then stage it."
                        : $"{conflictText} Resolve them, then stage them.";

            case InProgressKind.Merge:
                var merging = state.Subject is { } subject ? $"Merging: \"{subject}\"."
                    : state.Commit is { } commit ? $"Merging {Short(commit)}."
                    : "Merging.";
                return conflicts > 0 ? $"{merging} {conflictText}" : $"{merging} Conflicts resolved: commit to finish the merge.";

            case InProgressKind.CherryPick:
            case InProgressKind.Revert:
                return PickText(state, conflicts, conflictText);

            case InProgressKind.Rebase:
                var onto = state.Onto is { } target ? $" onto {Short(target)}" : string.Empty;
                var rebasing = state.Branch is { } branch ? $"Rebasing {branch}{onto}." : $"Rebasing{onto}.";
                return conflicts > 0
                    ? $"{rebasing} {conflictText}"
                    : $"{rebasing} Conflicts resolved: continue in a terminal (git rebase --continue), or abort.";

            case InProgressKind.ApplyMailbox:
                return conflicts > 0
                    ? $"Applying patches (git am). {conflictText}"
                    : "Applying patches (git am). Continue in a terminal (git am --continue), or abort.";

            default:
                return null;
        }
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
    /// The question the Abort button asks (section 5, 2.15, D110): title "Abort the &lt;kind&gt;?",
    /// the question for the kind, the backup's detail, and the danger button "Abort &lt;kind&gt;".
    /// <paramref name="hasSequencer"/>: <c>sequencer/</c> exists, so a cherry-pick's or revert's
    /// abort also drops the commits the sequence already made, unless
    /// <see cref="InProgressState.AbortKeepsHead"/> (HEAD moved since, and git keeps it).
    /// </summary>
    public static ConfirmationRequest AbortConfirmation(InProgressState state, bool hasSequencer)
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
                    if (hasSequencer)
                    {
                        question += $" Commits this {kind} already made are dropped too.";
                    }
                }

                break;

            case InProgressKind.Rebase:
                question = state.Branch is { } branch
                    ? $"Abort the rebase of {branch}? The branch goes back to where it was before it started."
                    : "Abort the rebase? HEAD goes back to where it was before it started.";
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

    /// <summary>Continue (docs/phase-4-design.md 5.1, 5.4) for the state shown. No button shows it yet (work package WP4 adds them, 5.2).</summary>
    [RelayCommand]
    private Task Continue() => State.IsActive ? _continue(State) : Task.CompletedTask;

    /// <summary>Skip (5.1, 5.4) for the state shown. No button shows it yet (WP4).</summary>
    [RelayCommand]
    private Task Skip() => State.IsActive ? _skip(State) : Task.CompletedTask;

    private static string PickText(InProgressState state, int conflicts, string conflictText)
    {
        var cherryPick = state.Kind == InProgressKind.CherryPick;
        var (doing, done, name) = cherryPick ? ("Cherry-picking", "Cherry-pick", "cherry-pick") : ("Reverting", "Revert", "revert");
        if (state.Commit is not { } commit)
        {
            // A sequence from a terminal, stopped between commits (D110).
            return state.Remaining switch
            {
                0 => $"{done} stopped with no commits left: abort to end it.",
                1 => $"{done} stopped with 1 commit left: continue in a terminal, or abort.",
                _ => string.Create(CultureInfo.InvariantCulture, $"{done} stopped with {state.Remaining} commits left: continue in a terminal, or abort."),
            };
        }

        var what = state.Subject is { } subject ? $"{Short(commit)} \"{subject}\"" : Short(commit);
        if (state.Empty)
        {
            // A revert that changes nothing stops only on a git that keeps REVERT_HEAD (D117).
            return cherryPick
                ? $"{doing} {what} changes nothing: its changes are already here. Abort to go on."
                : $"{doing} {what} changes nothing: it is already undone. Abort to go on.";
        }

        var text = conflicts > 0 ? $"{doing} {what}. {conflictText}" : $"{doing} {what}. Conflicts resolved: commit to finish the {name}.";
        return state.Remaining switch
        {
            0 => text,
            1 => $"{text} 1 more commit waits: continue it in a terminal (git {name} --continue).",
            _ => string.Create(CultureInfo.InvariantCulture, $"{text} {state.Remaining} more commits wait: continue them in a terminal (git {name} --continue)."),
        };
    }

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
