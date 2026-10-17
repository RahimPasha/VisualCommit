using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Pages;

/// <summary>
/// The file history page (docs/phase-5-design.md 4.1, D158, D175): the commits that touched a
/// file, newest first, following renames, from a start resolved to a commit when the page opens
/// and fixed from then on; the rows come as git prints them. The selected row's change shows in
/// the diff part, a <see cref="DiffViewModel"/> of its own that has no write action and no close
/// button, reused for every row. Only the user's click or key on a row shows that commit in the
/// right panel and selects it behind the page (11.3): the first row, selected when it arrives,
/// fills only the diff part.
/// </summary>
public sealed partial class FileHistoryViewModel : GraphAreaPageViewModel
{
    /// <summary>From this page width the list and the diff lie side by side, 45 % and 55 % (BD2); below it the list lies above the diff.</summary>
    public const double SideBySideWidth = 760;

    /// <summary>A row's secondary text shows from this list width (BD2), the author from <see cref="AuthorFromWidth"/>, the date from <see cref="DateFromWidth"/>.</summary>
    public const double SecondaryFromWidth = 402;

    /// <inheritdoc cref="SecondaryFromWidth"/>
    public const double AuthorFromWidth = 520;

    /// <inheritdoc cref="SecondaryFromWidth"/>
    public const double DateFromWidth = 628;

    private readonly IFileHistoryHost _host;
    private bool _selectingQuietly;

    /// <summary>The rows git has printed that the list does not show yet: added in batches on the UI thread (<see cref="Drain"/>).</summary>
    private readonly ConcurrentQueue<FileHistoryRow> _pending = new();

    /// <summary>1 while a <see cref="Drain"/> is posted and has not started.</summary>
    private int _drainPosted;

    public FileHistoryViewModel(IFileHistoryHost host, FileHistoryRequest request)
        : base(host)
    {
        ArgumentNullException.ThrowIfNull(request);
        _host = host;
        Request = request;
        FileName = WorkingChangesViewModel.FileName(request.Path);
        Folder = request.Path.LastIndexOf('/') is var slash and > 0 ? request.Path[..slash] : string.Empty;
        Title = $"History of {FileName}";
    }

    /// <summary>What the page shows: the path and where its history starts.</summary>
    public FileHistoryRequest Request { get; }

    /// <summary>The file's name, the header's <c>History of {name}</c>.</summary>
    public string FileName { get; }

    /// <summary>The file's folder, in the secondary colour after the title; empty at the root.</summary>
    public string Folder { get; }

    public bool HasFolder => Folder.Length > 0;

    /// <summary>The commit the history starts from, a full id, once the page has resolved it.</summary>
    public string? StartSha { get; private set; }

    /// <summary>
    /// <c>from {short}</c> when the history starts at a commit other than HEAD's (BD24: compared by
    /// full id, so a history opened from HEAD's own commit has none); null otherwise.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFrom))]
    public partial string? FromText { get; private set; }

    public bool HasFrom => FromText is not null;

    /// <summary>The rows, newest first, as git hands them over, added in batches.</summary>
    public RowCollection<FileHistoryRowViewModel> Rows { get; } = [];

    /// <summary>No row has come yet and git has not ended: the list's place says <c>Loading…</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading))]
    public partial bool IsLoading { get; private set; } = true;

    public bool ShowsLoading => IsLoading && Rows.Count == 0;

    /// <summary><c>No commit has touched {name} yet</c> when git listed none, or git's message when it failed; null while there are rows or they are loading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    /// <summary>The message is git's failure: drawn in the error style.</summary>
    [ObservableProperty]
    public partial bool IsError { get; private set; }

    /// <summary>The selected row, whose change the diff part shows. The list binds to it both ways: a change from the list is the user's.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BlameCommand))]
    public partial FileHistoryRowViewModel? SelectedRow { get; set; }

    /// <summary>The diff part (<c>FileHistoryDiff</c>): null until a row is selected.</summary>
    [ObservableProperty]
    public partial DiffViewModel? Diff { get; private set; }

    /// <summary>The page is at least <see cref="SideBySideWidth"/> wide: the view lays the list and the diff side by side. The view sets it.</summary>
    [ObservableProperty]
    public partial bool IsSideBySide { get; set; }

    /// <summary>The list's width, which decides the rows' columns (BD2). The view sets it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSecondary), nameof(ShowsAuthor), nameof(ShowsDate))]
    public partial double ListWidth { get; set; }

    public bool ShowsSecondary => ListWidth >= SecondaryFromWidth;

    public bool ShowsAuthor => ListWidth >= AuthorFromWidth;

    public bool ShowsDate => ListWidth >= DateFromWidth;

    /// <summary>
    /// Reads the history (4.1): resolves the start to a full id, then lists the rows as git prints
    /// them, the first one selected (filling the diff part only). Closing the page cancels the read.
    /// The rows reach the list in batches, one UI job and one change of the list each, whatever
    /// has come since the last (step 25's review): one job per row kept the window from taking
    /// input until a file with thousands of commits had all its rows.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            var start = IsFullId(Request.Start) ? Request.Start : await Host.Repository.ResolveAsync(Request.Start, Lifetime);
            if (IsClosed)
            {
                return;
            }

            if (start is null)
            {
                Finish(NoCommitText(), isError: false);
                return;
            }

            StartSha = start;
            FromText = string.Equals(start, Host.MenuContext.Head.Sha, StringComparison.Ordinal) ? null : $"from {Short(start)}";
            await Host.Repository.ReadFileHistoryAsync(start, Request.Path, Enqueue, Lifetime);
            OnUiThread(() =>
            {
                Drain();
                Finish(Rows.Count == 0 ? NoCommitText() : null, isError: false);
            });
        }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Host.Log.Error($"The history of {Request.Path} could not be read.", ex);
            var reason = ex is GitException { StandardError: { } error } && !string.IsNullOrWhiteSpace(error) ? error.Trim() : ex.Message;
            OnUiThread(() =>
            {
                Drain();
                Finish(Rows.Count == 0 ? reason : null, isError: true);
            });
        }
    }

    /// <summary>"Show in graph" and a double-click on a row: the page closes and the graph selects and reveals the commit; nothing for a commit the graph does not have (4.3).</summary>
    public void ShowInGraph(FileHistoryRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!IsClosed && Host.HasCommit(row.Sha))
        {
            Host.ShowInGraph(row.Sha);
        }
    }

    /// <summary>A row's menu (<see cref="RepositoryMenus.ForFileHistoryRow"/>), with whether the graph has its commit now.</summary>
    public MenuModel MenuFor(FileHistoryRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return RepositoryMenus.ForFileHistoryRow(Host.MenuContext, row.Row, Host.HasCommit(row.Sha), Host.Actions);
    }

    /// <summary>The header's <c>Blame</c> (<c>FileHistoryBlame</c>): the blame of the selected row's version, in place of this page.</summary>
    [RelayCommand(CanExecute = nameof(CanBlame))]
    private void Blame()
    {
        if (SelectedRow is { } row && RepositoryMenus.BlameOfHistoryRow(row.Row) is { } request)
        {
            _host.OpenBlame(request);
        }
    }

    private bool CanBlame() => SelectedRow is not null;

    partial void OnSelectedRowChanged(FileHistoryRowViewModel? value)
    {
        if (value is null || IsClosed)
        {
            return;
        }

        ShowChange(value);
        if (!_selectingQuietly)
        {
            // The user's click or key (4.1, 11.3): the right panel shows the commit, selected behind the page.
            Host.ShowCommit(value.Sha);
        }
    }

    protected override void OnDisposed() => Diff?.Dispose();

    /// <summary>A row git printed, on the reader's thread: queued, and a <see cref="Drain"/> posted unless one is waiting already.</summary>
    private void Enqueue(FileHistoryRow row)
    {
        _pending.Enqueue(row);
        if (Interlocked.Exchange(ref _drainPosted, 1) == 0)
        {
            OnUiThread(Drain);
        }
    }

    /// <summary>
    /// On the UI thread: every row queued since the last drain, added to the list with one change
    /// (<see cref="RowCollection{T}.AddRange"/>); the first row of the history selected quietly,
    /// as soon as it comes. A row queued while this runs posts the next drain.
    /// </summary>
    private void Drain()
    {
        Volatile.Write(ref _drainPosted, 0);
        if (IsClosed)
        {
            return;
        }

        var added = new List<FileHistoryRowViewModel>();
        while (_pending.TryDequeue(out var row))
        {
            added.Add(new FileHistoryRowViewModel(row, Request.Path, Host.Dates.Format(row.Commit.AuthorDate)));
        }

        if (added.Count == 0)
        {
            return;
        }

        var first = Rows.Count == 0;
        Rows.AddRange(added);
        OnPropertyChanged(nameof(ShowsLoading));
        if (first)
        {
            _selectingQuietly = true;
            try
            {
                SelectedRow = added[0];
            }
            finally
            {
                _selectingQuietly = false;
            }

            RequestFocus();
        }
    }

    /// <summary>Loads the row's change at its commit against its first parent, with the paths at that commit (0.1), into the diff part.</summary>
    private void ShowChange(FileHistoryRowViewModel row)
    {
        var commit = row.Row.Commit;
        var target = new DiffTarget(DiffSide.Commit, row.Row.Path, row.Row.Status, row.Row.OldPath, Commit: commit.Sha, Parent: commit.Parents.FirstOrDefault());
        if (Diff is { } diff)
        {
            _ = diff.LoadAsync(target, row.ShortSha);
            return;
        }

        var created = new DiffViewModel(new PageDiffHost(Host), target, row.ShortSha, Host.Settings, Host.Log) { ShowsClose = false };
        Diff = created;
        _ = created.LoadAsync();
    }

    private void Finish(string? message, bool isError)
    {
        if (IsClosed)
        {
            return;
        }

        IsLoading = false;
        IsError = message is not null && isError;
        Message = message;
        OnPropertyChanged(nameof(ShowsLoading));
    }

    private string NoCommitText() => $"No commit has touched {FileName} yet";

    private static bool IsFullId(string text) => text.Length == 40 && text.All(char.IsAsciiHexDigitLower);
}

/// <summary>
/// One row of the file history (4.1, BD2, BD13): the status letter in the changed-file list's
/// colours, the short SHA, the subject, the secondary text, the author and the date.
/// </summary>
public sealed class FileHistoryRowViewModel
{
    public FileHistoryRowViewModel(FileHistoryRow row, string pagePath, string dateText)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(pagePath);
        Row = row;
        DateText = dateText;
        SecondaryText = row switch
        {
            { Status: FileChangeKind.Renamed, OldPath: { } old } => $"renamed from {old}",
            { Status: FileChangeKind.Copied, OldPath: { } old } => $"copied from {old}",
            { Status: FileChangeKind.Added } => "added",
            { Status: FileChangeKind.Deleted } => "deleted",
            _ when !string.Equals(row.Path, pagePath, StringComparison.Ordinal) => $"as {row.Path}",
            _ => string.Empty,
        };
    }

    /// <summary>The commit and the file's change in it, as git listed it.</summary>
    public FileHistoryRow Row { get; }

    public string Sha => Row.Commit.Sha;

    public string ShortSha => Row.Commit.ShortSha;

    public string Subject => Row.Commit.Subject;

    public string Author => Row.Commit.AuthorName;

    /// <summary>The author date as the graph's Date column shows it (D43).</summary>
    public string DateText { get; }

    /// <summary>The row's automation name: <c>{short} {subject}</c>.</summary>
    public string Name => $"{ShortSha} {Subject}";

    /// <summary>BD13: <c>renamed from {old}</c>, <c>copied from {old}</c>, <c>added</c> or <c>deleted</c>, else <c>as {path}</c> when the path differs from the page's; empty otherwise.</summary>
    public string SecondaryText { get; }

    public bool HasSecondary => SecondaryText.Length > 0;

    public string StatusLetter => Row.Status switch
    {
        FileChangeKind.Added => "A",
        FileChangeKind.Modified => "M",
        FileChangeKind.Deleted => "D",
        FileChangeKind.Renamed => "R",
        FileChangeKind.Copied => "C",
        FileChangeKind.TypeChanged => "T",
        _ => "?",
    };

    /// <summary>The letter is drawn in the success colour.</summary>
    public bool IsAdded => Row.Status == FileChangeKind.Added;

    /// <summary>The letter is drawn in the warning colour.</summary>
    public bool IsModified => Row.Status == FileChangeKind.Modified;

    /// <summary>The letter is drawn in the danger colour.</summary>
    public bool IsDeleted => Row.Status == FileChangeKind.Deleted;

    /// <summary>The letter is drawn in the accent colour.</summary>
    public bool IsRenamedOrCopied => Row.Status is FileChangeKind.Renamed or FileChangeKind.Copied;
}

/// <summary>
/// What a page's diff reads through (4.1): the repository's reads, outside the queue; it writes
/// nothing (a commit's diff offers no write action) and closes with its page.
/// </summary>
internal sealed class PageDiffHost(IGraphAreaPageHost host) : IDiffHost
{
    private const string NoWrites = "A page's diff changes nothing.";

    public Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken) =>
        host.Repository.ReadDiffAsync(target, cancellationToken);

    public Task<byte[]?> ReadFileAsync(FileVersion version, long maxBytes, CancellationToken cancellationToken) =>
        host.Repository.ReadFileAsync(version, maxBytes, cancellationToken);

    public Task<long?> ReadFileSizeAsync(FileVersion version, CancellationToken cancellationToken) =>
        host.Repository.ReadFileSizeAsync(version, cancellationToken);

    public Task<string?> StageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(NoWrites);

    public Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(NoWrites);

    public Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(NoWrites);

    public Task<string?> ApplyLinesAsync(DiffTarget target, FileDiff diff, IReadOnlySet<DiffLineRef> lines, LineAction action, bool wholeHunk) =>
        Task.FromResult<string?>(NoWrites);

    public void CloseDiff()
    {
        // The page's diff has no close button: it closes with the page.
    }
}
