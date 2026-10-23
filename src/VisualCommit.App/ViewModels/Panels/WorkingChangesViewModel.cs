using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>What the stage panel asks its repository to do. Each write returns git's message when it fails, or null.</summary>
public interface IWorkingChangesHost
{
    /// <summary>HEAD has a commit, so there is something to amend.</summary>
    bool HasHead { get; }

    Task<string?> StageAsync(IReadOnlyList<ChangedFile> files);

    Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files);

    /// <summary>Asks for confirmation, saves a snapshot and discards the files' unstaged changes (D62, D67).</summary>
    Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files);

    /// <summary>Puts back what the last discard took (D62, D73), asking first when the files changed since (D75).</summary>
    Task<string?> RestoreAsync();

    /// <summary>Commits or amends with <paramref name="message"/> (D68).</summary>
    Task<string?> CommitAsync(string message, bool amend);

    /// <summary>HEAD's subject and body, for amending.</summary>
    Task<(string Subject, string Body)?> ReadHeadMessageAsync();

    /// <summary>Opens a file's diff in place of the graph (D63).</summary>
    void OpenDiff(ChangedFile file, bool staged);

    /// <summary>
    /// <c>MERGE_MSG</c> as git left it, or null when there is none: the message a merge,
    /// cherry-pick or revert in progress fills an empty commit message with (docs/phase-3-design.md section 5).
    /// </summary>
    Task<string?> ReadMergeMessageAsync() => Task.FromResult<string?>(null);

    /// <summary>
    /// The stage panel's "Continue cherry-pick", "Continue revert" or "Continue rebase"
    /// (docs/phase-4-design.md 5.3): the banner's Continue with <paramref name="message"/>, or null
    /// when the boxes still hold what was filled in (the git layer then reads the operation's own
    /// message). Returns the stage panel's error text, or null: Continue's failures open the
    /// error dialog. The default does nothing.
    /// </summary>
    Task<string?> ContinueAsync(string? message) => Task.FromResult<string?>(null);

    /// <summary>
    /// The message the stage panel fills in for <paramref name="state"/>'s stopped commit (5.3):
    /// <c>MERGE_MSG</c> less git's comment block, a rebase's own message, or a group's. The
    /// default is <see cref="ReadMergeMessageAsync"/>.
    /// </summary>
    Task<string?> ReadOperationMessageAsync(InProgressState state) => ReadMergeMessageAsync();
}

/// <summary>
/// The questions the stage panel asks before it stages or commits over conflict markers
/// (docs/phase-4-design.md 7.1, 7.2, 14.3, D148). A host that implements it besides
/// <see cref="IWorkingChangesHost"/> is asked; without it the panel stages and commits unasked.
/// </summary>
public interface IConflictMarkerQuestions
{
    /// <summary>
    /// Before <paramref name="files"/> are staged: asks 7.2's "{file} still has conflict
    /// markers." when a conflicted file among them still has conflict markers in its working-tree
    /// file. True to go ahead (nothing to ask, or the user said yes).
    /// </summary>
    Task<bool> ConfirmStagingAsync(IReadOnlyList<ChangedFile> files);

    /// <summary>
    /// Before the commit that concludes <paramref name="state"/>'s merge or single pick or revert
    /// (14.3): asks 7.2's "{N} files still have conflict markers." when the paths that were
    /// conflicted at the stop have markers staged. True to go ahead.
    /// </summary>
    Task<bool> ConfirmConcludingAsync(InProgressState state);
}

/// <summary>
/// The stage panel (C4): the unstaged and staged files, the commit message editor and the
/// restore bar after a discard. It shows what <see cref="Update"/> gives it and hands every write
/// to its <see cref="IWorkingChangesHost"/>, the repository's view model. During a list of picks
/// or reverts, or a rebase, its commit button is the banner's Continue with the panel's message
/// (docs/phase-4-design.md 5.3).
/// </summary>
public sealed partial class WorkingChangesViewModel : ObservableObject
{
    /// <summary>The length a summary should keep to; the counter shows what is left of it.</summary>
    public const int SummaryLimit = 72;

    private readonly IWorkingChangesHost _host;
    private readonly ISettingsStore _settings;
    private WorkingTreeStatus _status = WorkingTreeStatus.Clean;
    private int _conflicts;

    /// <summary>The stopped commit whose message the stage panel last looked at ("Merge:&lt;sha&gt;", "Rebase:&lt;sha&gt;:&lt;k&gt;"), or null.</summary>
    private string? _messageKey;

    /// <summary>The summary and description as they were filled from the operation's message, or null.</summary>
    private (string Summary, string Description)? _filledMessage;

    /// <summary>The last read of the operation's message for the boxes, started by a status update.</summary>
    private Task _messageFill = Task.CompletedTask;

    public WorkingChangesViewModel(IWorkingChangesHost host, ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(settings);
        _host = host;
        _settings = settings;
        FileList = settings.Current.FileList;
        Unstaged.SetMode(FileList);
        Staged.SetMode(FileList);
    }

    /// <summary>The files with unstaged changes, the untracked and the conflicted files.</summary>
    public ChangedFileList Unstaged { get; } = new();

    /// <summary>The files whose index differs from HEAD's.</summary>
    public ChangedFileList Staged { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFlat), nameof(IsTree))]
    public partial FileListMode FileList { get; private set; }

    public bool IsFlat => FileList == FileListMode.Flat;

    public bool IsTree => FileList == FileListMode.Tree;

    [ObservableProperty]
    public partial string UnstagedTitle { get; private set; } = "Unstaged files (0)";

    [ObservableProperty]
    public partial string StagedTitle { get; private set; } = "Staged files (0)";

    [ObservableProperty]
    public partial bool HasUnstaged { get; private set; }

    [ObservableProperty]
    public partial bool HasStaged { get; private set; }

    /// <summary>The commit's first line.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryCounter), nameof(IsSummaryOverLimit), nameof(CanCommit), nameof(CommitToolTip))]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial string Summary { get; set; } = string.Empty;

    /// <summary>The rest of the commit's message, after a blank line.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    /// <summary>What is left of <see cref="SummaryLimit"/> characters; negative past it.</summary>
    public string SummaryCounter => (SummaryLimit - Summary.Length).ToString(CultureInfo.InvariantCulture);

    public bool IsSummaryOverLimit => Summary.Length > SummaryLimit;

    /// <summary>The commit replaces HEAD's ("Amend previous commit").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommitButtonText), nameof(CanCommit), nameof(CommitDisabledReason), nameof(CommitToolTip))]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial bool IsAmend { get; set; }

    /// <summary>There is a commit to amend, and no list or rebase is stopped (docs/phase-4-design.md 5.3).</summary>
    [ObservableProperty]
    public partial bool CanAmend { get; private set; }

    /// <summary>
    /// Why "Amend previous commit" is disabled during a list of picks or reverts or a rebase stop
    /// (5.3): "Finish or abort the {kind} first"; null otherwise.
    /// </summary>
    [ObservableProperty]
    public partial string? AmendToolTip { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommitButtonText), nameof(CanCommit), nameof(CommitToolTip))]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial bool IsCommitting { get; private set; }

    /// <summary>
    /// The commit button continues the operation in progress instead of committing (5.3): a list
    /// of picks or reverts, or a rebase. A merge and a single pick or revert keep phase 3's Commit.
    /// </summary>
    public bool ContinuesOperation => _status.InProgress is { Kind: InProgressKind.Rebase } || ConflictBannerViewModel.InList(_status.InProgress);

    /// <summary>The commit button's label: "Continue cherry-pick", "Continue revert" or "Continue rebase" while it continues the operation (5.3).</summary>
    public string CommitButtonText =>
        ContinuesOperation ? $"Continue {ConflictBannerViewModel.KindName(_status.InProgress.Kind)}"
        : IsCommitting ? "Committing…"
        : IsAmend ? "Amend previous commit"
        : _status.Staged.Count == 0 ? (CanCommitMerge ? MergeCommitText : "Stage files to commit")
        : _status.Staged.Count == 1 ? "Commit 1 file"
        : string.Create(CultureInfo.InvariantCulture, $"Commit {_status.Staged.Count} files");

    /// <summary>
    /// The summary has text other than spaces and there is something to commit or amend: staged
    /// files, or a merge without conflicts left, which git records even with nothing staged
    /// (docs/phase-3-design.md section 5, D89). While it continues the operation (5.3) it is
    /// enabled as the banner's Continue is, and the summary may be empty (the operation's own
    /// message is used then).
    /// </summary>
    public bool CanCommit => ContinuesOperation
        ? !IsCommitting && ContinueReason is null
        : !IsCommitting && !string.IsNullOrWhiteSpace(Summary) && (IsAmend || _status.Staged.Count > 0 || CanCommitMerge);

    /// <summary>
    /// Why the commit button is disabled, or null (docs/phase-6-design.md 6.8): while it continues
    /// the operation, Continue's reason (5.3, 10.4); during a cherry-pick or revert that changes
    /// nothing, why the button stays disabled (5.3); none for an empty summary or nothing staged.
    /// The palette's commit entry reads it (5.4's row 6).
    /// </summary>
    public string? CommitDisabledReason =>
        ContinuesOperation ? ContinueReason
        : _status.InProgress is { Empty: true, Kind: InProgressKind.CherryPick or InProgressKind.Revert } empty && !IsAmend ? ConflictBannerViewModel.ChangesNothing(empty.Kind)
        : null;

    /// <summary>
    /// The commit button's tooltip and help text (6.8, D199): its reason while disabled, or none;
    /// while enabled what it does with its key, <c>Commit 1 file (Ctrl+Enter)</c>,
    /// <c>Continue rebase (Ctrl+Enter)</c> (the key works from the message boxes, 6.10).
    /// </summary>
    public string? CommitToolTip => CommitDisabledReason ?? (CanCommit ? Keys.WithKey(CommitButtonText, AppCommand.Commit) : null);

    /// <summary>
    /// Whose words the commit button's key uses (6.4): <see cref="KeyTexts.Current"/>,
    /// unless a test of another platform's texts sets its own.
    /// </summary>
    public KeyTexts Keys
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
            OnPropertyChanged(nameof(CommitToolTip));
        }
    } = KeyTexts.Current;

    /// <summary>Why the banner's Continue is disabled for the state shown (5.1), or null.</summary>
    private string? ContinueReason => ConflictBannerViewModel.ContinueFor(_status.InProgress, _conflicts, _status.HasUnstagedTrackedChanges).DisabledReason;

    /// <summary>The label of the commit button that records a merge with nothing staged.</summary>
    public const string MergeCommitText = "Commit merge";

    /// <summary>A merge whose conflicts are all resolved: it can be committed with nothing staged.</summary>
    private bool CanCommitMerge => _status.InProgress.Kind == InProgressKind.Merge && _conflicts == 0;

    /// <summary>Git's message when the last commit or other write failed, shown under the commit button; null otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; set; }

    public bool HasError => ErrorText is not null;

    /// <summary>What the restore bar says (D62), or null when it is not shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRestore))]
    public partial string? RestoreText { get; private set; }

    public bool HasRestore => RestoreText is not null;

    /// <summary>Shows what the working tree and index hold now.</summary>
    public void Update(WorkingTreeStatus status, bool hasHead)
    {
        ArgumentNullException.ThrowIfNull(status);
        _status = status;
        _conflicts = status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);

        // A list or a rebase stop: the commit continues it, and nothing is amended (5.3).
        var continues = ContinuesOperation;
        CanAmend = hasHead && !continues;
        AmendToolTip = continues ? OperationTexts.FinishOrAbortFirst(status.InProgress.Kind) : null;
        if (!CanAmend)
        {
            IsAmend = false;
        }

        SyncFileList();
        Unstaged.Show(status.Unstaged, keepFolders: true);
        Staged.Show(status.Staged, keepFolders: true);
        UnstagedTitle = _conflicts > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Unstaged files ({status.Unstaged.Count}, {_conflicts} with conflicts)")
            : string.Create(CultureInfo.InvariantCulture, $"Unstaged files ({status.Unstaged.Count})");
        StagedTitle = string.Create(CultureInfo.InvariantCulture, $"Staged files ({status.Staged.Count})");
        HasUnstaged = status.Unstaged.Count > 0;
        HasStaged = status.Staged.Count > 0;
        OnPropertyChanged(nameof(CommitButtonText));
        OnPropertyChanged(nameof(CanCommit));
        OnPropertyChanged(nameof(CommitDisabledReason));
        OnPropertyChanged(nameof(CommitToolTip));
        CommitCommand.NotifyCanExecuteChanged();
        _messageFill = FollowOperationMessageAsync(status.InProgress);
    }

    /// <summary>
    /// The message of the stopped commit (docs/phase-4-design.md 5.3): when a merge, a cherry-pick
    /// or revert, or a rebase stops on a commit, or the stage panel first sees the stop, and the
    /// summary and description are empty, they are filled with
    /// <see cref="IWorkingChangesHost.ReadOperationMessageAsync"/>'s message (<c>MERGE_MSG</c> less
    /// git's comment block, a rebase's own message, a group's). Done once per stopped commit, so a
    /// message the user cleared stays clear. When the operation goes on or ends, a message still as
    /// it was filled in is cleared. A list paused between commits fills nothing (8).
    /// </summary>
    private async Task FollowOperationMessageAsync(InProgressState state)
    {
        var key = MessageKey(state);
        if (key == _messageKey)
        {
            return;
        }

        if (_filledMessage is { } filled && Summary == filled.Summary && Description == filled.Description)
        {
            Summary = string.Empty;
            Description = string.Empty;
        }

        _filledMessage = null;
        _messageKey = key;
        if (key is null || Summary.Length > 0 || Description.Length > 0 || IsAmend)
        {
            return;
        }

        var text = await _host.ReadOperationMessageAsync(state);
        if (text is null || _messageKey != key || Summary.Length > 0 || Description.Length > 0 || IsAmend)
        {
            return;
        }

        var (summary, description) = SplitMessage(text);
        Summary = summary;
        Description = description;
        _filledMessage = (summary, description);
    }

    /// <summary>
    /// Which stopped commit the message belongs to: "&lt;kind&gt;:&lt;sha&gt;" for a merge, pick or
    /// revert with its pick head, "Rebase:&lt;sha&gt;:&lt;k&gt;" for a rebase stop; null for a
    /// list paused between commits, git am and nothing in progress.
    /// </summary>
    private static string? MessageKey(InProgressState state)
    {
        var paused = state.Sequenced && !state.HasPickHead;
        return state switch
        {
            { Kind: InProgressKind.Merge, Commit: { } merged } => $"Merge:{merged}",
            { Kind: InProgressKind.CherryPick or InProgressKind.Revert, Commit: { } picked } when !paused => $"{state.Kind}:{picked}",
            { Kind: InProgressKind.Rebase } => string.Create(CultureInfo.InvariantCulture, $"Rebase:{state.Commit}:{state.Done}"),
            _ => null,
        };
    }

    /// <summary>
    /// A message as the stage panel shows it (5.3, as the message dialog splits it): its first
    /// line, after any blank lines it starts with, is the summary; the rest, without the blank
    /// lines that follow the summary and those at its end, the description; trailing spaces
    /// dropped. No line is removed: a commit's own <c>#</c> lines stay (D68); git's comment block
    /// is left out by the git layer (<see cref="IGitRepository.ReadOperationMessageAsync"/>).
    /// </summary>
    public static (string Summary, string Description) SplitMessage(string text) => MessageDialogViewModel.Split(text);

    /// <summary>The message the boxes make (14.3): the summary alone when the description is blank, else the summary, an empty line and the description.</summary>
    public string JoinedMessage => string.IsNullOrWhiteSpace(Description) ? Summary : $"{Summary}\n\n{Description}";

    /// <summary>The boxes still hold what was filled in for the stopped commit (5.3): Continue then passes no message.</summary>
    public bool HoldsFilledMessage => _filledMessage is { } filled && Summary == filled.Summary && Description == filled.Description;

    /// <summary>
    /// The boxes are still waiting for the operation's message, read after the last status update
    /// (5.3): the checks wait for it before they look at the panel.
    /// </summary>
    public bool IsFillingMessage => !_messageFill.IsCompleted;

    /// <summary>Takes the Flat or Tree setting again: the commit details may have changed it (D70).</summary>
    public void SyncFileList()
    {
        FileList = _settings.Current.FileList;
        Unstaged.SetMode(FileList);
        Staged.SetMode(FileList);
    }

    /// <summary>Marks the row of the file whose diff is open; null paths clear both lists.</summary>
    public void ShowSelection(string? path, bool staged)
    {
        Unstaged.SelectedPath = staged ? null : path;
        Staged.SelectedPath = staged ? path : null;
    }

    /// <summary>Shows the restore bar for a discard: "Discarded changes to …" (D62).</summary>
    public void ShowRestore(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        RestoreText = paths.Count == 1
            ? $"Discarded changes to {FileName(paths[0])}."
            : string.Create(CultureInfo.InvariantCulture, $"Discarded changes to {paths.Count} files.");
    }

    /// <summary>Hides the restore bar: the discard has been restored.</summary>
    public void HideRestore() => RestoreText = null;

    /// <summary>The file part of a path: what dialogs and the restore bar name a file by.</summary>
    public static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    [RelayCommand]
    private void ShowFlat() => SetFileList(FileListMode.Flat);

    [RelayCommand]
    private void ShowTree() => SetFileList(FileListMode.Tree);

    /// <summary>A file row was clicked: opens its diff.</summary>
    [RelayCommand]
    private void ActivateUnstaged(ChangedFileRow? row)
    {
        if (row?.File is { } file)
        {
            _host.OpenDiff(file, staged: false);
        }
    }

    [RelayCommand]
    private void ActivateStaged(ChangedFileRow? row)
    {
        if (row?.File is { } file)
        {
            _host.OpenDiff(file, staged: true);
        }
    }

    /// <summary>A row's stage button: a conflicted file whose working-tree file still has markers asks first (7.1, 7.2).</summary>
    [RelayCommand]
    private Task StageFile(ChangedFileRow? row) => row?.File is { } file ? StageAsync([file]) : Task.CompletedTask;

    [RelayCommand]
    private Task DiscardFile(ChangedFileRow? row) => row?.File is { } file ? Run(_host.DiscardAsync([file])) : Task.CompletedTask;

    [RelayCommand]
    private Task UnstageFile(ChangedFileRow? row) => row?.File is { } file ? Run(_host.UnstageAsync([file])) : Task.CompletedTask;

    /// <summary>The unstaged list's "Stage all": asks first when a conflicted file still has markers (7.1, 7.2).</summary>
    [RelayCommand]
    private Task StageAll() => StageAsync(_status.Unstaged);

    [RelayCommand]
    private Task DiscardAll() => Run(_host.DiscardAsync(_status.Unstaged));

    [RelayCommand]
    private Task UnstageAll() => Run(_host.UnstageAsync(_status.Staged));

    /// <summary>The host hides the bar once the files are restored; a cancelled or failed restore keeps it.</summary>
    [RelayCommand]
    private Task Restore() => Run(_host.RestoreAsync());

    [RelayCommand]
    private void DismissRestore() => RestoreText = null;

    /// <summary>
    /// The commit button: commits (or amends) with the boxes' message; during a list or a rebase
    /// it is the banner's Continue with the boxes' message (5.3). The commit that concludes a
    /// merge or a single pick or revert asks first when markers are staged (14.3).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task Commit()
    {
        if (ContinuesOperation)
        {
            await ContinueAsync();
            return;
        }

        ErrorText = null;
        IsCommitting = true;
        try
        {
            if (!IsAmend && _status.InProgress.IsActive && _host is IConflictMarkerQuestions questions && !await questions.ConfirmConcludingAsync(_status.InProgress))
            {
                return;
            }

            var message = JoinedMessage;
            var error = await _host.CommitAsync(message, IsAmend);
            if (error is not null)
            {
                ErrorText = error;
                return;
            }

            Summary = string.Empty;
            Description = string.Empty;
            IsAmend = false;
        }
        finally
        {
            IsCommitting = false;
        }
    }

    /// <summary>
    /// Undo of a commit or an amend left its changes staged (docs/phase-5-design.md 7.5): the
    /// summary and the description get the undone commit's message when both are empty, as
    /// GitKraken does, so that committing again needs no typing.
    /// </summary>
    public void PutBackUndoneMessage(string summary, string description)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(description);
        if (Summary.Length > 0 || Description.Length > 0)
        {
            return;
        }

        Summary = summary;
        Description = description;
    }

    /// <summary>
    /// Redo of that commit (7.5): boxes that still hold its message are emptied again, as after the
    /// commit itself (phase 4's rule for filled messages); a message changed since stays.
    /// </summary>
    public void TakeBackRedoneMessage(string summary, string description)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(description);
        if (string.Equals(Summary, summary, StringComparison.Ordinal) && string.Equals(Description, description, StringComparison.Ordinal))
        {
            Summary = string.Empty;
            Description = string.Empty;
        }
    }

    /// <summary>Ticking "Amend previous commit" fills empty boxes with HEAD's message.</summary>
    async partial void OnIsAmendChanged(bool value)
    {
        if (!value || Summary.Length > 0 || Description.Length > 0)
        {
            return;
        }

        if (await _host.ReadHeadMessageAsync() is { } message && IsAmend && Summary.Length == 0 && Description.Length == 0)
        {
            Summary = message.Subject;
            Description = message.Body;
        }
    }

    private void SetFileList(FileListMode mode)
    {
        if (FileList != mode)
        {
            _settings.Update(settings => settings with { FileList = mode });
            SyncFileList();
        }
    }

    /// <summary>Runs a write and shows git's message if it fails.</summary>
    private async Task Run(Task<string?> write)
    {
        ErrorText = null;
        ErrorText = await write;
    }

    /// <summary>Stages <paramref name="files"/>, after the host's question about conflict markers (7.1, 7.2).</summary>
    private async Task StageAsync(IReadOnlyList<ChangedFile> files)
    {
        if (_host is IConflictMarkerQuestions questions && !await questions.ConfirmStagingAsync(files))
        {
            return;
        }

        await Run(_host.StageAsync(files));
    }

    /// <summary>
    /// "Continue cherry-pick", "Continue revert", "Continue rebase" (5.3): the banner's Continue
    /// with <see cref="ContinueMessage"/>. When the operation went on (another stopped commit, or
    /// nothing in progress), the boxes are emptied and filled again for the next stop.
    /// </summary>
    private async Task ContinueAsync()
    {
        ErrorText = null;
        IsCommitting = true;
        var mark = MarkContinue();
        try
        {
            var error = await _host.ContinueAsync(ContinueMessage);
            if (error is not null)
            {
                ErrorText = error;
                return;
            }
        }
        finally
        {
            IsCommitting = false;
        }

        await AfterContinueAsync(mark);
    }

    /// <summary>
    /// The message Continue commits with, from the banner or this panel (5.3, 5.4): the boxes'
    /// message when the user changed what was filled in; null, so that the git layer reads the
    /// operation's own, while the boxes still hold what was filled in or the summary is empty
    /// ("The summary may be empty: the operation's own message is used then").
    /// </summary>
    public string? ContinueMessage => HoldsFilledMessage || string.IsNullOrWhiteSpace(Summary) ? null : JoinedMessage;

    /// <summary>
    /// The banner's Continue (5.4), which commits as this panel's Continue does: <paramref name="run"/>
    /// gets <see cref="ContinueMessage"/>; afterwards, when the operation went on, a message the
    /// user typed (and Continue committed) is cleared and the next stop's message is filled in.
    /// </summary>
    public async Task ContinueWithMessageAsync(Func<string?, Task> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var mark = MarkContinue();
        await run(ContinueMessage);
        await AfterContinueAsync(mark);
    }

    /// <summary>
    /// The banner's Skip (5.4): <paramref name="run"/> asks and skips; afterwards, when the
    /// operation went on (another stopped commit, or nothing in progress), a message the user
    /// typed for the skipped commit is cleared, as after a Continue, and the next stop's message
    /// is filled in from the operation (the review's finding 17: it was committed at the next stop).
    /// </summary>
    public async Task SkipClearingMessageAsync(Func<Task> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var mark = MarkContinue();
        await run();
        await AfterContinueAsync(mark);
    }

    /// <summary>What <see cref="AfterContinueAsync"/> compares: the stopped commit and the boxes before Continue.</summary>
    private (string? Key, string Summary, string Description) MarkContinue() => (MessageKey(_status.InProgress), Summary, Description);

    /// <summary>
    /// After a Continue: the refresh after the operation has shown the new state already, which
    /// cleared a message that was still as filled in and filled the next one. A message the user
    /// typed is still there; it belonged to the commit Continue made, so it is cleared and the next
    /// stop's message filled in.
    /// </summary>
    private async Task AfterContinueAsync((string? Key, string Summary, string Description) before)
    {
        var state = _status.InProgress;
        if (MessageKey(state) != before.Key && (Summary, Description) == (before.Summary, before.Description) && (before.Summary.Length > 0 || before.Description.Length > 0))
        {
            Summary = string.Empty;
            Description = string.Empty;
            _filledMessage = null;
            _messageKey = null;
            await FollowOperationMessageAsync(state);
        }
    }
}
