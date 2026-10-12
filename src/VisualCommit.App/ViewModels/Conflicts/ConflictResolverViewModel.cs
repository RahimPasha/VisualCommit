using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Conflicts;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Conflicts;

/// <summary>What the resolver's body shows (docs/phase-4-design.md 11.2, 11.8).</summary>
public enum ConflictResolverBody
{
    /// <summary>Nothing is open, or the file is being read.</summary>
    None,

    /// <summary>A text conflict: the ours and theirs panes above the editable result.</summary>
    Text,

    /// <summary>A conflict without text: the card with what happened and buttons that resolve it at once.</summary>
    Card,
}

/// <summary>
/// Where the result's caret goes and what the view brings into view, after the resolver reached a
/// block, made a choice, or read a file (11.5).
/// </summary>
/// <param name="CaretOffset">The caret's offset in the result's document.</param>
/// <param name="RevealBlock">A block whose bar each top pane scrolls to as its third row, or null.</param>
/// <param name="RevealLine">A line (from 1) of the result that scrolls to its third row, or null: the caret is brought into view.</param>
/// <param name="FocusResult">The result takes the keyboard focus, without a focus outline.</param>
public sealed record ConflictNavigation(int CaretOffset, int? RevealBlock, int? RevealLine, bool FocusResult);

/// <summary>
/// The conflict resolver (docs/phase-4-design.md section 11, D140, D148, D149): one conflicted file
/// at a time from the repository's conflicts, the ours and theirs panes as the file was read, the
/// editable result and its undo stack, the choices per block and the bars' states, a whole side,
/// Mark resolved with its questions, the card of a conflict without text, the merge tool, leaving
/// and the refreshes while it is open. It writes nothing itself: every write goes through
/// <see cref="IConflictActions"/>, the repository's queued operations.
/// </summary>
/// <remarks>
/// <para>
/// The result is a <see cref="TextDocument"/> the view model owns, with its undo stack, so that a
/// tab switch keeps them (one view serves every tab). A choice and a whole side are one undo step
/// each, which also records what the bars say (<see cref="ConflictBlockChoice"/> on the stack);
/// undo and redo are the view model's (<see cref="Undo"/>, <see cref="Redo"/>). Choices, undo and
/// redo update the bars, tints and counts at once; anything typed is parsed again
/// <see cref="ParseDelay"/> after the last change (<see cref="IsParsePending"/>, 14.3).
/// </para>
/// <para>
/// The repository feeds it from every status read (<see cref="Update"/>) and with git's
/// <c>merge.tool</c> (<see cref="MergeTool"/>); it opens a file with <see cref="OpenAsync"/>, asks
/// before replacing it with <see cref="TryLeaveAsync"/>, and hides it on <see cref="Closed"/>.
/// </para>
/// </remarks>
public sealed partial class ConflictResolverViewModel : ObservableObject, IDisposable
{
    /// <summary>How long after the last typed change the result is parsed again (11.5).</summary>
    public static readonly TimeSpan ParseDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>How long after opening a press with a click count of 2 or more is ignored (11.1).</summary>
    public static readonly TimeSpan DoubleClickGuard = TimeSpan.FromMilliseconds(500);

    private readonly IConflictActions _actions;
    private readonly IAppLog _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeProvider _time;

    /// <summary>The whole sides Take ours and Take theirs took for the open file: their text and their bytes as read (11.6).</summary>
    private readonly List<(string Text, byte[] Bytes)> _takenSides = [];

    private IReadOnlyList<ConflictEntry> _conflicts = [];
    private InProgressState _inProgress = InProgressState.None;
    private IReadOnlySet<string> _statusPaths = new HashSet<string>(StringComparer.Ordinal);
    private string _currentName = "HEAD";
    private string? _ontoName;

    private ConflictText? _read;
    private byte[] _readBytes = [];
    private bool _isUtf8 = true;
    private int _markerSize = ConflictText.DefaultMarkerSize;
    private string _original = string.Empty;
    private BlockChoice?[] _choices = [];
    private int[] _matched = [];
    private ConflictText? _current;
    private CancellationTokenSource? _parsing;
    private bool _changing;
    private int _version;
    private bool _disposed;

    /// <param name="actions">The reads and writes of a conflicted file: the repository's operations.</param>
    /// <param name="log">Where failures are logged.</param>
    /// <param name="delay">Waits before a typed change is parsed (<see cref="Task.Delay(TimeSpan, CancellationToken)"/> unless a test holds it).</param>
    /// <param name="time">The clock of the double-click guard (the system's unless a test holds it).</param>
    public ConflictResolverViewModel(IConflictActions actions, IAppLog? log = null, Func<TimeSpan, CancellationToken, Task>? delay = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions;
        _log = log ?? NullAppLog.Instance;
        _delay = delay ?? Task.Delay;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The resolver closed: every file is resolved, the operation ended, or the user left it. The repository hides it.</summary>
    public event EventHandler? Closed;

    /// <summary>The view moves the result's caret and scrolls (11.5).</summary>
    public event EventHandler<ConflictNavigation>? NavigationRequested;

    /// <summary>The open file's conflict, or null while nothing is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(Path), nameof(FileName), nameof(Folder), nameof(HasFolder), nameof(KindText))]
    public partial ConflictEntry? Entry { get; private set; }

    /// <summary>A file is open.</summary>
    public bool IsOpen => Entry is not null;

    /// <summary>The open file's path, repository-relative with <c>/</c>, or empty.</summary>
    public string Path => Entry?.Path ?? string.Empty;

    /// <summary>The open file's name (<c>ResolverFileName</c>); also <c>{file}</c> in the questions (14.3).</summary>
    public string FileName => WorkingChangesViewModel.FileName(Path);

    /// <summary>The open file's folder (<c>ResolverFolder</c>), empty for a file at the root (14.3).</summary>
    public string Folder => Path.LastIndexOf('/') is var slash and > 0 ? Path[..slash] : string.Empty;

    public bool HasFolder => Folder.Length > 0;

    /// <summary>The kind in git's words (<c>ResolverKind</c>, 11.2).</summary>
    public string KindText => Entry is { } entry ? ConflictResolverTexts.KindText(entry.Kind) : string.Empty;

    /// <summary>The status letter of the header (<c>ResolverFileStatus</c>).</summary>
    public string StatusLetter => "U";

    /// <summary><c>File {i} of {n}</c> (<c>ResolverFilePosition</c>).</summary>
    [ObservableProperty]
    public partial string FilePositionText { get; private set; } = string.Empty;

    /// <summary>Another conflicted file can be reached from the header (previous and next, wrapping).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousFileCommand), nameof(NextFileCommand))]
    public partial bool HasOtherFiles { get; private set; }

    /// <summary>What the body shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsText), nameof(ShowsCard), nameof(ShowsConflictTools), nameof(ShowsMergeTool))]
    [NotifyCanExecuteChangedFor(nameof(TakeOursCommand), nameof(TakeTheirsCommand), nameof(MarkResolvedCommand), nameof(RunMergeToolCommand), nameof(PreviousConflictCommand), nameof(NextConflictCommand))]
    public partial ConflictResolverBody Body { get; private set; }

    public bool ShowsText => Body == ConflictResolverBody.Text;

    public bool ShowsCard => Body == ConflictResolverBody.Card;

    /// <summary>The conflict navigation, Take ours, Take theirs and Mark resolved show for a text conflict only (11.8).</summary>
    public bool ShowsConflictTools => Body == ConflictResolverBody.Text;

    /// <summary><c>Merge tool…</c> shows for a text conflict and a binary file (git runs no tool for the others, 11.9).</summary>
    public bool ShowsMergeTool => Body == ConflictResolverBody.Text || CardKind == ConflictCardKind.Binary;

    /// <summary>The result: the file itself, editable; null without a text conflict.</summary>
    [ObservableProperty]
    public partial TextDocument? Document { get; private set; }

    /// <summary>The marker size of the open file (git's <c>conflict-marker-size</c>).</summary>
    public int MarkerSize => _markerSize;

    /// <summary>The panes' pieces: the file as it was read, every block with its ours and theirs lines (11.3).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ConflictPanesPart> Parts { get; private set; } = [];

    /// <summary>What each block's bars show and how its sides are tinted (11.5, 11.6, 14.3).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ConflictBlockBars> Bars { get; private set; } = [];

    /// <summary>The ours pane's title (<c>ConflictOursTitle</c>, 11.4).</summary>
    [ObservableProperty]
    public partial string OursTitle { get; private set; } = string.Empty;

    /// <summary>The theirs pane's title (<c>ConflictTheirsTitle</c>, 11.4).</summary>
    [ObservableProperty]
    public partial string TheirsTitle { get; private set; } = string.Empty;

    /// <summary>The result's title (<c>ConflictResultTitle</c>): <c>Result</c>, or what the resolver waits for.</summary>
    [ObservableProperty]
    public partial string ResultTitle { get; private set; } = ConflictResolverTexts.ResultTitle;

    /// <summary>The result's count (<c>ConflictResultCount</c>), "the truth" (11.5).</summary>
    [ObservableProperty]
    public partial string ResultCountText { get; private set; } = string.Empty;

    /// <summary>How many whole blocks the result has, as last parsed.</summary>
    [ObservableProperty]
    public partial int ConflictsLeft { get; private set; }

    /// <summary><c>Conflict {k} of {m}</c> or <c>No conflicts left</c> (<c>ResolverConflictPosition</c>, 14.3).</summary>
    [ObservableProperty]
    public partial string ConflictPositionText { get; private set; } = string.Empty;

    /// <summary>The block last reached (on opening, by the conflict arrows), from 0, or null.</summary>
    [ObservableProperty]
    public partial int? CurrentBlock { get; private set; }

    /// <summary>A typed change waits to be parsed (14.3): the checks wait for it to be false.</summary>
    [ObservableProperty]
    public partial bool IsParsePending { get; private set; }

    /// <summary>The result differs from the file as the resolver last read or wrote it (11.1).</summary>
    [ObservableProperty]
    public partial bool IsModified { get; private set; }

    /// <summary>A write of the resolver runs (Mark resolved, a card's button, the merge tool): the result is read-only and the buttons disabled (11.7).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(CanUndo), nameof(CanRedo))]
    [NotifyCanExecuteChangedFor(nameof(TakeOursCommand), nameof(TakeTheirsCommand), nameof(MarkResolvedCommand), nameof(RunMergeToolCommand), nameof(PreviousFileCommand), nameof(NextFileCommand), nameof(PreviousConflictCommand), nameof(NextConflictCommand), nameof(CloseResolverCommand))]
    public partial bool IsBusy { get; private set; }

    public bool IsIdle => !IsBusy;

    /// <summary>The open file is no longer conflicted, but the result is modified (11.11).</summary>
    [ObservableProperty]
    public partial bool IsResolvedElsewhere { get; private set; }

    /// <summary>
    /// The merge tool git's configuration names (<c>merge.tool</c>, <c>RepositoryMenuContext.MergeTool</c>),
    /// which the repository sets after each status read; null or empty: none (11.9).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MergeToolReason))]
    [NotifyCanExecuteChangedFor(nameof(RunMergeToolCommand))]
    public partial string? MergeTool { get; set; }

    /// <summary>Why <c>Merge tool…</c> is disabled, as its tooltip; null while it can be used.</summary>
    public string? MergeToolReason => string.IsNullOrEmpty(MergeTool) ? ConflictResolverTexts.NoMergeToolReason : null;

    /// <summary>Why <c>Take ours</c> is disabled (a side the conflict lacks), or null.</summary>
    public string? TakeOursReason => Entry is { HasOurs: false } ? ConflictResolverTexts.SideDeletedReason : null;

    /// <summary>Why <c>Take theirs</c> is disabled, or null.</summary>
    public string? TakeTheirsReason => Entry is { HasTheirs: false } ? ConflictResolverTexts.SideDeletedReason : null;

    /// <summary>What the open conflict without text is, or null for a text conflict.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMergeTool))]
    public partial ConflictCardKind? CardKind { get; private set; }

    /// <summary>The card's text (<c>ConflictCardText</c>, 11.8).</summary>
    [ObservableProperty]
    public partial string CardText { get; private set; } = string.Empty;

    /// <summary>The card's buttons, in order.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ConflictCardButton> CardButtons { get; private set; } = [];

    /// <summary>When the resolver was last opened on a file from outside (the clock's <see cref="TimeProvider.GetTimestamp"/>), for the double-click guard (11.1).</summary>
    public long OpenedAt { get; private set; }

    /// <summary>Whether a press with a click count of 2 or more is still ignored: within <see cref="DoubleClickGuard"/> of opening (11.1).</summary>
    public bool IsInDoubleClickGuard => OpenedAt != 0 && _time.GetElapsedTime(OpenedAt) < DoubleClickGuard;

    /// <summary>The result's caret, kept while another tab's resolver is shown.</summary>
    public int CaretOffset { get; set; }

    /// <summary>The body is to take the keyboard focus (the result on opening and after a choice, or a card's first button) and the view has not given it yet: a view shown later gives it.</summary>
    public bool ResultFocusPending { get; set; }

    /// <summary>The result's scroll offset, kept while another tab's resolver is shown.</summary>
    public (double X, double Y)? ResultScroll { get; set; }

    /// <summary>The panes' scroll offset, kept while another tab's resolver is shown.</summary>
    public (double X, double Y)? PanesScroll { get; set; }

    /// <summary>
    /// The conflicted file's menu for a right-click on the header (11.12): the repository's
    /// <c>RepositoryMenus.ForUnstagedFile</c> for the file, which the repository sets; null opens nothing.
    /// </summary>
    public Func<ChangedFile, MenuModel?>? FileMenu { get; set; }

    /// <summary>The result can be undone (the result's menu, 14.3).</summary>
    public bool CanUndo => Document?.UndoStack.CanUndo == true && !IsBusy;

    /// <summary>The result can be redone.</summary>
    public bool CanRedo => Document?.UndoStack.CanRedo == true && !IsBusy;

    /// <summary>The conflicted files in the stage list's order, as last read.</summary>
    public IReadOnlyList<ConflictEntry> Conflicts => _conflicts;

    /// <summary>The file as it was read, parsed: the panes' blocks.</summary>
    public ConflictText? Read => _read;

    /// <summary>
    /// What a status read says (11.11): the conflicted files, what is in progress, <c>{cur}</c>
    /// and a rebase's new base by name (null: its short SHA). A file whose result is not modified
    /// is read again; one that is no longer conflicted is left (11.7) unless its result is
    /// modified; the end of the operation closes the resolver.
    /// </summary>
    public void Update(WorkingTreeStatus status, string currentName, string? ontoName)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(currentName);
        var wasInProgress = _inProgress.IsActive;
        var oldOrder = _conflicts.Select(conflict => conflict.Path).ToList();
        _conflicts = status.Conflicts;
        _inProgress = status.InProgress;
        _statusPaths = new HashSet<string>(status.Staged.Select(file => file.Path).Concat(status.Unstaged.Select(file => file.Path)), StringComparer.Ordinal);
        _currentName = currentName;
        _ontoName = ontoName;

        // A write of the resolver itself is running: it moves on when it ends, from what is read now.
        if (Entry is not { } entry || IsBusy)
        {
            return;
        }

        if (wasInProgress && !status.InProgress.IsActive)
        {
            Close();
            return;
        }

        UpdateFilePosition();
        UpdateTitles();
        if (status.FindConflict(entry.Path) is { } now)
        {
            IsResolvedElsewhere = false;
            UpdateResultTitle();
            if (!IsModified)
            {
                _ = ReadAgainAsync(now);
            }
            else if (now != entry)
            {
                Entry = now;
            }

            return;
        }

        if (IsModified)
        {
            IsResolvedElsewhere = true;
            UpdateResultTitle();
            return;
        }

        _ = MoveOnAsync(oldOrder, entry.Path);
    }

    /// <summary>
    /// Opens the resolver on the conflicted file <paramref name="path"/> (11.1): reads it and
    /// reaches its first block, with the keyboard focus in the result. The repository calls it after
    /// <see cref="Update"/> and after leaving what the graph area showed.
    /// </summary>
    public Task OpenAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        OpenedAt = _time.GetTimestamp();
        return OpenFileAsync(path);
    }

    /// <summary>
    /// Whether the resolver may be replaced in the graph area (11.1): true when the result is not
    /// modified, or when the user discards it; false on "Keep editing". Closes nothing itself.
    /// </summary>
    public async Task<bool> TryLeaveAsync()
    {
        if (Entry is null || Body != ConflictResolverBody.Text || IsBusy)
        {
            return true;
        }

        FlushParse();
        return !IsModified || await _actions.ConfirmAsync(ConflictResolverTexts.LeaveQuestion(FileName));
    }

    /// <summary>Closes the resolver without asking (the operation ended, the tab closes, the repository shows something else).</summary>
    public void Close()
    {
        if (Entry is null && Body == ConflictResolverBody.None)
        {
            return;
        }

        _version++;
        CancelParse();
        ShowNothing();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A block bar's button (11.5): replaces block <paramref name="block"/>'s text in the result,
    /// from its <c>&lt;</c> line to its <c>&gt;</c> line, with the chosen lines, as one undo step; the
    /// caret goes to the start of the replaced text, with the focus in the result. Nothing happens
    /// when the block's markers are no longer in the result.
    /// </summary>
    public void Choose(int block, ConflictChoice choice)
    {
        if (Document is not { } document || IsBusy || _read is null)
        {
            return;
        }

        FlushParse();
        if (block < 0 || block >= _matched.Length || _matched[block] < 0 || _current is not { } current)
        {
            return;
        }

        var target = current.Blocks[_matched[block]];
        var blockChoice = choice switch
        {
            ConflictChoice.UseOurs => BlockChoice.Ours,
            ConflictChoice.UseTheirs => BlockChoice.Theirs,
            ConflictChoice.UseBothOursFirst => BlockChoice.OursThenTheirs,
            _ => BlockChoice.TheirsThenOurs,
        };

        var choices = (BlockChoice?[])_choices.Clone();
        choices[block] = blockChoice;
        Change(document, target.Offset, target.Length, target.TextFor(blockChoice), choices);
        Navigate(new ConflictNavigation(target.Offset, null, null, FocusResult: true));
    }

    /// <summary>Undoes the result's last change (the platform's undo, the result's menu), updating the bars and counts at once.</summary>
    public bool Undo() => Step(undo: true);

    /// <summary>Redoes the result's last undone change.</summary>
    public bool Redo() => Step(undo: false);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _version++;
        CancelParse();
        DetachDocument();
    }

    [RelayCommand(CanExecute = nameof(CanGoToOtherFile))]
    private Task PreviousFile() => GoToFileAsync(-1);

    [RelayCommand(CanExecute = nameof(CanGoToOtherFile))]
    private Task NextFile() => GoToFileAsync(1);

    /// <summary>The close button and Esc outside the result (11.10): asks first when the result is modified. Nothing while a write of the resolver or the merge tool runs (11.7, 14.4), however it is called.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task CloseResolver()
    {
        if (IsBusy)
        {
            return;
        }

        if (await TryLeaveAsync() && !IsBusy)
        {
            Close();
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigateConflicts))]
    private void PreviousConflict() => GoToConflict(-1);

    [RelayCommand(CanExecute = nameof(CanNavigateConflicts))]
    private void NextConflict() => GoToConflict(1);

    /// <summary>Take ours (11.6): the whole result becomes ours' file, as one undo step.</summary>
    [RelayCommand(CanExecute = nameof(CanTakeOurs))]
    private Task TakeOurs() => TakeSideAsync(ConflictSide.Ours);

    /// <summary>Take theirs (11.6).</summary>
    [RelayCommand(CanExecute = nameof(CanTakeTheirs))]
    private Task TakeTheirs() => TakeSideAsync(ConflictSide.Theirs);

    /// <summary>
    /// Mark resolved (11.7): the result's text at the click; asks when blocks are left, and when the
    /// file on disk changed since it was read; then writes it and stages it in one operation, and
    /// moves on to the next conflicted file or closes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMarkResolved))]
    private async Task MarkResolved()
    {
        if (Entry is not { } entry || Document is not { } document || IsBusy)
        {
            return;
        }

        var text = document.Text;
        var version = _version;
        if (ConflictText.HasMarkerLines(text, _markerSize) && !await _actions.ConfirmAsync(ConflictResolverTexts.MarkersQuestion(FileName)))
        {
            return;
        }

        if (version != _version || Entry is null)
        {
            return;
        }

        if (!await IsAsReadAsync(entry) && !await _actions.ConfirmAsync(ConflictResolverTexts.ChangedOnDiskQuestion(FileName)))
        {
            return;
        }

        if (version != _version || Entry is null)
        {
            return;
        }

        var content = BytesToWrite(text);
        await ResolveAsync(entry, () => _actions.MarkResolvedAsync(entry.Path, content), resolved =>
        {
            _original = text;
            _readBytes = content;
            UpdateModified();
        });
    }

    /// <summary>
    /// <c>Merge tool…</c> (11.9): asks first when the result is modified; runs the tool as one
    /// operation that waits for it, with the result read-only; then moves on when git staged the
    /// file, or reads the file again.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunMergeTool))]
    private async Task RunMergeTool()
    {
        if (Entry is not { } entry || MergeTool is not { Length: > 0 } tool || IsBusy)
        {
            return;
        }

        FlushParse();
        if (IsModified && !await _actions.ConfirmAsync(ConflictResolverTexts.MergeToolQuestion(FileName)))
        {
            return;
        }

        if (Entry is null || IsBusy)
        {
            return;
        }

        var order = _conflicts.Select(conflict => conflict.Path).ToList();
        var version = ++_version;
        IsBusy = true;
        ResultTitle = ConflictResolverTexts.WaitingForTool(tool);
        bool staged;
        try
        {
            staged = await _actions.RunMergeToolAsync(entry.Path);
        }
        catch (Exception ex)
        {
            _log.Error($"The merge tool for {entry.Path} failed.", ex);
            staged = false;
        }
        finally
        {
            IsBusy = false;
        }

        if (version != _version || _disposed)
        {
            return;
        }

        UpdateResultTitle();
        if (staged)
        {
            await MoveOnAsync(order, entry.Path);
        }
        else
        {
            // The tool may have written the file: the result is read again from it.
            await OpenFileAsync(entry.Path);
        }
    }

    private bool CanGoToOtherFile() => HasOtherFiles && !IsBusy;

    private bool CanNavigateConflicts() => Body == ConflictResolverBody.Text && !IsBusy;

    private bool CanTakeOurs() => Body == ConflictResolverBody.Text && !IsBusy && Entry is { HasOurs: true };

    private bool CanTakeTheirs() => Body == ConflictResolverBody.Text && !IsBusy && Entry is { HasTheirs: true };

    private bool CanMarkResolved() => Body == ConflictResolverBody.Text && !IsBusy;

    private bool CanRunMergeTool() => ShowsMergeTool && !IsBusy && !string.IsNullOrEmpty(MergeTool);

    /// <summary>Reads <paramref name="path"/> and shows it: its panes and result, or its card.</summary>
    private async Task OpenFileAsync(string path)
    {
        var version = ++_version;
        CancelParse();
        if (_conflicts.FirstOrDefault(conflict => conflict.Path == path) is not { } entry)
        {
            _log.Warning($"The resolver was asked to open {path}, which is not conflicted.");
            if (Entry is null)
            {
                Close();
            }

            return;
        }

        ConflictContent content;
        try
        {
            content = await _actions.ReadAsync(entry);
        }
        catch (Exception ex)
        {
            _log.Error($"Reading the conflict of {path} failed.", ex);
            return;
        }

        if (version != _version || _disposed)
        {
            return;
        }

        Show(entry, content);
    }

    /// <summary>Reads the open file again after a refresh, and shows it only when git or another program changed it (11.11).</summary>
    private async Task ReadAgainAsync(ConflictEntry entry)
    {
        // Not a new version: a choice or a whole side being read goes on; any of them makes the result modified, which drops this read.
        var version = _version;
        ConflictContent content;
        try
        {
            content = await _actions.ReadAsync(entry);
        }
        catch (Exception ex)
        {
            _log.Error($"Reading the conflict of {entry.Path} again failed.", ex);
            return;
        }

        if (version != _version || _disposed || Entry?.Path != entry.Path || IsModified || IsBusy)
        {
            return;
        }

        if (content.WorkingTree.AsSpan().SequenceEqual(_readBytes) && content.MarkerSize == _markerSize && KindOf(entry, content) == KindOf(Entry, null))
        {
            Entry = entry;
            return;
        }

        Show(entry, content, focus: false);
    }

    /// <summary>Shows a file as read: a text conflict's panes and result with its first block reached, or a card.</summary>
    private void Show(ConflictEntry entry, ConflictContent content, bool focus = true)
    {
        DetachDocument();
        Entry = entry;
        IsResolvedElsewhere = false;
        _takenSides.Clear();
        _readBytes = content.WorkingTree;
        _markerSize = content.MarkerSize > 0 ? content.MarkerSize : ConflictText.DefaultMarkerSize;
        UpdateFilePosition();

        if (KindOf(entry, content) is { } card)
        {
            ShowCard(entry, card, focus);
            return;
        }

        var decoded = ConflictText.Decode(content.WorkingTree);
        _isUtf8 = decoded.IsUtf8;
        _original = decoded.Text;
        _read = ConflictText.Parse(decoded.Text, _markerSize);
        _choices = new BlockChoice?[_read.BlockCount];
        _matched = [];
        _current = null;
        CardKind = null;
        CardText = string.Empty;
        CardButtons = [];
        Parts = ConflictPanes.PartsOf(_read);
        UpdateTitles();

        var document = new TextDocument(decoded.Text);
        document.TextChanged += OnDocumentTextChanged;
        Document = document;
        CaretOffset = 0;
        ResultScroll = null;
        PanesScroll = null;
        CurrentBlock = null;
        IsParsePending = false;
        Body = ConflictResolverBody.Text;
        Reparse();
        UpdateResultTitle();
        NotifyToolbar();

        if (FirstUnresolved() is { } first)
        {
            Reach(first, focus);
        }
        else
        {
            Navigate(new ConflictNavigation(0, null, 1, focus));
        }
    }

    /// <summary>The card of a conflict without text (11.8) and its buttons.</summary>
    private void ShowCard(ConflictEntry entry, ConflictCardKind kind, bool focus)
    {
        _read = null;
        _current = null;
        _choices = [];
        _matched = [];
        _original = string.Empty;
        Document = null;
        Parts = [];
        Bars = [];
        CurrentBlock = null;
        IsParsePending = false;
        UpdateTitles();
        CardKind = kind;

        var (oursName, theirsName) = SideNames(theirsLabel: null);
        var file = FileName;
        var path = entry.Path;
        string? otherName = null;
        string? folder = null;
        if (kind == ConflictCardKind.MovedAside)
        {
            // The file is the side's that has it (added by us, or deleted by them: ours); the other side has the folder.
            otherName = entry.Kind is ConflictKind.AddedByUs or ConflictKind.DeletedByThem ? theirsName : oursName;
            var moved = MovedAsideFolder(path) ?? path;
            folder = WorkingChangesViewModel.FileName(moved);
        }

        CardText = ConflictResolverTexts.CardText(kind, file, oursName, theirsName, otherName, folder);
        var keepSide = entry.Kind is ConflictKind.DeletedByThem or ConflictKind.AddedByUs ? ConflictSide.Ours : ConflictSide.Theirs;
        ResultFocusPending = focus && kind != ConflictCardKind.Submodule;
        CardButtons = kind switch
        {
            ConflictCardKind.DeletedByThem or ConflictCardKind.DeletedByUs =>
            [
                Button("Keep the file", "ConflictKeepFile", () => _actions.TakeSideAsync(path, keepSide)),
                Button("Delete it", "ConflictDeleteFile", () => _actions.RemoveAsync(path)),
            ],
            ConflictCardKind.AddedByUs or ConflictCardKind.AddedByThem =>
            [
                Button("Keep it", "ConflictKeepFile", () => _actions.TakeSideAsync(path, keepSide)),
                Button("Delete it", "ConflictDeleteFile", () => _actions.RemoveAsync(path)),
            ],
            ConflictCardKind.MovedAside => [Button("Mark resolved", "ConflictCardMarkResolved", () => _actions.TakeSideAsync(path, keepSide))],
            ConflictCardKind.BothDeleted => [Button("Delete it", "ConflictDeleteFile", () => _actions.RemoveAsync(path))],
            ConflictCardKind.Binary or ConflictCardKind.SymbolicLink =>
            [
                Button("Use ours", "ConflictUseOursFile", () => _actions.TakeSideAsync(path, ConflictSide.Ours)),
                Button("Use theirs", "ConflictUseTheirsFile", () => _actions.TakeSideAsync(path, ConflictSide.Theirs)),
            ],
            _ => [],
        };

        Body = ConflictResolverBody.Card;
        ResultCountText = string.Empty;
        ConflictsLeft = 0;
        ConflictPositionText = string.Empty;
        UpdateResultTitle();
        NotifyToolbar();

        ConflictCardButton Button(string label, string id, Func<Task<bool>> resolve) =>
            new(label, id, () => ResolveAsync(entry, resolve, null));
    }

    /// <summary>Clears the resolver: nothing open.</summary>
    private void ShowNothing()
    {
        DetachDocument();
        Entry = null;
        ResultFocusPending = false;
        _takenSides.Clear();
        _read = null;
        _current = null;
        _choices = [];
        _matched = [];
        _original = string.Empty;
        _readBytes = [];
        Parts = [];
        Bars = [];
        CardKind = null;
        CardText = string.Empty;
        CardButtons = [];
        CurrentBlock = null;
        IsParsePending = false;
        IsModified = false;
        IsResolvedElsewhere = false;
        ConflictsLeft = 0;
        ResultCountText = string.Empty;
        ConflictPositionText = string.Empty;
        FilePositionText = string.Empty;
        HasOtherFiles = false;
        Body = ConflictResolverBody.None;
        UpdateResultTitle();
        NotifyToolbar();
    }

    /// <summary>
    /// Runs one write that resolves the open file (Mark resolved, a card's button), with the result
    /// read-only and the buttons disabled while it runs; then moves on to the next conflicted file,
    /// or closes when none is left (11.7).
    /// </summary>
    private async Task ResolveAsync(ConflictEntry entry, Func<Task<bool>> resolve, Action<bool>? written)
    {
        if (IsBusy || Entry?.Path != entry.Path)
        {
            return;
        }

        var order = _conflicts.Select(conflict => conflict.Path).ToList();
        var version = ++_version;
        CancelParse();
        IsBusy = true;
        bool resolved;
        try
        {
            resolved = await resolve();
        }
        catch (Exception ex)
        {
            _log.Error($"Resolving {entry.Path} failed.", ex);
            resolved = false;
        }
        finally
        {
            IsBusy = false;
        }

        if (version != _version || _disposed)
        {
            return;
        }

        if (!resolved)
        {
            return;
        }

        written?.Invoke(resolved);
        await MoveOnAsync(order, entry.Path);
    }

    /// <summary>
    /// Moves from the resolved file <paramref name="resolved"/> to the next conflicted file in
    /// <paramref name="order"/> (the stage list's order when it was resolved), wrapping; closes when
    /// no conflicted file is left (11.7).
    /// </summary>
    private async Task MoveOnAsync(IReadOnlyList<string> order, string resolved)
    {
        var left = _conflicts.Where(conflict => conflict.Path != resolved).Select(conflict => conflict.Path).ToHashSet(StringComparer.Ordinal);
        if (left.Count == 0)
        {
            Close();
            return;
        }

        var at = order.ToList().IndexOf(resolved);
        string? next = null;
        for (var step = 1; at >= 0 && step <= order.Count && next is null; step++)
        {
            var candidate = order[(at + step) % order.Count];
            if (left.Contains(candidate))
            {
                next = candidate;
            }
        }

        next ??= _conflicts.First(conflict => left.Contains(conflict.Path)).Path;
        _conflicts = [.. _conflicts.Where(conflict => conflict.Path != resolved)];
        await OpenFileAsync(next);
    }

    /// <summary>Previous or next file from the header (wrapping), after asking when the result is modified.</summary>
    private async Task GoToFileAsync(int step)
    {
        if (Entry is not { } entry || IsBusy || _conflicts.Count == 0)
        {
            return;
        }

        if (!await TryLeaveAsync() || Entry?.Path != entry.Path)
        {
            return;
        }

        var paths = _conflicts.Select(conflict => conflict.Path).ToList();
        var at = paths.IndexOf(entry.Path);
        var next = at < 0
            ? (step > 0 ? 0 : paths.Count - 1)
            : (at + step + paths.Count) % paths.Count;
        await OpenFileAsync(paths[next]);
    }

    /// <summary>Reaches the previous or next block still in the result, wrapping (11.5).</summary>
    private void GoToConflict(int step)
    {
        if (Body != ConflictResolverBody.Text || IsBusy)
        {
            return;
        }

        FlushParse();
        var open = Enumerable.Range(0, _matched.Length).Where(block => _matched[block] >= 0).ToList();
        if (open.Count == 0)
        {
            return;
        }

        var from = CurrentBlock ?? (step > 0 ? -1 : _matched.Length);
        var target = step > 0
            ? open.FirstOrDefault(block => block > from, open[0])
            : open.LastOrDefault(block => block < from, open[^1]);
        Reach(target, focus: false);
    }

    /// <summary>
    /// Reaches block <paramref name="block"/> (11.5): the caret to the start of its <c>&lt;</c> line,
    /// its bar the third row of each top pane, its <c>&lt;</c> line the third row of the result.
    /// </summary>
    private void Reach(int block, bool focus)
    {
        if (Document is not { } document || _current is not { } current || block < 0 || block >= _matched.Length || _matched[block] < 0)
        {
            return;
        }

        CurrentBlock = block;
        UpdatePosition();
        var offset = current.Blocks[_matched[block]].Offset;
        var line = document.GetLineByOffset(offset).LineNumber;
        CaretOffset = offset;
        Navigate(new ConflictNavigation(offset, block, line, focus));
    }

    /// <summary>Take ours or Take theirs (11.6): reads that side's file in its working-tree form and makes it the whole result, every block as if that side had been chosen.</summary>
    private async Task TakeSideAsync(ConflictSide side)
    {
        if (Entry is not { } entry || Document is null || IsBusy)
        {
            return;
        }

        var version = _version;
        byte[]? bytes;
        try
        {
            bytes = await _actions.ReadSideAsync(entry, side);
        }
        catch (Exception ex)
        {
            _log.Error($"Reading {side.ToString().ToLowerInvariant()} of {entry.Path} failed.", ex);
            return;
        }

        if (bytes is null || version != _version || Document is not { } document || IsBusy)
        {
            return;
        }

        FlushParse();
        var text = ConflictText.Decode(bytes).Text;

        // Mark resolved writes the side's own bytes while the result is exactly this text (11.6).
        _takenSides.Add((text, bytes));
        var choices = Enumerable.Repeat<BlockChoice?>(side == ConflictSide.Ours ? BlockChoice.Ours : BlockChoice.Theirs, _choices.Length).ToArray();
        Change(document, 0, document.TextLength, text, choices);
        Navigate(new ConflictNavigation(0, null, 1, FocusResult: true));
    }

    /// <summary>
    /// The bytes Mark resolved writes for the result <paramref name="text"/> (11.6, 11.7): a whole
    /// side's bytes as they were read, byte for byte, when the result is exactly that side's text
    /// as Take ours or Take theirs took it (the side may be in another encoding than the file with
    /// markers); otherwise the text in the encoding the file was read in.
    /// </summary>
    private byte[] BytesToWrite(string text)
    {
        for (var i = _takenSides.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_takenSides[i].Text, text, StringComparison.Ordinal))
            {
                return _takenSides[i].Bytes;
            }
        }

        return ConflictText.Encode(text, _isUtf8);
    }

    /// <summary>Tells the view where the caret goes; a navigation that gives the result the focus is kept until the view has given it (a view shown later still does).</summary>
    private void Navigate(ConflictNavigation navigation)
    {
        CaretOffset = navigation.CaretOffset;
        if (navigation.FocusResult)
        {
            ResultFocusPending = true;
        }

        NavigationRequested?.Invoke(this, navigation);
    }

    /// <summary>One change of the resolver's own as one undo step: the text replaced and the choices it records.</summary>
    private void Change(TextDocument document, int offset, int length, string text, BlockChoice?[] choices)
    {
        CancelParse();
        _changing = true;
        try
        {
            using (document.RunUpdate())
            {
                document.Replace(offset, length, text);
                document.UndoStack.Push(new ChoicesStep(this, _choices, choices));
                _choices = choices;
            }
        }
        finally
        {
            _changing = false;
        }

        CaretOffset = offset;
        Reparse();
    }

    private bool Step(bool undo)
    {
        if (Document is not { } document || IsBusy || !(undo ? document.UndoStack.CanUndo : document.UndoStack.CanRedo))
        {
            return false;
        }

        CancelParse();
        _changing = true;
        try
        {
            if (undo)
            {
                document.UndoStack.Undo();
            }
            else
            {
                document.UndoStack.Redo();
            }
        }
        finally
        {
            _changing = false;
        }

        Reparse();
        return true;
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        UpdateModified();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        if (_changing || !ReferenceEquals(sender, Document))
        {
            return;
        }

        // Typing: parsed again a moment after the last key (11.5).
        CancelParse();
        IsParsePending = true;
        var parsing = new CancellationTokenSource();
        _parsing = parsing;
        _ = ParseLaterAsync(parsing);
    }

    private async Task ParseLaterAsync(CancellationTokenSource parsing)
    {
        try
        {
            await _delay(ParseDelay, parsing.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_parsing, parsing) || parsing.IsCancellationRequested || _disposed)
        {
            return;
        }

        _parsing = null;
        parsing.Dispose();
        Reparse();
    }

    /// <summary>Parses a typed change now, when one waits (before a choice or a navigation acts on the result).</summary>
    private void FlushParse()
    {
        if (IsParsePending)
        {
            CancelParse();
            Reparse();
        }
    }

    private void CancelParse()
    {
        if (_parsing is { } parsing)
        {
            _parsing = null;
            parsing.Cancel();
            parsing.Dispose();
        }

        IsParsePending = false;
    }

    /// <summary>
    /// Parses the result and matches its blocks to the panes' (<see cref="MatchBlocks"/>, 11.5).
    /// A panes' block whose markers are in the result shows its buttons; one whose markers are gone
    /// shows the choice that took it out, or <c>Not used</c> on both bars when typing did.
    /// </summary>
    private void Reparse()
    {
        IsParsePending = false;
        if (Document is not { } document || _read is not { } read)
        {
            return;
        }

        var current = ConflictText.Parse(document.Text, _markerSize);
        var matched = MatchBlocks(read, current, _choices);
        _current = current;
        _matched = matched;
        var bars = new ConflictBlockBars[read.BlockCount];
        for (var p = 0; p < bars.Length; p++)
        {
            bars[p] = matched[p] >= 0 ? ConflictBlockBars.Open : BarsOf(p < _choices.Length ? _choices[p] : null);
        }

        if (!bars.SequenceEqual(Bars))
        {
            Bars = bars;
        }

        ConflictsLeft = current.BlockCount;
        ResultCountText = ConflictResolverTexts.ConflictsLeft(current.BlockCount);
        UpdatePosition();
        UpdateModified();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>
    /// Matches the result's blocks to the panes' blocks (11.5), keeping their order: of the
    /// matchings that keep both in order, the one with the most evidence, where a pair's evidence
    /// is, first, the same ours and theirs lines, then the same line before the block and the same
    /// line after it; among matchings with as much evidence, the one whose blocks stand nearer
    /// their places in the file (counted from the top or from the bottom, whichever is nearer). A
    /// panes' block that a recorded choice took out of the result is matched only by its sides,
    /// and only where no other block can be. The result's blocks left between two matched pairs
    /// take, by order, the panes' blocks left there that no choice took out. So two identical
    /// blocks keep their own bars, and a block chosen earlier is never handed the block still in
    /// the result. Returns, for each panes' block, its result block or -1.
    /// </summary>
    internal static int[] MatchBlocks(ConflictText read, ConflictText current, IReadOnlyList<BlockChoice?> choices)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(choices);
        var panes = read.BlockCount;
        var result = current.BlockCount;
        var matched = Enumerable.Repeat(-1, panes).ToArray();
        if (panes == 0 || result == 0)
        {
            return matched;
        }

        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        var paneSides = read.Blocks.Select(block => SidesKey(block, keys)).ToArray();
        var resultSides = current.Blocks.Select(block => SidesKey(block, keys)).ToArray();
        var anchors = new List<(int Result, int Pane)>();
        if ((long)panes * result <= MaxWeighedPairs)
        {
            anchors = WeighedAnchors();
        }
        else
        {
            // Too many blocks to weigh every pair: each result block, in order, to the next panes'
            // block with its sides that no choice took out.
            var last = -1;
            for (var r = 0; r < result; r++)
            {
                for (var p = last + 1; p < panes; p++)
                {
                    if (!IsChosen(p) && paneSides[p] == resultSides[r])
                    {
                        anchors.Add((r, p));
                        last = p;
                        break;
                    }
                }
            }
        }

        // The blocks between two matched pairs (and before the first, after the last), by order.
        var previous = (Result: -1, Pane: -1);
        foreach (var anchor in anchors.Append((result, panes)))
        {
            var free = Enumerable.Range(previous.Pane + 1, anchor.Item2 - previous.Pane - 1).Where(p => !IsChosen(p)).ToList();
            for (int r = previous.Result + 1, i = 0; r < anchor.Item1 && i < free.Count; r++, i++)
            {
                matched[free[i]] = r;
            }

            if (anchor.Item1 < result)
            {
                matched[anchor.Item2] = anchor.Item1;
            }

            previous = anchor;
        }

        return matched;

        bool IsChosen(int pane) => pane < choices.Count && choices[pane] is not null;

        // The pairs of the matching with the most evidence, by dynamic programming over both orders.
        List<(int Result, int Pane)> WeighedAnchors()
        {
            var best = new (long Evidence, long Closeness)[result + 1, panes + 1];
            var step = new byte[result + 1, panes + 1];
            for (var r = 1; r <= result; r++)
            {
                for (var p = 1; p <= panes; p++)
                {
                    var (score, from) = best[r - 1, p].CompareTo(best[r, p - 1]) >= 0 ? (best[r - 1, p], (byte)1) : (best[r, p - 1], (byte)2);
                    if (Weigh(r - 1, p - 1) is { } pair)
                    {
                        var diagonal = (best[r - 1, p - 1].Evidence + pair.Evidence, best[r - 1, p - 1].Closeness + pair.Closeness);
                        if (diagonal.CompareTo(score) > 0)
                        {
                            (score, from) = (diagonal, 3);
                        }
                    }

                    best[r, p] = score;
                    step[r, p] = from;
                }
            }

            var pairs = new List<(int Result, int Pane)>();
            for (int r = result, p = panes; r > 0 && p > 0;)
            {
                switch (step[r, p])
                {
                    case 3:
                        pairs.Add((r - 1, p - 1));
                        r--;
                        p--;
                        break;
                    case 1:
                        r--;
                        break;
                    default:
                        p--;
                        break;
                }
            }

            pairs.Reverse();
            return pairs;
        }

        // A pair's evidence and closeness, or null without evidence.
        (long Evidence, long Closeness)? Weigh(int r, int p)
        {
            var sameSides = paneSides[p] == resultSides[r];
            if (IsChosen(p))
            {
                return sameSides ? (1, 0) : null;
            }

            var paneBlock = read.Blocks[p];
            var resultBlock = current.Blocks[r];
            var evidence = (sameSides ? 1_000_000L : 0)
                + (string.Equals(LineBefore(read, paneBlock), LineBefore(current, resultBlock), StringComparison.Ordinal) ? 1_000 : 0)
                + (string.Equals(LineAfter(read, paneBlock), LineAfter(current, resultBlock), StringComparison.Ordinal) ? 1_000 : 0);
            if (evidence == 0)
            {
                return null;
            }

            var fromTop = Math.Abs(paneBlock.StartLine - resultBlock.StartLine);
            var fromBottom = Math.Abs((read.Lines.Count - paneBlock.EndLine) - (current.Lines.Count - resultBlock.EndLine));
            return (evidence, -Math.Min(fromTop, fromBottom));
        }

        static string? LineBefore(ConflictText text, ConflictBlock block) => block.StartLine > 0 ? text.Lines[block.StartLine - 1].Text : null;

        static string? LineAfter(ConflictText text, ConflictBlock block) => block.EndLine + 1 < text.Lines.Count ? text.Lines[block.EndLine + 1].Text : null;

        // One number per distinct pair of sides, compared without line ends (as HasSameSides).
        static int SidesKey(ConflictBlock block, Dictionary<string, int> keys)
        {
            var key = string.Join('\n', [block.Ours.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), .. block.Ours.Select(line => line.Text), .. block.Theirs.Select(line => line.Text)]);
            if (!keys.TryGetValue(key, out var id))
            {
                id = keys.Count;
                keys.Add(key, id);
            }

            return id;
        }
    }

    /// <summary>Up to how many pairs of blocks <see cref="MatchBlocks"/> weighs (a file of 500 blocks); beyond it, it matches by sides and order only.</summary>
    private const long MaxWeighedPairs = 250_000;

    private static ConflictBlockBars BarsOf(BlockChoice? choice) => choice switch
    {
        BlockChoice.Ours => new(ConflictSideState.Used, ConflictSideState.NotUsed),
        BlockChoice.Theirs => new(ConflictSideState.NotUsed, ConflictSideState.Used),
        BlockChoice.OursThenTheirs or BlockChoice.TheirsThenOurs => new(ConflictSideState.Used, ConflictSideState.Used),
        _ => new(ConflictSideState.NotUsed, ConflictSideState.NotUsed),
    };

    /// <summary><c>Conflict {k} of {m}</c>: <c>m</c> the blocks as the file was read, <c>k</c> the block last reached; <c>No conflicts left</c> without a block (14.3).</summary>
    private void UpdatePosition()
    {
        if (Body != ConflictResolverBody.Text || _current is not { } current || _read is not { } read)
        {
            ConflictPositionText = string.Empty;
            return;
        }

        ConflictPositionText = current.BlockCount == 0
            ? ConflictResolverTexts.NoConflictsLeft
            : ConflictResolverTexts.ConflictPosition((CurrentBlock ?? 0) + 1, Math.Max(read.BlockCount, (CurrentBlock ?? 0) + 1));
    }

    private void UpdateModified() =>
        IsModified = Body == ConflictResolverBody.Text && Document is { } document && !string.Equals(document.Text, _original, StringComparison.Ordinal);

    private void UpdateFilePosition()
    {
        var paths = _conflicts.Select(conflict => conflict.Path).ToList();
        var at = Entry is { } entry ? paths.IndexOf(entry.Path) : -1;
        if (at >= 0)
        {
            FilePositionText = ConflictResolverTexts.FilePosition(at + 1, paths.Count);
        }

        HasOtherFiles = paths.Count > (at >= 0 ? 1 : 0) && Entry is not null;
    }

    private void UpdateResultTitle() => ResultTitle = IsResolvedElsewhere ? ConflictResolverTexts.ResolvedElsewhere : ConflictResolverTexts.ResultTitle;

    private void UpdateTitles()
    {
        var (ours, theirs) = SideNames(_read?.Blocks.FirstOrDefault()?.TheirsLabel);
        OursTitle = ConflictResolverTexts.OursTitle(ours);
        TheirsTitle = ConflictResolverTexts.TheirsTitle(theirs);
    }

    private (string Ours, string Theirs) SideNames(string? theirsLabel) =>
        ConflictResolverTexts.SideNames(_inProgress, _currentName, _ontoName, theirsLabel);

    private void NotifyToolbar()
    {
        OnPropertyChanged(nameof(TakeOursReason));
        OnPropertyChanged(nameof(TakeTheirsReason));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        TakeOursCommand.NotifyCanExecuteChanged();
        TakeTheirsCommand.NotifyCanExecuteChanged();
        MarkResolvedCommand.NotifyCanExecuteChanged();
        RunMergeToolCommand.NotifyCanExecuteChanged();
        PreviousConflictCommand.NotifyCanExecuteChanged();
        NextConflictCommand.NotifyCanExecuteChanged();
    }

    private int? FirstUnresolved()
    {
        for (var block = 0; block < _matched.Length; block++)
        {
            if (_matched[block] >= 0)
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>Whether the working-tree file is still the bytes the resolver read (11.7); a failed read counts as changed.</summary>
    private async Task<bool> IsAsReadAsync(ConflictEntry entry)
    {
        try
        {
            var now = await _actions.ReadAsync(entry);
            return now.WorkingTreeExists && now.WorkingTree.AsSpan().SequenceEqual(_readBytes);
        }
        catch (Exception ex)
        {
            _log.Warning($"Reading {entry.Path} before marking it resolved failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// The card a conflict shows instead of the panes (11.8), or null for a text conflict: a
    /// submodule; a deletion or a one-sided file (a file moved aside by a file/folder clash too);
    /// both sides deleted; a binary file or a symbolic link changed on both sides.
    /// </summary>
    private ConflictCardKind? KindOf(ConflictEntry? entry, ConflictContent? content)
    {
        if (entry is null)
        {
            return null;
        }

        if (entry.IsSubmodule)
        {
            return ConflictCardKind.Submodule;
        }

        // A file/folder clash is any kind on <p>~<label> (14.2): git also reports it as modify/delete.
        switch (entry.Kind)
        {
            case ConflictKind.DeletedByThem:
                return MovedAsideFolder(entry.Path) is not null ? ConflictCardKind.MovedAside : ConflictCardKind.DeletedByThem;
            case ConflictKind.DeletedByUs:
                return MovedAsideFolder(entry.Path) is not null ? ConflictCardKind.MovedAside : ConflictCardKind.DeletedByUs;
            case ConflictKind.AddedByUs:
                return MovedAsideFolder(entry.Path) is not null ? ConflictCardKind.MovedAside : ConflictCardKind.AddedByUs;
            case ConflictKind.AddedByThem:
                return MovedAsideFolder(entry.Path) is not null ? ConflictCardKind.MovedAside : ConflictCardKind.AddedByThem;
            case ConflictKind.BothDeleted:
                return ConflictCardKind.BothDeleted;
        }

        if (content is null)
        {
            return CardKind;
        }

        if (content.IsSymbolicLink)
        {
            return ConflictCardKind.SymbolicLink;
        }

        return content.OursBinary || content.TheirsBinary ? ConflictCardKind.Binary : null;
    }

    /// <summary>
    /// For <c>&lt;p&gt;~&lt;label&gt;</c>, the folder <c>&lt;p&gt;</c> when the status shows a path
    /// inside it (git moved the file aside for a folder of that name, 0.4); otherwise null.
    /// </summary>
    private string? MovedAsideFolder(string path)
    {
        var tilde = path.LastIndexOf('~');
        if (tilde <= 0 || path.IndexOf('/', tilde) >= 0)
        {
            return null;
        }

        var folder = path[..tilde] + "/";
        return _statusPaths.Any(other => other.StartsWith(folder, StringComparison.Ordinal))
            || _conflicts.Any(other => other.Path.StartsWith(folder, StringComparison.Ordinal))
            ? path[..tilde]
            : null;
    }

    private void DetachDocument()
    {
        if (Document is { } document)
        {
            document.TextChanged -= OnDocumentTextChanged;
        }
    }

    /// <summary>
    /// The undo stack's record of what a choice or a whole side decided for each block, in the same
    /// undo step as its text: undone and redone with it, so that the bars follow (11.5, 14.3).
    /// </summary>
    private sealed class ChoicesStep(ConflictResolverViewModel owner, BlockChoice?[] before, BlockChoice?[] after) : IUndoableOperation
    {
        public void Undo() => owner._choices = before;

        public void Redo() => owner._choices = after;
    }
}
