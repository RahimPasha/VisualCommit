using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Rebase;

/// <summary>
/// The interactive rebase's list (docs/phase-4-design.md 2.2, D141), shown in the graph area in
/// place of the graph by <c>Views/Rebase/InteractiveRebaseView</c>: the commits git's todo would
/// hold, newest on top, then the base row; an action per row (Pick, Reword, Squash, Fixup, Drop)
/// from its action button's menu or git's letters; rows moved by a drag or Alt+Up and Alt+Down;
/// squash and fixup rows joining the nearest Pick or Reword below them; new messages through the
/// message dialog; Start, which hands the git layer a <see cref="RebasePlan"/>; Cancel and Esc,
/// which ask first when the list was changed.
/// <para>
/// It never runs git: everything that reaches outside the list goes through its
/// <see cref="IInteractiveRebaseHost"/> (the repository). Every member is used on the UI thread.
/// </para>
/// </summary>
public sealed partial class InteractiveRebaseViewModel : ObservableObject
{
    /// <summary>The most commits one list holds (2.1): more are refused with <see cref="TooManyMessage"/>.</summary>
    public const int MaxCommits = 1000;

    /// <summary>Start's reason while every row is still Pick in its first order (10.4).</summary>
    public const string UnchangedReason = "Change an action or the order first";

    /// <summary>Start's reason while HEAD is no longer on the branch at the tip the list was read from (10.4).</summary>
    public const string HeadMovedReason = "HEAD moved: open the list again";

    /// <summary>Squash's and Fixup's reason on the bottom row (10.4).</summary>
    public const string NothingBelowReason = "The oldest commit has nothing below it to join";

    /// <summary>
    /// Squash's and Fixup's reason on a row whose rows below are all dropped: no Pick or Reword
    /// below it to join. The design names only the bottom row's reason; this case has the same
    /// cause, and a squash there would have no target and turn back into Pick at once.
    /// </summary>
    public const string AllBelowDroppedReason = "Every commit below it is dropped: there is nothing to join";

    /// <summary>The question on leaving a changed list (7.2), asked by Esc, Cancel and the repository's leaving rule.</summary>
    public const string DiscardTitle = "Discard the changes to this list?";

    /// <summary>The question's button that leaves (danger).</summary>
    public const string DiscardText = "Discard";

    /// <summary>The question's button that stays.</summary>
    public const string KeepEditingText = "Keep editing";

    /// <summary>The message dialog's title for a group's message (2.2, 7.2).</summary>
    public const string SquashedMessageTitle = "Message of the squashed commit";

    /// <summary>The message dialog's confirm button in the list (7.2).</summary>
    public const string SaveText = "Save";

    /// <summary>The actions in the order the row's menu lists them (2.2).</summary>
    public static IReadOnlyList<RebaseAction> MenuActions { get; } =
        [RebaseAction.Pick, RebaseAction.Reword, RebaseAction.Squash, RebaseAction.Fixup, RebaseAction.Drop];

    private readonly IInteractiveRebaseHost _host;
    private readonly List<InteractiveRebaseRowViewModel> _rows;
    private readonly ObservableCollection<object> _items;
    private readonly IReadOnlyList<string> _firstOrder;
    private bool _asking;

    /// <summary>
    /// A list for <paramref name="request"/> holding what <paramref name="list"/> read (2.3). It
    /// opens with its top row selected, and shows that commit in the right panel at once
    /// (<see cref="IInteractiveRebaseHost.ShowDetails"/>). When the list cannot be made (merges in
    /// the range, no commits of the branch's own, more than <see cref="MaxCommits"/>), it holds
    /// no rows and says why in <see cref="Message"/>.
    /// </summary>
    /// <param name="request">What the menu item asked for: the target, the branch, "from here", a checkout first.</param>
    /// <param name="list">The commits git's todo would hold, read when the list opened.</param>
    /// <param name="baseSubject">The subject of the base commit (<see cref="RebaseList.Onto"/>), for the base row; empty when unknown.</param>
    /// <param name="host">The repository: Start, the details, the questions and dialogs, closing.</param>
    public InteractiveRebaseViewModel(InteractiveRebaseRequest request, RebaseList list, string baseSubject, IInteractiveRebaseHost host)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(baseSubject);
        ArgumentNullException.ThrowIfNull(host);
        Request = request;
        List = list;
        _host = host;

        BranchName = request.BranchRef.StartsWith("refs/heads/", StringComparison.Ordinal) ? request.BranchRef["refs/heads/".Length..] : request.BranchRef;
        HeaderText = request.FromHere
            ? $"Interactive rebase of {BranchName} from {request.Onto.Name}"
            : $"Interactive rebase of {BranchName} onto {request.Onto.Name}";

        var commits = list.CommitsOldestFirst;
        Message = list.MergeCount > 0 ? $"A merge lies between {request.Onto.Name} and {BranchName}: interactive rebase would flatten it."
            : commits.Count == 0 ? $"{BranchName} has no commits of its own after {request.Onto.Name}."
            : commits.Count > MaxCommits ? string.Create(CultureInfo.InvariantCulture, $"{commits.Count} commits are too many for one interactive rebase.")
            : null;

        _rows = Message is null ? [.. commits.Reverse().Select(entry => new InteractiveRebaseRowViewModel(entry))] : [];
        _firstOrder = [.. _rows.Select(row => row.Sha)];
        BaseRow = new InteractiveRebaseBaseRow(list.Onto, baseSubject);
        _items = [.. _rows];
        if (Message is null)
        {
            _items.Add(BaseRow);
        }

        Items = new ReadOnlyObservableCollection<object>(_items);
        CountText = _rows.Count == 1 ? "1 commit" : string.Create(CultureInfo.InvariantCulture, $"{_rows.Count} commits");

        Refresh();
        if (_rows.Count > 0)
        {
            Select(_rows[0]);
        }
    }

    /// <summary>What the menu item asked for.</summary>
    public InteractiveRebaseRequest Request { get; }

    /// <summary>The commits as they were read (2.3): what Start rebases onto and the tip it expects.</summary>
    public RebaseList List { get; }

    /// <summary>The short name of the branch being rebased (<c>{b}</c>).</summary>
    public string BranchName { get; }

    /// <summary>
    /// The header (<c>RebaseListHeader</c>): <c>Interactive rebase of {b} onto {target}</c>, or for
    /// "from here" <c>Interactive rebase of {cur} from {short}</c> (2.2).
    /// </summary>
    public string HeaderText { get; }

    /// <summary>The count at the header's right (<c>RebaseListCount</c>): the listed rows, whatever their actions (14.3).</summary>
    public string CountText { get; }

    /// <summary>Why the list cannot be made (<c>RebaseListMessage</c>, 2.1), shown in the rows' place with only Cancel; null for a list.</summary>
    public string? Message { get; }

    /// <summary>The list cannot be made: <see cref="Message"/> is shown, no rows, no Start, no count.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>The commit rows, newest on top (the graph's order).</summary>
    public IReadOnlyList<InteractiveRebaseRowViewModel> Rows => _rows;

    /// <summary>The base row, under the last commit row.</summary>
    public InteractiveRebaseBaseRow BaseRow { get; }

    /// <summary>What the view's list draws: the commit rows, then the base row (none when <see cref="HasMessage"/>).</summary>
    public ReadOnlyObservableCollection<object> Items { get; }

    /// <summary>The selected row, or null when the list has none.</summary>
    [ObservableProperty]
    public partial InteractiveRebaseRowViewModel? SelectedRow { get; private set; }

    /// <summary>
    /// The rows' actions or order differ from the list's first state (14.3); a list moved back to
    /// it is not changed. Leaving a changed list asks first; Start needs it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(StartToolTip))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsChanged { get; private set; }

    /// <summary>HEAD is no longer on the branch at the tip the list was read from (for a branch checked out first: the branch moved). Start is disabled (10.4).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(StartToolTip))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsHeadMoved { get; private set; }

    /// <summary>Start's operation waits or runs: the rows and buttons are disabled (2.2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(StartToolTip), nameof(IsInteractive))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(CancelCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>The list has closed (Start's rebase ran, Cancel, Esc): it takes no more input.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(IsInteractive))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(CancelCommand))]
    public partial bool IsClosed { get; private set; }

    /// <summary>The rows take input: not while Start runs, nor once the list has closed.</summary>
    public bool IsInteractive => !IsBusy && !IsClosed;

    /// <summary>Start rebase (<c>RebaseStartButton</c>) is enabled.</summary>
    public bool CanStart => IsInteractive && !HasMessage && IsChanged && !IsHeadMoved;

    /// <summary>Start's tooltip: its disabled reason in the design's order (10.4), or null while it is enabled.</summary>
    public string? StartToolTip => HasMessage || IsBusy || IsClosed ? null
        : !IsChanged ? UnchangedReason
        : IsHeadMoved ? HeadMovedReason
        : null;

    /// <summary>
    /// Raised when the view should give the keyboard focus to the selected row: after a diff
    /// opened over the list closes (14.3). The repository calls <see cref="RequestFocus"/>.
    /// </summary>
    public event EventHandler? FocusRequested;

    /// <summary>The text of an action, as the action button and the row snapshot show it.</summary>
    public static string ActionName(RebaseAction action) => action switch
    {
        RebaseAction.Pick => "Pick",
        RebaseAction.Reword => "Reword",
        RebaseAction.Squash => "Squash",
        RebaseAction.Fixup => "Fixup",
        RebaseAction.Drop => "Drop",
        _ => action.ToString(),
    };

    /// <summary>The label of an action in the row's menu: <c>Reword…</c> opens a dialog (2.2, 7.1).</summary>
    public static string MenuLabel(RebaseAction action) => action == RebaseAction.Reword ? "Reword…" : ActionName(action);

    /// <summary>The position of <paramref name="row"/> from the top, or -1.</summary>
    public int IndexOf(InteractiveRebaseRowViewModel row) => _rows.IndexOf(row);

    /// <summary>Gives the keyboard focus back to the selected row (<see cref="FocusRequested"/>).</summary>
    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Selects <paramref name="row"/> and shows its commit in the right panel
    /// (<see cref="IInteractiveRebaseHost.ShowDetails"/>), without closing the list (2.2).
    /// </summary>
    public void Select(InteractiveRebaseRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (IsClosed || !_rows.Contains(row))
        {
            return;
        }

        if (!ReferenceEquals(SelectedRow, row))
        {
            if (SelectedRow is { } previous)
            {
                previous.IsSelected = false;
            }

            row.IsSelected = true;
            SelectedRow = row;
        }

        _host.ShowDetails(row.Sha);
    }

    /// <summary>Up and Down: selects the row <paramref name="delta"/> rows away, staying on the first or last. Returns whether the selection moved.</summary>
    public bool MoveSelection(int delta)
    {
        if (!IsInteractive || SelectedRow is not { } selected)
        {
            return false;
        }

        var index = Math.Clamp(_rows.IndexOf(selected) + delta, 0, _rows.Count - 1);
        if (ReferenceEquals(_rows[index], selected))
        {
            return false;
        }

        Select(_rows[index]);
        return true;
    }

    /// <summary>Home: selects the top row.</summary>
    public bool SelectFirst() => SelectAt(0);

    /// <summary>End: selects the bottom commit row (the base row is not selectable).</summary>
    public bool SelectLast() => SelectAt(_rows.Count - 1);

    /// <summary>
    /// Alt+Up (<paramref name="delta"/> -1) and Alt+Down (+1): moves the selected row one place,
    /// never above the top or below the bottom commit row. Returns whether it moved.
    /// </summary>
    public bool MoveSelectedRow(int delta)
    {
        if (!IsInteractive || SelectedRow is not { } selected)
        {
            return false;
        }

        var index = _rows.IndexOf(selected);
        return MoveRow(selected, Math.Clamp(index + delta, 0, _rows.Count - 1));
    }

    /// <summary>
    /// The drop of a dragged row into gap <paramref name="gap"/>: gap k is the line above row k,
    /// gap <see cref="Rows"/>.Count the line under the bottom commit row, right above the base
    /// row (a drop below the base row lands there, 2.2). The gaps right above and under the row
    /// leave it where it is. Returns whether it moved.
    /// </summary>
    public bool DropRow(InteractiveRebaseRowViewModel row, int gap)
    {
        ArgumentNullException.ThrowIfNull(row);
        var from = _rows.IndexOf(row);
        if (from < 0)
        {
            return false;
        }

        gap = Math.Clamp(gap, 0, _rows.Count);
        return MoveRow(row, gap > from ? gap - 1 : gap);
    }

    /// <summary>
    /// Moves <paramref name="row"/> to <paramref name="index"/> (counted from the top among the
    /// commit rows). Squash and fixup rows then join their new nearest target, and one with none
    /// below it turns into Pick (2.2). Returns whether it moved.
    /// </summary>
    public bool MoveRow(InteractiveRebaseRowViewModel row, int index)
    {
        ArgumentNullException.ThrowIfNull(row);
        var from = _rows.IndexOf(row);
        if (!IsInteractive || from < 0 || index < 0 || index >= _rows.Count || index == from)
        {
            return false;
        }

        _rows.RemoveAt(from);
        _rows.Insert(index, row);
        _items.Move(from, index);
        Refresh();
        return true;
    }

    /// <summary>
    /// Whether <paramref name="action"/> can apply to <paramref name="row"/> now, and why not: Squash
    /// and Fixup need a Pick or Reword below the row to join; the row's own action is no change.
    /// </summary>
    public (bool CanApply, string? Reason) CanApply(InteractiveRebaseRowViewModel row, RebaseAction action)
    {
        ArgumentNullException.ThrowIfNull(row);
        var index = _rows.IndexOf(row);
        if (index < 0 || row.Action == action)
        {
            return (false, null);
        }

        if (action is RebaseAction.Squash or RebaseAction.Fixup && TargetBelow(index) is null)
        {
            return (false, index == _rows.Count - 1 ? NothingBelowReason : AllBelowDroppedReason);
        }

        return (true, null);
    }

    /// <summary>
    /// The row's menu (2.2, 7.1): the header <c>{short} {subject}</c>, then Pick, Reword…, Squash,
    /// Fixup and Drop without the row's current action; Squash and Fixup disabled with their reason
    /// when nothing below can be joined. An item sets the action as <see cref="SetActionAsync"/> does.
    /// </summary>
    public MenuModel MenuFor(InteractiveRebaseRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var entries = new List<MenuEntry> { new MenuHeader(row.Name) };
        foreach (var action in MenuActions.Where(action => action != row.Action))
        {
            var (canApply, reason) = CanApply(row, action);
            entries.Add(new MenuItem(MenuLabel(action), () => _ = SetActionAsync(row, action), canApply, canApply ? null : reason));
        }

        return new MenuModel(entries);
    }

    /// <summary>
    /// Git's letters on the selected row (2.2): <c>p</c> Pick, <c>r</c> Reword (the dialog), <c>s</c>
    /// Squash, <c>f</c> Fixup, <c>d</c> Drop. A letter that cannot apply does nothing. Completes
    /// with whether the action changed.
    /// </summary>
    public Task<bool> ApplyLetterAsync(char letter)
    {
        RebaseAction? action = char.ToLowerInvariant(letter) switch
        {
            'p' => RebaseAction.Pick,
            'r' => RebaseAction.Reword,
            's' => RebaseAction.Squash,
            'f' => RebaseAction.Fixup,
            'd' => RebaseAction.Drop,
            _ => null,
        };
        return action is { } chosen && SelectedRow is { } row ? SetActionAsync(row, chosen) : Task.FromResult(false);
    }

    /// <summary>
    /// Sets <paramref name="row"/>'s action (2.2). Reword first asks for the new message (the
    /// message dialog titled <c>Reword {short}</c>, filled with the commit's message): Cancel there
    /// leaves the action as it was. Squash and Fixup apply only with a Pick or Reword below. Then
    /// every squash and fixup row joins its nearest target again. Completes with whether the
    /// action changed.
    /// </summary>
    public async Task<bool> SetActionAsync(InteractiveRebaseRowViewModel row, RebaseAction action)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!IsInteractive || _asking || !CanApply(row, action).CanApply)
        {
            return false;
        }

        string? newMessage = null;
        if (action == RebaseAction.Reword)
        {
            newMessage = await AskAsync($"Reword {row.ShortSha}", row.Entry.Message);
            if (newMessage is null || !IsInteractive || !_rows.Contains(row) || !CanApply(row, action).CanApply)
            {
                return false;
            }
        }

        row.Action = action;
        row.NewMessage = newMessage;
        Refresh();
        return true;
    }

    /// <summary>
    /// <c>Edit…</c> on a row's second line (2.2, 7.2): for the target of a group with a squash, the
    /// group's message (<c>Message of the squashed commit</c>); for a reworded row, its new message
    /// (<c>Reword {short}</c>). Cancel keeps the message. Completes with whether it changed.
    /// </summary>
    public async Task<bool> EditMessageAsync(InteractiveRebaseRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!IsInteractive || _asking || !_rows.Contains(row))
        {
            return false;
        }

        if (row.IsSquashTarget)
        {
            var message = await AskAsync(SquashedMessageTitle, GroupMessage(row));
            if (message is null || !row.IsSquashTarget)
            {
                return false;
            }

            row.EditedGroupMessage = message;
        }
        else if (row.Action == RebaseAction.Reword)
        {
            var message = await AskAsync($"Reword {row.ShortSha}", row.NewMessage ?? row.Entry.Message);
            if (message is null || row.Action != RebaseAction.Reword)
            {
                return false;
            }

            row.NewMessage = message;
        }
        else
        {
            return false;
        }

        Refresh();
        return true;
    }

    /// <summary>
    /// The message the group whose target is <paramref name="target"/> gives its commit (2.2): the
    /// edited one, or git's own, which is the target's message (its new one when reworded)
    /// followed by each squash member's message in history order, joined by a blank line; a group
    /// of fixups only keeps the target's message.
    /// </summary>
    public string GroupMessage(InteractiveRebaseRowViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.EditedGroupMessage is { } edited)
        {
            return edited;
        }

        var squashes = MembersOf(target).Where(member => member.Action == RebaseAction.Squash).Select(member => member.Entry.Message).ToList();
        return squashes.Count == 0 ? target.OwnOrNewMessage : RebaseTodo.SquashMessage([target.OwnOrNewMessage, .. squashes]);
    }

    /// <summary>
    /// The squash and fixup rows that join <paramref name="target"/>, in history order (the one
    /// nearest above it first).
    /// </summary>
    public IReadOnlyList<InteractiveRebaseRowViewModel> MembersOf(InteractiveRebaseRowViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var index = _rows.IndexOf(target);
        var members = new List<InteractiveRebaseRowViewModel>();
        for (var above = index - 1; above >= 0; above--)
        {
            if (ReferenceEquals(_rows[above].JoinTarget, target))
            {
                members.Add(_rows[above]);
            }
        }

        return members;
    }

    /// <summary>
    /// What Start hands the git layer (2.4, 2.5): the rows in git's order (oldest first) with their
    /// actions; a reword's new message on its row; on the target of a group with a squash the
    /// group's message when it was edited or the target was reworded (otherwise null, and the git
    /// layer writes git's own squash message from the commits' exact messages); a group of fixups
    /// only keeps its target's message (null for a Pick, its new one for a Reword). The rebase goes
    /// onto <see cref="RebaseList.Onto"/> and expects <see cref="RebaseList.BranchTip"/>.
    /// </summary>
    public RebasePlan BuildPlan()
    {
        var steps = new List<RebaseStep>(_rows.Count);
        for (var index = _rows.Count - 1; index >= 0; index--)
        {
            var row = _rows[index];
            var message = row.Action switch
            {
                RebaseAction.Pick when row.IsSquashTarget => row.EditedGroupMessage,
                RebaseAction.Reword when row.IsSquashTarget => GroupMessage(row),
                RebaseAction.Reword => row.NewMessage,
                _ => null,
            };
            steps.Add(new RebaseStep(row.Sha, row.Action, message));
        }

        return new RebasePlan(List.Onto, List.BranchTip, Request.BranchRef, steps);
    }

    /// <summary>
    /// Start rebase (2.4): hands <see cref="BuildPlan"/> to the repository, which runs it as one
    /// operation. The rows and buttons are disabled while it waits or runs; the list closes when
    /// the rebase is done or stopped, and stays as it was when git refused before anything started.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!CanStart)
        {
            return;
        }

        var plan = BuildPlan();
        IsBusy = true;
        var close = false;
        try
        {
            close = await _host.StartAsync(plan);
        }
        finally
        {
            IsBusy = false;
            if (close)
            {
                CloseList();
            }
        }
    }

    /// <summary>Cancel (<c>RebaseCancelButton</c>), and Esc: closes the list, asking first when it was changed (7.2, 14.3).</summary>
    [RelayCommand(CanExecute = nameof(IsInteractive))]
    private async Task CancelAsync()
    {
        if (await TryLeaveAsync())
        {
            CloseList();
        }
    }

    /// <summary>
    /// Whether the list may be left (2.2, 11.1): true at once when it was not changed; otherwise
    /// after the question <c>Discard the changes to this list?</c> (7.2), true on <c>Discard</c>,
    /// false on <c>Keep editing</c>. It closes nothing itself: the repository's
    /// <c>TryLeaveGraphAreaAsync</c> asks through it before something else takes the graph area.
    /// </summary>
    public async Task<bool> TryLeaveAsync()
    {
        if (IsClosed)
        {
            return true;
        }

        if (!IsChanged)
        {
            return true;
        }

        if (_asking)
        {
            return false;
        }

        _asking = true;
        try
        {
            return await _host.ConfirmAsync(new ConfirmationRequest(DiscardTitle, string.Empty, string.Empty, DiscardText, IsDestructive: true)
            {
                CancelText = KeepEditingText,
            });
        }
        finally
        {
            _asking = false;
        }
    }

    /// <summary>
    /// Follows the repository's refs (<see cref="RepositoryViewModel"/> calls it whenever it reads
    /// them): Start is disabled with <see cref="HeadMovedReason"/> once HEAD is not on the branch
    /// at the tip the list was read from, or, for a branch checked out first, once the branch has
    /// moved (2.2, 2.4). The list itself was read once and does not change.
    /// </summary>
    public void ObserveRefs(RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var tip = refs.Refs.FirstOrDefault(gitRef => gitRef.Kind == RefKind.LocalBranch && gitRef.FullName == Request.BranchRef)?.TargetSha;
        var headOnBranch = refs.Head.BranchName is { } head && "refs/heads/" + head == Request.BranchRef && refs.Head.Sha == List.BranchTip;
        IsHeadMoved = !string.Equals(tip, List.BranchTip, StringComparison.OrdinalIgnoreCase) || (!Request.CheckoutFirst && !headOnBranch);
    }

    /// <summary>The list's state as text, for a test's message: each row's short SHA and action, top to bottom.</summary>
    public override string ToString() => string.Join(", ", _rows.Select(row => $"{row.ShortSha} {row.ActionText}"));

    private bool SelectAt(int index)
    {
        if (!IsInteractive || index < 0 || index >= _rows.Count || ReferenceEquals(SelectedRow, _rows[index]))
        {
            return false;
        }

        Select(_rows[index]);
        return true;
    }

    private void CloseList()
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;
        _host.Close();
    }

    private async Task<string?> AskAsync(string title, string message)
    {
        _asking = true;
        try
        {
            return await _host.AskMessageAsync(title, message, SaveText);
        }
        finally
        {
            _asking = false;
        }
    }

    /// <summary>The nearest Pick or Reword row below row <paramref name="index"/>, or null.</summary>
    private InteractiveRebaseRowViewModel? TargetBelow(int index)
    {
        for (var below = index + 1; below < _rows.Count; below++)
        {
            if (_rows[below].Action is RebaseAction.Pick or RebaseAction.Reword)
            {
                return _rows[below];
            }
        }

        return null;
    }

    /// <summary>
    /// After every change: a squash or fixup row with no target below it turns into Pick (2.2);
    /// each joins its nearest target; the groups' messages, second lines and join lines follow;
    /// "changed" is compared with the first state.
    /// </summary>
    private void Refresh()
    {
        // From the bottom: a row that turns into Pick is a target for the rows above it.
        var targetBelow = false;
        for (var index = _rows.Count - 1; index >= 0; index--)
        {
            var row = _rows[index];
            if (row.IsJoined && !targetBelow)
            {
                row.Action = RebaseAction.Pick;
            }

            targetBelow |= row.Action is RebaseAction.Pick or RebaseAction.Reword;
        }

        var lines = new RebaseJoinLine[_rows.Count];
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            row.JoinTarget = row.IsJoined ? TargetBelow(index) : null;
            if (row.JoinTarget is not { } target)
            {
                continue;
            }

            // The line runs from the member's centre to the top edge of its target.
            var targetIndex = _rows.IndexOf(target);
            if (lines[index] == RebaseJoinLine.None)
            {
                lines[index] = RebaseJoinLine.FromCentre;
            }

            for (var between = index + 1; between < targetIndex; between++)
            {
                lines[between] = RebaseJoinLine.Through;
            }
        }

        foreach (var (row, index) in _rows.Select((row, index) => (row, index)))
        {
            row.JoinLine = lines[index];
            row.IsSquashTarget = row.Action is RebaseAction.Pick or RebaseAction.Reword
                && _rows.Any(member => ReferenceEquals(member.JoinTarget, row) && member.Action == RebaseAction.Squash);
            if (!row.IsSquashTarget)
            {
                row.EditedGroupMessage = null;
            }
        }

        foreach (var row in _rows)
        {
            row.SecondLine = row.IsSquashTarget ? "Message: " + InteractiveRebaseRowViewModel.SubjectOf(GroupMessage(row))
                : row.Action == RebaseAction.Reword ? "New message: " + InteractiveRebaseRowViewModel.SubjectOf(row.OwnOrNewMessage)
                : null;
        }

        IsChanged = !_rows.Select(row => row.Sha).SequenceEqual(_firstOrder, StringComparer.Ordinal)
            || _rows.Any(row => row.Action != RebaseAction.Pick);
    }
}
