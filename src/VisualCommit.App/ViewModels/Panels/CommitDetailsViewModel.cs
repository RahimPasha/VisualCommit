using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// The right panel of one repository tab: the details of the selected commit, as "Commit
/// details" in docs/test-reports/phase-1.md describes them, or phase 0's placeholder when
/// nothing is selected. A tab without a repository uses an instance without one.
/// </summary>
public partial class CommitDetailsViewModel : ObservableObject
{
    private readonly IGitRepository? _repository;
    private readonly ISettingsStore _settings;
    private readonly DateDisplay _dates;
    private readonly IAppLog _log;
    /// <summary>
    /// Guards <see cref="_loading"/> and <see cref="_version"/>. A load's continuation runs on
    /// the UI thread in the app, but on any thread where there is none (view-model tests), so a
    /// newer call and an older load can meet on two threads.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>Cancels the load still running, if any.</summary>
    private CancellationTokenSource? _loading;

    /// <summary>Counts the calls of <see cref="ShowAsync"/>; a load whose number is not the latest shows nothing.</summary>
    private int _version;
    private readonly ChangedFileList _fileList = new();

    public CommitDetailsViewModel(IGitRepository? repository, ISettingsStore settings, DateDisplay dates, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dates);
        _repository = repository;
        _settings = settings;
        _dates = dates;
        _log = log ?? NullAppLog.Instance;
        FileList = settings.Current.FileList;
        _fileList.SetMode(FileList);
    }

    /// <summary>A commit's details are shown; false shows the placeholder.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPlaceholder))]
    public partial bool HasCommit { get; private set; }

    /// <summary>Why the details of the selected commit could not be read, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowsPlaceholder))]
    public partial string? ErrorText { get; private set; }

    public bool HasError => ErrorText is not null;

    /// <summary>Neither details nor an error: the panel says "Select a commit to see its details.".</summary>
    public bool ShowsPlaceholder => !HasCommit && !HasError;

    /// <summary>A commit's details are being read. The panel keeps showing the previous ones until they arrive.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>The full id of the commit shown, or null.</summary>
    [ObservableProperty]
    public partial string? Sha { get; private set; }

    /// <summary>The first line of the message; for a stash, its message.</summary>
    [ObservableProperty]
    public partial string Subject { get; private set; } = string.Empty;

    /// <summary>The rest of the message, without trailing blank lines; empty when there is none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBody))]
    public partial string Body { get; private set; } = string.Empty;

    public bool HasBody => Body.Length > 0;

    /// <summary>The author as "name &lt;e-mail&gt;".</summary>
    [ObservableProperty]
    public partial string AuthorText { get; private set; } = string.Empty;

    /// <summary>The author date, as the graph's Date column shows dates (D43).</summary>
    [ObservableProperty]
    public partial string DateText { get; private set; } = string.Empty;

    /// <summary>The committer as "name &lt;e-mail&gt;", or empty when it reads the same as the author.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommitter))]
    public partial string CommitterText { get; private set; } = string.Empty;

    public bool HasCommitter => CommitterText.Length > 0;

    /// <summary>The commit date, or empty when it reads the same as the author date.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommitDate))]
    public partial string CommitDateText { get; private set; } = string.Empty;

    public bool HasCommitDate => CommitDateText.Length > 0;

    /// <summary>The parents as links; for a stash only the commit it was made on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParents))]
    public partial IReadOnlyList<ParentLink> Parents { get; private set; } = [];

    public bool HasParents => Parents.Count > 0;

    /// <summary>For a stash, its name (<c>stash@{0}</c>); otherwise empty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStash))]
    public partial string StashName { get; private set; } = string.Empty;

    public bool IsStash => StashName.Length > 0;

    /// <summary>"Changed files (N)".</summary>
    [ObservableProperty]
    public partial string ChangedFilesTitle { get; private set; } = string.Empty;

    /// <summary>Flat or tree, from the settings (D44). Changed with <see cref="ShowFlatCommand"/> and <see cref="ShowTreeCommand"/>, which save it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFlat), nameof(IsTree))]
    public partial FileListMode FileList { get; private set; }

    public bool IsFlat => FileList == FileListMode.Flat;

    public bool IsTree => FileList == FileListMode.Tree;
    /// <summary>The rows of the changed-file list, flat or as a tree.</summary>
    public RowCollection<ChangedFileRow> Files => _fileList.Rows;

    /// <summary>The commit shown: its id and first parent, for the diff of one of its files. Null without a commit.</summary>
    public CommitDetails? Details { get; private set; }

    /// <summary>The path of the file whose diff is open, whose row has the selection background; null for none.</summary>
    public string? SelectedFilePath
    {
        get => _fileList.SelectedPath;
        set => _fileList.SelectedPath = value;
    }

    /// <summary>Raised when the user clicks a parent's id: the parent's full id, which the graph then selects.</summary>
    public event EventHandler<string>? ParentActivated;

    /// <summary>A file of the list was clicked: its diff should open (D63).</summary>
    public event EventHandler<ChangedFile>? FileActivated;

    /// <summary>Opens the diff of a file row's file; does nothing for a folder.</summary>
    [RelayCommand]
    private void ActivateFile(ChangedFileRow? row)
    {
        if (row?.File is { } file)
        {
            FileActivated?.Invoke(this, file);
        }
    }

    /// <summary>
    /// Shows the details of <paramref name="sha"/>, or the placeholder when it is null. For a
    /// stash, <paramref name="stash"/> is its entry: the panel then shows its name and, as its
    /// only parent, the commit it was made on. A call that comes while an earlier one is still
    /// loading cancels the earlier one, so the panel always ends up showing the last selection.
    /// Failures are logged and shown in the panel; they do not throw.
    /// </summary>
    public async Task ShowAsync(string? sha, StashEntry? stash = null, CancellationToken cancellationToken = default)
    {
        // Whoever takes a load's source out of _loading, under the lock, cancels and disposes
        // it: a newer call here, or the load itself in its finally block when nothing replaced it.
        CancellationTokenSource? earlier;
        CancellationTokenSource? loading = null;
        int version;
        lock (_gate)
        {
            earlier = _loading;
            version = ++_version;
            if (sha is not null && _repository is not null)
            {
                loading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            _loading = loading;
        }

        // Outside the lock: cancelling can run the earlier load's continuation right here.
        if (earlier is not null)
        {
            earlier.Cancel();
            earlier.Dispose();
        }

        if (loading is null || sha is null || _repository is null)
        {
            Clear();
            return;
        }

        // Another tab may have switched between flat and tree since this one last showed a commit.
        FileList = _settings.Current.FileList;
        _fileList.SetMode(FileList);
        IsLoading = true;
        try
        {
            var details = await _repository.ReadCommitDetailsAsync(sha, loading.Token);
            if (IsCurrent(version))
            {
                Apply(details, stash);
            }
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
            // A newer selection replaced this one, or the tab was closed.
        }
        catch (Exception ex)
        {
            if (IsCurrent(version))
            {
                _log.Error($"The details of commit {sha} could not be read.", ex);
                Clear();
                var reason = ex is GitException { StandardError: { } error } && !string.IsNullOrWhiteSpace(error)
                    ? error.Trim()
                    : ex.Message;
                ErrorText = $"The details of this commit could not be read. {reason}";
            }
        }
        finally
        {
            bool owned;
            lock (_gate)
            {
                owned = ReferenceEquals(_loading, loading);
                if (owned)
                {
                    _loading = null;
                }
            }

            // Not owned: a newer call has replaced this load and cleans up after it.
            if (owned)
            {
                loading.Dispose();
                IsLoading = false;
            }
        }
    }

    /// <summary>Raises <see cref="ParentActivated"/>.</summary>
    protected void OnParentActivated(string sha) => ParentActivated?.Invoke(this, sha);

    /// <summary>Lists the changed files with their folders, and saves the choice.</summary>
    [RelayCommand]
    private void ShowFlat() => SetFileList(FileListMode.Flat);

    /// <summary>Lists the changed files as a tree of folders, and saves the choice.</summary>
    [RelayCommand]
    private void ShowTree() => SetFileList(FileListMode.Tree);

    private void SetFileList(FileListMode mode)
    {
        if (FileList != mode)
        {
            FileList = mode;
            _settings.Update(settings => settings with { FileList = mode });
            _fileList.SetMode(mode);
        }
    }

    private bool IsCurrent(int version)
    {
        lock (_gate)
        {
            return version == _version;
        }
    }

    private void Apply(CommitDetails details, StashEntry? stash)
    {
        Sha = details.Sha;
        Subject = details.Subject;
        Body = details.Body;
        AuthorText = Person(details.AuthorName, details.AuthorEmail);
        DateText = _dates.Format(details.AuthorDate);
        var committer = Person(details.CommitterName, details.CommitterEmail);
        CommitterText = committer == AuthorText ? string.Empty : committer;
        var commitDate = _dates.Format(details.CommitDate);
        CommitDateText = commitDate == DateText ? string.Empty : commitDate;

        // A stash's other parents are git's own index and untracked-files commits, which the
        // graph does not show (D47): only the commit it was made on is a parent the user knows.
        IEnumerable<string> parents = stash is null ? details.Parents : [stash.BaseSha];
        Parents = parents.Select(parent => new ParentLink(parent, OnParentActivated)).ToList();
        StashName = stash?.Name ?? string.Empty;

        Details = details;
        _fileList.SelectedPath = null;
        _fileList.Show(details.Files);
        ChangedFilesTitle = string.Create(CultureInfo.InvariantCulture, $"Changed files ({details.Files.Count})");

        ErrorText = null;
        HasCommit = true;
    }

    private void Clear()
    {
        HasCommit = false;
        ErrorText = null;
        IsLoading = false;
        Sha = null;
        Subject = string.Empty;
        Body = string.Empty;
        AuthorText = string.Empty;
        DateText = string.Empty;
        CommitterText = string.Empty;
        CommitDateText = string.Empty;
        Parents = [];
        StashName = string.Empty;
        ChangedFilesTitle = string.Empty;
        Details = null;
        _fileList.SelectedPath = null;
        _fileList.Show([]);
    }

    private static string Person(string name, string email) => $"{name} <{email}>";
}
