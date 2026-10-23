using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// One repository the checklist found (docs/phase-6-design.md 1.9): its name, then its path
/// relative to the chosen folder, or <c>Already in this workspace</c> (checked, disabled), or for
/// the repository that holds the chosen folder <c>Contains {folder name}</c> (unchecked). The
/// others start checked.
/// </summary>
public sealed partial class FoundRepositoryRow : ObservableObject
{
    private readonly Action _checkedChanged;

    internal FoundRepositoryRow(string name, string workingDirectory, string detail, bool isChecked, bool isEnabled, bool isEnclosing, Action checkedChanged)
    {
        _checkedChanged = checkedChanged;
        Name = name;
        WorkingDirectory = workingDirectory;
        Detail = detail;
        IsEnabled = isEnabled;
        IsEnclosing = isEnclosing;
        IsChecked = isChecked;
    }

    /// <summary>The repository's name (primary; the row's automation name).</summary>
    public string Name { get; }

    /// <summary>Its working tree as git reported it: what the dialog answers when the row is checked.</summary>
    public string WorkingDirectory { get; }

    /// <summary>The secondary text after the name, cut with "…", whole in its tooltip.</summary>
    public string Detail { get; }

    /// <summary>The row's box can be changed: false for a repository the workspace holds already.</summary>
    public bool IsEnabled { get; }

    /// <summary>The workspace holds the repository already (the box checked and disabled).</summary>
    public bool IsAlreadyInWorkspace => !IsEnabled;

    /// <summary>The repository holds the chosen folder (listed first, unchecked).</summary>
    public bool IsEnclosing { get; }

    /// <summary>The row's box (<c>FoundRepositoryCheck</c>).</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    /// <summary>The row is added when the dialog is confirmed: checked and enabled.</summary>
    public bool IsAdded => IsChecked && IsEnabled;

    partial void OnIsCheckedChanged(bool value) => _checkedChanged();
}

/// <summary>
/// The "Add repositories" dialog (<c>AddRepositoriesDialog</c>, docs/phase-6-design.md 1.8, 1.9):
/// the folder scan over a chosen folder that is no working tree's top, then each candidate
/// confirmed by git, then a checklist of what was found. Answers the working trees to append, in
/// the list's order, or null when cancelled. Cancel and Esc stop the search and the opens and add
/// nothing. Used on the UI thread: the scan runs on the thread pool, its progress and every result
/// come back to the thread the dialog was made on.
/// </summary>
public sealed partial class AddRepositoriesDialogViewModel : DialogViewModel<IReadOnlyList<string>?>
{
    /// <summary>How many candidates are opened at once (1.8's "Confirming").</summary>
    public const int OpensAtOnce = 4;

    private readonly string _folder;
    private readonly IRepositoryProvider _provider;
    private readonly Func<string, bool> _isInWorkspace;
    private readonly IGitRepository? _enclosing;
    private readonly IAppLog _log;
    private readonly ScanOptions _options;
    private readonly IScanFileSystem? _fileSystem;
    private readonly CancellationTokenSource _cancel = new();
    private Task? _search;

    /// <param name="workspaceName">The workspace's name, for the title.</param>
    /// <param name="folder">The chosen folder, as the folder dialog answered it.</param>
    /// <param name="provider">Opens each candidate (and logs <c>Opened the repository at …</c>, as a tab's open does).</param>
    /// <param name="isInWorkspace">Whether the workspace holds a working tree already (1.2's <c>WorkspaceMembership</c>).</param>
    /// <param name="enclosing">The repository whose working tree holds <paramref name="folder"/> (opened, not at its top), listed first; null when none does.</param>
    /// <param name="log">Where an unexpected failure is logged.</param>
    /// <param name="options">The scan's limits; 1.8's by default.</param>
    /// <param name="fileSystem">The file system the scan reads; the real one by default (tests give a fake).</param>
    public AddRepositoriesDialogViewModel(
        string workspaceName,
        string folder,
        IRepositoryProvider provider,
        Func<string, bool> isInWorkspace,
        IGitRepository? enclosing = null,
        IAppLog? log = null,
        ScanOptions? options = null,
        IScanFileSystem? fileSystem = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceName);
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(isInWorkspace);
        Title = WorkspaceTexts.AddRepositoriesTitle(workspaceName);
        _folder = folder;
        _provider = provider;
        _isInWorkspace = isInWorkspace;
        _enclosing = enclosing;
        _log = log ?? NullAppLog.Instance;
        _options = options ?? new ScanOptions();
        _fileSystem = fileSystem;
        FolderText = WorkspaceTexts.LookingIn(folder);
        ProgressText = WorkspaceTexts.Searched(0, 0);
    }

    public override string Title { get; }

    /// <summary>The chosen folder.</summary>
    public string Folder => _folder;

    /// <summary>The search or the opens still run (the searching state).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDone), nameof(ShowsProgress), nameof(ShowsList))]
    public partial bool IsSearching { get; private set; } = true;

    /// <summary>The checklist is there.</summary>
    public bool IsDone => !IsSearching;

    /// <summary><c>ScanFolderText</c>: <c>Looking for repositories in {folder}…</c>, then what was found.</summary>
    [ObservableProperty]
    public partial string FolderText { get; private set; }

    /// <summary><c>ScanProgressText</c> while searching: <c>Searched {n} folders, found {k} repositories</c>.</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; }

    public bool ShowsProgress => IsSearching;

    /// <summary>The rows, the repository holding the folder first, then by relative path.</summary>
    public ObservableCollection<FoundRepositoryRow> Rows { get; } = [];

    /// <summary><c>FoundList</c> shows once done with a row to show.</summary>
    public bool ShowsList => IsDone && Rows.Count > 0;

    /// <summary><c>ScanStoppedText</c>, or null when the search did not stop at its limit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStopped))]
    public partial string? StoppedText { get; private set; }

    public bool ShowsStopped => StoppedText is not null;

    /// <summary><c>ScanFailedText</c>, or null when git opened every candidate.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFailed))]
    public partial string? FailedText { get; private set; }

    /// <summary><c>ScanFailedText</c>'s tooltip: the candidates' relative paths, at most 10, then <c>…</c>.</summary>
    [ObservableProperty]
    public partial string? FailedToolTip { get; private set; }

    public bool ShowsFailed => FailedText is not null;

    /// <summary>The confirm button's label: <c>Add {N} repositories</c>, <c>Add 1 repository</c>, or <c>Add repositories</c> while it is disabled.</summary>
    [ObservableProperty]
    public partial string ConfirmText { get; private set; } = WorkspaceTexts.AddRepositoriesConfirm;

    /// <summary>The confirm button is enabled: the search is done and a row would be added.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool CanConfirm { get; private set; }

    /// <summary>The search and its opens; completes when the checklist is there or the dialog was cancelled.</summary>
    public Task Search => _search ?? Task.CompletedTask;

    protected override IReadOnlyList<string>? CancelledResult => null;

    /// <summary>
    /// A report of the search (1.9) while it runs: <see cref="ProgressText"/> says it, unless it
    /// counts fewer folders than the one shown, a report that came late: a
    /// <see cref="Progress{T}"/> made where no synchronization context is current posts its reports
    /// to the thread pool, where two can run out of order.
    /// </summary>
    public void ShowProgress(ScanProgress report)
    {
        ArgumentNullException.ThrowIfNull(report);
        lock (_progressGate)
        {
            if (!IsSearching || report.FoldersSearched < _shownFolders)
            {
                return;
            }

            _shownFolders = report.FoldersSearched;
            ProgressText = WorkspaceTexts.Searched(report.FoldersSearched, report.Found);
        }
    }

    private readonly Lock _progressGate = new();
    private int _shownFolders;

    /// <summary>Starts the search (once); its task is <see cref="Search"/>.</summary>
    public Task Start() => _search ??= RunAsync();

    protected override void OnCancel()
    {
        _cancel.Cancel();
        base.OnCancel();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        IReadOnlyList<string> added = [.. Rows.Where(row => row.IsAdded).Select(row => row.WorkingDirectory)];
        Complete(added);
    }

    private async Task RunAsync()
    {
        var token = _cancel.Token;
        try
        {
            var progress = new Progress<ScanProgress>(ShowProgress);
            var result = await Task.Run(() => RepositoryScanner.Scan(_folder, progress, token, _options, _fileSystem), token);
            var opened = await ConfirmAsync(result.Candidates, token);
            if (IsClosed)
            {
                return;
            }

            Show(result, opened);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Cancel or Esc: nothing is added, and the dialog has closed.
        }
        catch (Exception ex)
        {
            _log.Error($"Looking for repositories in {_folder} failed.", ex);
            if (!IsClosed)
            {
                Show(new ScanResult([], 0, false), []);
            }
        }
    }

    /// <summary>
    /// Opens each candidate, <see cref="OpensAtOnce"/> at a time, and keeps those git opens at
    /// their top; one git refuses or passes over (an empty <c>.git</c> inside another repository,
    /// 0.8) is reported by its relative path with a null repository.
    /// </summary>
    private async Task<IReadOnlyList<(string RelativePath, IGitRepository? Repository)>> ConfirmAsync(IReadOnlyList<string> candidates, CancellationToken token)
    {
        using var slots = new SemaphoreSlim(OpensAtOnce);
        var opens = candidates.Select(async candidate =>
        {
            await slots.WaitAsync(token);
            try
            {
                // A slot an open cancelled by Cancel gave back starts no open (1.9: Cancel stops the opens).
                token.ThrowIfCancellationRequested();
                return (Path.GetRelativePath(_folder, candidate), await OpenAtTopAsync(candidate, token));
            }
            finally
            {
                slots.Release();
            }
        }).ToList();
        return await Task.WhenAll(opens);
    }

    private async Task<IGitRepository?> OpenAtTopAsync(string candidate, CancellationToken token)
    {
        try
        {
            var repository = await _provider.OpenAsync(candidate, token);
            return await repository.OpenedAtTopAsync(token) ? repository : null;
        }
        catch (Exception ex) when (ex is NotARepositoryException or GitException or GitUnavailableException)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warning($"Opening {candidate} as a repository failed.", ex);
            return null;
        }
    }

    /// <summary>The done state: the rows, the folder's line, the limit's and the refusals' lines, the button.</summary>
    private void Show(ScanResult result, IReadOnlyList<(string RelativePath, IGitRepository? Repository)> opened)
    {
        var folderName = WorkspaceTexts.FolderName(_folder);
        if (_enclosing is { } enclosing)
        {
            Rows.Add(RowOf(enclosing, WorkspaceTexts.Contains(folderName), isEnclosing: true));
        }

        var kept = new List<(string RelativePath, IGitRepository Repository)>();
        foreach (var (relativePath, repository) in opened.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            // Kept ones with one working directory appear once.
            if (repository is not null && !kept.Any(other => WorkspacePaths.Same(other.Repository.WorkingDirectory, repository.WorkingDirectory)))
            {
                kept.Add((relativePath, repository));
            }
        }

        foreach (var (relativePath, repository) in kept)
        {
            Rows.Add(RowOf(repository, relativePath, isEnclosing: false));
        }

        var failed = opened.Where(entry => entry.Repository is null).Select(entry => entry.RelativePath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FolderText = WorkspaceTexts.FoundIn(kept.Count, _folder, _enclosing is not null);
        StoppedText = result.StoppedAtLimit ? WorkspaceTexts.StoppedAfter(_options.MaxFolders) : null;
        FailedText = failed.Count > 0 ? WorkspaceTexts.CouldNotOpen(failed.Count) : null;
        FailedToolTip = failed.Count > 0 ? WorkspaceTexts.CouldNotOpenToolTip(failed) : null;
        IsSearching = false;
        UpdateConfirm();
    }

    /// <summary>A row: <c>Already in this workspace</c> when the workspace holds it, whatever else it is.</summary>
    private FoundRepositoryRow RowOf(IGitRepository repository, string detail, bool isEnclosing)
    {
        var held = _isInWorkspace(repository.WorkingDirectory);
        return new FoundRepositoryRow(
            repository.Name,
            repository.WorkingDirectory,
            held ? WorkspaceTexts.AlreadyInThisWorkspace : detail,
            isChecked: held || !isEnclosing,
            isEnabled: !held,
            isEnclosing,
            UpdateConfirm);
    }

    private void UpdateConfirm()
    {
        var count = IsSearching ? 0 : Rows.Count(row => row.IsAdded);
        ConfirmText = WorkspaceTexts.AddConfirm(count);
        CanConfirm = count > 0;
    }
}
