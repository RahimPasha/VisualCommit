using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
/// The stage panel (C4): the unstaged and staged files, the commit message editor and the
/// restore bar after a discard. It shows what <see cref="Update"/> gives it and hands every write
/// to its <see cref="IWorkingChangesHost"/>, the repository's view model.
/// </summary>
public sealed partial class WorkingChangesViewModel : ObservableObject
{
    /// <summary>The length a summary should keep to; the counter shows what is left of it.</summary>
    public const int SummaryLimit = 72;

    private readonly IWorkingChangesHost _host;
    private readonly ISettingsStore _settings;
    private WorkingTreeStatus _status = WorkingTreeStatus.Clean;
    private int _conflicts;

    /// <summary>The operation whose message the stage panel last looked at ("Merge:&lt;sha&gt;"), or null.</summary>
    private string? _messageKey;

    /// <summary>The summary and description as they were filled from <c>MERGE_MSG</c>, or null.</summary>
    private (string Summary, string Description)? _filledMessage;

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
    [NotifyPropertyChangedFor(nameof(SummaryCounter), nameof(IsSummaryOverLimit), nameof(CanCommit))]
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
    [NotifyPropertyChangedFor(nameof(CommitButtonText), nameof(CanCommit), nameof(CommitToolTip))]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial bool IsAmend { get; set; }

    /// <summary>There is a commit to amend.</summary>
    [ObservableProperty]
    public partial bool CanAmend { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommitButtonText), nameof(CanCommit))]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial bool IsCommitting { get; private set; }

    /// <summary>The commit button's label.</summary>
    public string CommitButtonText =>
        IsCommitting ? "Committing…"
        : IsAmend ? "Amend previous commit"
        : _status.Staged.Count == 0 ? (CanCommitMerge ? MergeCommitText : "Stage files to commit")
        : _status.Staged.Count == 1 ? "Commit 1 file"
        : string.Create(CultureInfo.InvariantCulture, $"Commit {_status.Staged.Count} files");

    /// <summary>
    /// The summary has text other than spaces and there is something to commit or amend: staged
    /// files, or a merge without conflicts left, which git records even with nothing staged
    /// (docs/phase-3-design.md section 5, D89).
    /// </summary>
    public bool CanCommit => !IsCommitting && !string.IsNullOrWhiteSpace(Summary) && (IsAmend || _status.Staged.Count > 0 || CanCommitMerge);

    /// <summary>
    /// The commit button's tooltip, or null: during a cherry-pick or revert that changes nothing,
    /// why the button stays disabled (section 5).
    /// </summary>
    public string? CommitToolTip => _status.InProgress is { Empty: true, Kind: InProgressKind.CherryPick or InProgressKind.Revert } empty && !IsAmend
        ? $"This {(empty.Kind == InProgressKind.CherryPick ? "cherry-pick" : "revert")} changes nothing: abort it"
        : null;

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
        CanAmend = hasHead;
        if (!hasHead)
        {
            IsAmend = false;
        }

        _conflicts = status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);
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
        OnPropertyChanged(nameof(CommitToolTip));
        CommitCommand.NotifyCanExecuteChanged();
        _ = FollowOperationMessageAsync(status.InProgress);
    }

    /// <summary>
    /// The message of the merge, cherry-pick or revert in progress (section 5): when one starts,
    /// or the stage panel first sees it, and the summary and description are empty, they are
    /// filled from <c>MERGE_MSG</c> with every line that starts with <c>#</c> removed. Done once
    /// per operation, so a message the user cleared stays clear. When the operation ends
    /// (committed or aborted), a message still as it was filled in is cleared.
    /// </summary>
    private async Task FollowOperationMessageAsync(InProgressState state)
    {
        var key = state is { Kind: InProgressKind.Merge or InProgressKind.CherryPick or InProgressKind.Revert, Commit: { } commit }
            ? $"{state.Kind}:{commit}"
            : null;
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

        var text = await _host.ReadMergeMessageAsync();
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
    /// A message as <c>git commit</c> would take it from a file (section 5): the lines that start
    /// with <c>#</c> removed, then the first line as the summary and the rest, after the blank
    /// lines that follow it, as the description; trailing spaces and blank lines dropped.
    /// </summary>
    public static (string Summary, string Description) SplitMessage(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => !line.StartsWith('#'))
            .Select(line => line.TrimEnd())
            .SkipWhile(line => line.Length == 0)
            .ToList();
        if (lines.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var description = string.Join('\n', lines.Skip(1)).Trim('\n');
        return (lines[0], description);
    }

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

    [RelayCommand]
    private Task StageFile(ChangedFileRow? row) => row?.File is { } file ? Run(_host.StageAsync([file])) : Task.CompletedTask;

    [RelayCommand]
    private Task DiscardFile(ChangedFileRow? row) => row?.File is { } file ? Run(_host.DiscardAsync([file])) : Task.CompletedTask;

    [RelayCommand]
    private Task UnstageFile(ChangedFileRow? row) => row?.File is { } file ? Run(_host.UnstageAsync([file])) : Task.CompletedTask;

    [RelayCommand]
    private Task StageAll() => Run(_host.StageAsync(_status.Unstaged));

    [RelayCommand]
    private Task DiscardAll() => Run(_host.DiscardAsync(_status.Unstaged));

    [RelayCommand]
    private Task UnstageAll() => Run(_host.UnstageAsync(_status.Staged));

    /// <summary>The host hides the bar once the files are restored; a cancelled or failed restore keeps it.</summary>
    [RelayCommand]
    private Task Restore() => Run(_host.RestoreAsync());

    [RelayCommand]
    private void DismissRestore() => RestoreText = null;

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task Commit()
    {
        ErrorText = null;
        IsCommitting = true;
        try
        {
            var message = string.IsNullOrWhiteSpace(Description) ? Summary : $"{Summary}\n\n{Description}";
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
}
