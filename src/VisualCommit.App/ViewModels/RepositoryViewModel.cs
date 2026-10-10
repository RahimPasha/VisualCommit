using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// One repository open in a tab (D45): its refs, its graph, its left panel and commit details,
/// its working-tree status with the stage panel and the open diff, its watcher, and everything
/// that keeps them up to date. Every write the app makes to the repository runs here, with the
/// watcher paused (D71). Closing the tab disposes it, which stops all of its work.
/// </summary>
public sealed partial class RepositoryViewModel : ObservableObject, IDisposable, IWorkingChangesHost, IDiffHost
{
    /// <summary>The height of a graph row: the working-changes row moves the rows below it by this much.</summary>
    private const double GraphRowHeight = 26;

    private const string SnapshotNote = "A snapshot is saved first, so the changes can be restored.";

    private readonly CancellationTokenSource _lifetime = new();
    private readonly SynchronizationContext? _ui;
    private readonly IAppLog _log;
    private readonly ISettingsStore _settings;
    private readonly IDialogService? _dialogs;
    private readonly Stopwatch _sinceOpening;
    private readonly List<double> _frameTimes = [];
    private readonly List<double> _loadingFrameTimes = [];
    private IRepositoryWatcher? _watcher;
    private string? _selectedSha;
    private string? _pendingSelection;
    private string? _fingerprint;
    private string? _headSha;
    private readonly Lock _refreshGate = new();
    private readonly Lock _statusGate = new();
    private bool _firstRowsLogged;
    private Task? _refreshing;
    private bool _refreshAgain;
    private Task? _statusRefreshing;
    private bool _statusAgain;
    private bool _restoringSelection;
    private bool _started;
    private bool _changedWhileStarting;
    private bool _disposed;
    private DiscardSnapshot? _lastSnapshot;

    /// <param name="repository">The repository.</param>
    /// <param name="settings">The settings: the file list's flat or tree view, the diff mode.</param>
    /// <param name="dates">How dates are shown.</param>
    /// <param name="log">The app's log, which also gets the Q1 measurements (D50).</param>
    /// <param name="sinceOpening">Started when opening the repository began; the time to the first graph rows is measured from it.</param>
    /// <param name="dialogs">Where a discard asks for confirmation (D62, D69). Without it, discards go ahead unasked: tests only.</param>
    public RepositoryViewModel(
        IGitRepository repository,
        ISettingsStore settings,
        DateDisplay dates,
        IAppLog? log = null,
        Stopwatch? sinceOpening = null,
        IDialogService? dialogs = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dates);
        Repository = repository;
        Dates = dates;
        _settings = settings;
        _dialogs = dialogs;
        _log = log ?? NullAppLog.Instance;
        _sinceOpening = sinceOpening ?? Stopwatch.StartNew();
        _ui = SynchronizationContext.Current;

        LeftPanel = new LeftPanelViewModel();
        LeftPanel.RefActivated += (_, sha) => Select(sha, reveal: true);
        Details = new CommitDetailsViewModel(repository, settings, dates, _log);
        Details.ParentActivated += (_, sha) => Select(sha, reveal: true);
        Details.FileActivated += (_, file) => OpenCommitDiff(file);
        Changes = new WorkingChangesViewModel(this, settings);
    }

    public IGitRepository Repository { get; }

    /// <summary>The name the tab shows.</summary>
    public string Name => Repository.Name;

    /// <summary>The working tree's folder.</summary>
    public string Path => Repository.WorkingDirectory;

    public DateDisplay Dates { get; }

    public LeftPanelViewModel LeftPanel { get; }

    public CommitDetailsViewModel Details { get; }

    /// <summary>The stage panel, shown while the working-changes row is selected.</summary>
    public WorkingChangesViewModel Changes { get; }

    /// <summary>The commits the graph draws. Replaced whole when a refresh loads the history again (D52).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCommits))]
    public partial CommitGraphData Graph { get; private set; } = CommitGraphData.Empty;

    /// <summary>The selected commit's row of the graph, or -1. The graph control binds to it both ways.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>The working-changes row is selected (and <see cref="SelectedIndex"/> is -1). The graph control binds to it both ways.</summary>
    [ObservableProperty]
    public partial bool IsWorkingRowSelected { get; set; }

    /// <summary>The graph's vertical scroll offset, kept here so that each tab keeps its own.</summary>
    [ObservableProperty]
    public partial double ScrollOffset { get; set; }

    /// <summary>What the status bar shows: the branch HEAD is on, or "Detached at" and its short id.</summary>
    [ObservableProperty]
    public partial string BranchText { get; private set; } = string.Empty;

    /// <summary>Why the repository could not be read, or null.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>The staged, unstaged and untracked files, as last read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkingChanges))]
    public partial WorkingTreeStatus Status { get; private set; } = WorkingTreeStatus.Clean;

    /// <summary>The graph shows the working-changes row (D60).</summary>
    public bool HasWorkingChanges => Status.HasChanges;

    /// <summary>The diff open in place of the graph, or null (D63).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiffOpen))]
    public partial DiffViewModel? Diff { get; private set; }

    public bool IsDiffOpen => Diff is not null;

    /// <summary>
    /// The history is loaded and has no commits: the graph area says "No commits yet". Not while
    /// the refs are still being read: the empty graph shown until then is no history yet.
    /// </summary>
    public bool HasNoCommits =>
        !ReferenceEquals(Graph, CommitGraphData.Empty) && Graph.IsComplete && Graph.Count == 0 && ErrorText is null;

    /// <summary>HEAD has a commit.</summary>
    public bool HasHead => _headSha is not null;

    /// <summary>Raised when a row should be scrolled into the middle of the view: after a ref or a parent was activated.</summary>
    public event EventHandler<int>? RevealRequested;

    /// <summary>
    /// Reads the refs and the working-tree status, loads the history page by page into
    /// <see cref="Graph"/> and starts watching for outside changes. Completes when the whole
    /// history is loaded. Failures are shown in <see cref="ErrorText"/> and logged, not thrown.
    /// </summary>
    public async Task StartAsync()
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            // Watching starts first: a change made while the history loads (a commit in a
            // terminal, an editor's save) is caught and shown once the load is done.
            _watcher = Repository.CreateWatcher();
            _watcher.Changed += (_, e) => OnUiThread(() => OnRepositoryChanged(e));
            _watcher.Start();

            // The refs, the history and the status are read side by side: git log does not need
            // the refs, and starting it at once brings the first graph forward by the time they
            // take (Q1). The status is small next to them.
            _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: opened after {_sinceOpening.ElapsedMilliseconds} ms"));
            var reading = Repository.ReadRefsAsync(cancellationToken);
            var status = Repository.ReadStatusAsync(cancellationToken);
            var data = new Lazy<CommitGraphData>(() => new CommitGraphData(reading.Result), LazyThreadSafetyMode.ExecutionAndPublication);
            var loading = LoadAsync(reading, data, cancellationToken);

            RepoRefs refs;
            try
            {
                refs = await reading;
            }
            catch
            {
                // The load fails with the same error; it is observed here so it is not reported twice.
                _ = loading.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                _ = status.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                throw;
            }

            _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: refs read after {_sinceOpening.ElapsedMilliseconds} ms"));
            ApplyRefs(refs);
            Graph = data.Value;
            var statusShown = ShowStatusAsync(status);
            await loading;
            await statusShown;
            OnPropertyChanged(nameof(HasNoCommits));

            _started = true;
            if (_changedWhileStarting)
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tab was closed.
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: the repository could not be read.", ex);
            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
            OnPropertyChanged(nameof(HasNoCommits));
        }
    }

    /// <summary>
    /// Reads the refs again, after an outside change or a commit, and loads the history again when
    /// the refs changed what the graph shows (D52); then reads the working-tree status. The new
    /// graph replaces the old one once it is loaded, with the same commit selected. A call that
    /// comes while a refresh runs makes that refresh run once more and returns its task, so the
    /// caller always waits for a refresh that started after its call.
    /// </summary>
    public Task RefreshAsync()
    {
        lock (_refreshGate)
        {
            if (_refreshing is { IsCompleted: false })
            {
                _refreshAgain = true;
                return _refreshing;
            }

            _refreshing = RunRefreshesAsync();
            return _refreshing;
        }
    }

    /// <summary>
    /// Reads the working-tree status again, and the open diff: after a change in the working tree
    /// or a write of the app's (D71). Calls that come while one runs are folded into one more run,
    /// as <see cref="RefreshAsync"/> does.
    /// </summary>
    public Task RefreshStatusAsync()
    {
        lock (_statusGate)
        {
            if (_statusRefreshing is { IsCompleted: false })
            {
                _statusAgain = true;
                return _statusRefreshing;
            }

            _statusRefreshing = RunStatusRefreshesAsync();
            return _statusRefreshing;
        }
    }

    private async Task RunRefreshesAsync()
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            do
            {
                lock (_refreshGate)
                {
                    _refreshAgain = false;
                }

                var refs = await Repository.ReadRefsAsync(cancellationToken);
                if (refs.Fingerprint == _fingerprint)
                {
                    // Upstreams and their counts can change without moving a ref.
                    ApplyRefs(refs);
                }
                else
                {
                    var data = new CommitGraphData(refs);
                    await LoadAsync(Task.FromResult(refs), new Lazy<CommitGraphData>(data), cancellationToken);
                    ApplyRefs(refs);
                    SwapGraph(data);
                }

                // The index lives in the git folder: a change there may be a change of the status.
                await RefreshStatusAsync();
            }
            while (RefreshRequestedAgain() && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: refreshing after an outside change failed.", ex);
        }
    }

    private bool RefreshRequestedAgain()
    {
        lock (_refreshGate)
        {
            return _refreshAgain;
        }
    }

    private async Task RunStatusRefreshesAsync()
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            bool again;
            do
            {
                lock (_statusGate)
                {
                    _statusAgain = false;
                }

                var status = await Repository.ReadStatusAsync(cancellationToken);
                ApplyStatus(status);
                await ReloadDiffAsync();

                lock (_statusGate)
                {
                    again = _statusAgain;
                }
            }
            while (again && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: reading the working tree's status failed.", ex);
        }
    }

    /// <summary>Selects the commit <paramref name="sha"/> in the graph; with <paramref name="reveal"/>, scrolls it into the middle of the view. An open diff closes.</summary>
    public void Select(string sha, bool reveal)
    {
        CloseDiff();
        var index = Graph.IndexOf(sha);
        if (index < 0)
        {
            // Not loaded yet: select it when it arrives.
            _pendingSelection = Graph.IsComplete ? null : sha;
            return;
        }

        SelectedIndex = index;
        if (reveal)
        {
            RevealRequested?.Invoke(this, index);
        }
    }

    /// <summary>The graph control drew a frame that showed rows: logs the time since opening began, once (D50).</summary>
    public void OnRowsDrawn()
    {
        if (_firstRowsLogged || Graph.Count == 0)
        {
            return;
        }

        _firstRowsLogged = true;
        _log.Info(string.Create(CultureInfo.InvariantCulture, $"{Name}: first graph rows drawn after {_sinceOpening.ElapsedMilliseconds} ms"));
    }

    /// <summary>
    /// The graph control drew a frame in <paramref name="duration"/>: kept for the statistics
    /// logged when the tab closes (D50). D50 judges smoothness over scrolling, so frames drawn
    /// once the history has loaded are counted apart from those drawn while it loads (the first
    /// frame, and one per arriving page, while the loader also keeps the machine busy).
    /// </summary>
    public void OnFrameDrawn(TimeSpan duration) =>
        (Graph.IsComplete ? _frameTimes : _loadingFrameTimes).Add(duration.TotalMilliseconds);

    /// <summary>Opens a working-tree file's diff: unstaged or staged (D63).</summary>
    public void OpenDiff(ChangedFile file, bool staged)
    {
        ArgumentNullException.ThrowIfNull(file);
        var target = new DiffTarget(
            staged ? DiffSide.Staged : DiffSide.Unstaged,
            file.Path,
            file.Kind,
            staged ? file.OldPath : null,
            IsUntracked: !staged && Status.IsUntracked(file.Path));
        ShowDiff(target, staged ? "Staged" : "Unstaged");
        Details.SelectedFilePath = null;
        Changes.ShowSelection(file.Path, staged);
    }

    /// <summary>Closes the diff view and shows the graph again; the file row loses its selection.</summary>
    public void CloseDiff()
    {
        if (Diff is { } diff)
        {
            Diff = null;
            diff.Dispose();
        }

        Changes.ShowSelection(null, staged: false);
        Details.SelectedFilePath = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _watcher?.Dispose();
        Diff?.Dispose();
        LogFrameStatistics();
        _lifetime.Dispose();
    }

    // The stage panel's and the diff view's writes (IWorkingChangesHost, IDiffHost).

    public Task<string?> StageAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files.Count == 0
            ? Task.FromResult<string?>(null)
            : WriteAsync(cancellationToken => Repository.StageAsync([.. files.Select(file => file.Path)], cancellationToken));
    }

    public Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        // A rename unstages both its paths: the new one leaves the index, the old one comes back.
        var paths = files.SelectMany(file => file.OldPath is { } old ? new[] { file.Path, old } : [file.Path]).Distinct().ToList();
        return paths.Count == 0
            ? Task.FromResult<string?>(null)
            : WriteAsync(cancellationToken => Repository.UnstageAsync(paths, cancellationToken));
    }

    public async Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            return null;
        }

        var question = files.Count == 1
            ? $"Discard the changes to {WorkingChangesViewModel.FileName(files[0].Path)}?"
            : string.Create(CultureInfo.InvariantCulture, $"Discard the changes to {files.Count} files?");
        if (!await ConfirmDiscardAsync(question))
        {
            return null;
        }

        var paths = files.Select(file => file.Path).ToList();
        var untracked = new HashSet<string>(paths.Where(Status.IsUntracked), StringComparer.Ordinal);
        return await WriteAsync(
            async cancellationToken =>
            {
                var snapshot = await Repository.SaveSnapshotAsync(paths, cancellationToken);
                _lastSnapshot = snapshot;
                Changes.ShowRestore(paths);
                await Repository.DiscardAsync(paths, untracked, cancellationToken);
            });
    }

    public Task<string?> RestoreAsync()
    {
        if (_lastSnapshot is not { } snapshot)
        {
            return Task.FromResult<string?>(null);
        }

        return WriteAsync(
            async cancellationToken =>
            {
                await Repository.RestoreSnapshotAsync(snapshot, cancellationToken);
                _lastSnapshot = null;
            });
    }

    public async Task<string?> CommitAsync(string message, bool amend)
    {
        string? commit = null;
        var error = await WriteAsync(
            async cancellationToken => commit = await Repository.CommitAsync(message, amend, cancellationToken),
            refsChange: true);

        // With nothing left to commit, the working-changes row is gone: the new commit is selected.
        if (error is null && commit is not null && !HasWorkingChanges)
        {
            Select(commit, reveal: true);
        }

        return error;
    }

    public async Task<(string Subject, string Body)?> ReadHeadMessageAsync()
    {
        if (_headSha is not { } head)
        {
            return null;
        }

        try
        {
            var details = await Repository.ReadCommitDetailsAsync(head, _lifetime.Token);
            return (details.Subject, details.Body);
        }
        catch (Exception ex) when (ex is GitException or OperationCanceledException)
        {
            return null;
        }
    }

    public Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken) =>
        Repository.ReadDiffAsync(target, cancellationToken);

    public Task<byte[]?> ReadFileAsync(FileVersion version, long maxBytes, CancellationToken cancellationToken) =>
        Repository.ReadFileAsync(version, maxBytes, cancellationToken);

    public Task<long?> ReadFileSizeAsync(FileVersion version, CancellationToken cancellationToken) =>
        Repository.ReadFileSizeAsync(version, cancellationToken);

    public async Task<string?> ApplyLinesAsync(DiffTarget target, FileDiff diff, IReadOnlySet<DiffLineRef> lines, LineAction action, bool wholeHunk)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(lines);
        var chosen = lines.Where(line => line.Hunk < diff.Hunks.Count && line.Line < diff.Hunks[line.Hunk].Lines.Count && diff.Hunks[line.Hunk].Lines[line.Line].IsChange).ToHashSet();
        if (chosen.Count == 0)
        {
            return null;
        }

        if (action == LineAction.Discard)
        {
            var name = WorkingChangesViewModel.FileName(target.Path);
            var question = wholeHunk ? $"Discard this hunk of {name}?"
                : chosen.Count == 1 ? $"Discard 1 selected line of {name}?"
                : string.Create(CultureInfo.InvariantCulture, $"Discard {chosen.Count} selected lines of {name}?");
            if (!await ConfirmDiscardAsync(question))
            {
                return null;
            }
        }

        return await WriteAsync(
            async cancellationToken =>
            {
                if (action == LineAction.Discard)
                {
                    // The whole file goes into the snapshot, so Restore puts all of it back (D67).
                    _lastSnapshot = await Repository.SaveSnapshotAsync([target.Path], cancellationToken);
                    Changes.ShowRestore([target.Path]);
                    if (target.IsUntracked)
                    {
                        // An untracked file is all added lines: what stays is written back as it is.
                        await DiscardUntrackedLinesAsync(target, diff, chosen, cancellationToken);
                        return;
                    }
                }

                var direction = action == LineAction.Stage ? PatchDirection.Forward : PatchDirection.Reverse;
                var patch = PatchBuilder.Build(diff, chosen, direction);
                if (patch is not null)
                {
                    await Repository.ApplyPatchAsync(patch, toIndex: action != LineAction.Discard, direction, cancellationToken);
                }
            });
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0)
        {
            IsWorkingRowSelected = false;
        }

        var sha = value >= 0 && value < Graph.Count ? Graph.CommitAt(value).Sha : null;
        if (sha == _selectedSha)
        {
            return;
        }

        _selectedSha = sha;
        if (!_restoringSelection)
        {
            var stash = sha is null ? null : Graph.Refs.Stashes.FirstOrDefault(entry => entry.Sha == sha);
            _ = Details.ShowAsync(sha, stash, _lifetime.Token);
        }
    }

    partial void OnIsWorkingRowSelectedChanged(bool value)
    {
        if (value)
        {
            SelectedIndex = -1;
            Changes.SyncFileList();
        }
    }

    /// <summary>
    /// Shows a newly read status. When the working-changes row appears or goes while the graph is
    /// scrolled away from the top, the offset moves with it so that the rows in view stay put (D60).
    /// </summary>
    private void ApplyStatus(WorkingTreeStatus status)
    {
        var had = HasWorkingChanges;
        Status = status;

        // The real-window pass, which cannot see the graph's rows, waits for this line.
        _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: working tree: {status.Staged.Count} staged, {status.Unstaged.Count} unstaged"));
        if (had != status.HasChanges && ScrollOffset > 0)
        {
            ScrollOffset = Math.Max(0, ScrollOffset + (status.HasChanges ? GraphRowHeight : -GraphRowHeight));
        }

        if (!status.HasChanges && IsWorkingRowSelected)
        {
            IsWorkingRowSelected = false;
            _ = Details.ShowAsync(null, null, _lifetime.Token);
        }

        Changes.Update(status, HasHead);
    }

    private async Task ShowStatusAsync(Task<WorkingTreeStatus> reading)
    {
        try
        {
            ApplyStatus(await reading);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: reading the working tree's status failed.", ex);
        }
    }

    /// <summary>
    /// Reads the open diff again after a write or an outside change, for the file as the status
    /// now lists it. A working-tree diff whose file has left its list closes. A commit's diff
    /// never changes.
    /// </summary>
    private async Task ReloadDiffAsync()
    {
        if (Diff is not { } diff || diff.Target.Side == DiffSide.Commit)
        {
            return;
        }

        var staged = diff.Target.Side == DiffSide.Staged;
        var list = staged ? Status.Staged : Status.Unstaged;
        var file = list.FirstOrDefault(candidate => candidate.Path == diff.Target.Path);
        if (file is null)
        {
            CloseDiff();
            return;
        }

        var target = diff.Target with
        {
            Kind = file.Kind,
            OldPath = staged ? file.OldPath : null,
            IsUntracked = !staged && Status.IsUntracked(file.Path),
        };
        await diff.LoadAsync(target);
    }

    private void OpenCommitDiff(ChangedFile file)
    {
        if (Details.Details is not { } commit)
        {
            return;
        }

        var target = new DiffTarget(DiffSide.Commit, file.Path, file.Kind, file.OldPath, Commit: commit.Sha, Parent: commit.Parents.FirstOrDefault());
        ShowDiff(target, commit.Sha[..Math.Min(7, commit.Sha.Length)]);
        Changes.ShowSelection(null, staged: false);
        Details.SelectedFilePath = file.Path;
    }

    private void ShowDiff(DiffTarget target, string sideLabel)
    {
        Diff?.Dispose();
        var diff = new DiffViewModel(this, target, sideLabel, _settings, _log);
        Diff = diff;
        _ = diff.LoadAsync();
    }

    private async Task<bool> ConfirmDiscardAsync(string question) =>
        _dialogs is null || await _dialogs.ConfirmAsync(new ConfirmationRequest("Discard changes?", question, SnapshotNote, "Discard"));

    /// <summary>
    /// Runs one of the app's own writes with the watcher paused, then reads what changed: the
    /// status (and the open diff), and with <paramref name="refsChange"/> the refs and history
    /// too (D71). Returns git's message when the write failed, and shows it in the stage panel.
    /// </summary>
    private async Task<string?> WriteAsync(Func<CancellationToken, Task> write, bool refsChange = false)
    {
        var cancellationToken = _lifetime.Token;
        string? error = null;
        using (_watcher?.Pause())
        {
            try
            {
                await write(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (GitException ex)
            {
                _log.Warning($"{Name}: {ex.Message}");
                error = string.IsNullOrWhiteSpace(ex.StandardError) ? ex.Message : ex.StandardError.Trim();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warning($"{Name}: a write to the working tree failed.", ex);
                error = ex.Message;
            }
        }

        // Read again whether or not the write succeeded: it may have done part of its work.
        if (refsChange)
        {
            await RefreshAsync();
        }
        else
        {
            await RefreshStatusAsync();
        }

        Changes.ErrorText = error;
        return error;
    }

    /// <summary>Discards chosen lines of an untracked file, which git cannot patch: the file is written again without them.</summary>
    private async Task DiscardUntrackedLinesAsync(DiffTarget target, FileDiff diff, IReadOnlySet<DiffLineRef> chosen, CancellationToken cancellationToken)
    {
        var kept = new StringBuilder();
        var all = true;
        for (var h = 0; h < diff.Hunks.Count; h++)
        {
            var hunkLines = diff.Hunks[h].Lines;
            for (var l = 0; l < hunkLines.Count; l++)
            {
                if (chosen.Contains(new DiffLineRef(h, l)))
                {
                    continue;
                }

                all = false;
                kept.Append(hunkLines[l].Raw);
                if (!hunkLines[l].NoNewlineAtEnd)
                {
                    kept.Append('\n');
                }
            }
        }

        if (all)
        {
            await Repository.DiscardAsync([target.Path], new HashSet<string>(StringComparer.Ordinal) { target.Path }, cancellationToken);
            return;
        }

        var path = System.IO.Path.Combine(Repository.WorkingDirectory, target.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        await File.WriteAllBytesAsync(path, Encoding.Latin1.GetBytes(kept.ToString()), cancellationToken);
    }

    private void OnRepositoryChanged(RepositoryChangedEventArgs change)
    {
        if (_disposed)
        {
            return;
        }

        if (!_started)
        {
            _changedWhileStarting = true;
            return;
        }

        _ = change.GitFolder ? RefreshAsync() : RefreshStatusAsync();
    }

    /// <summary>
    /// Loads the history into <paramref name="lazyData"/>, which is made from <paramref name="refs"/>
    /// once they are known. Git starts at once; no page arrives before the refs (the contract of
    /// <see cref="IGitRepository.LoadCommitsAsync(Task{RepoRefs}, Action{IReadOnlyList{CommitInfo}}, CancellationToken)"/>).
    /// </summary>
    private async Task LoadAsync(Task<RepoRefs> refs, Lazy<CommitGraphData> lazyData, CancellationToken cancellationToken)
    {
        var layout = new GraphLayout();
        var stopwatch = Stopwatch.StartNew();
        await Repository.LoadCommitsAsync(
            refs,
            page =>
            {
                var data = lazyData.Value;
                if (layout.RowCount == 0)
                {
                    _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: first commits from git after {_sinceOpening.ElapsedMilliseconds} ms"));
                }

                // On a background thread: lay the page out here, add it on the UI thread.
                var rows = new GraphRow[page.Count];
                for (var i = 0; i < page.Count; i++)
                {
                    rows[i] = layout.Add(page[i]);
                }

                OnUiThread(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        data.Append(page, rows);
                        ApplyPendingSelection(data);
                    }
                });
            },
            cancellationToken);

        // The pages were posted in order; completing through the same queue keeps it last.
        var data = lazyData.Value;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OnUiThread(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                data.Complete();
                ApplyPendingSelection(data);
            }

            completed.TrySetResult();
        });
        await completed.Task;
        cancellationToken.ThrowIfCancellationRequested();

        _log.Info(string.Create(CultureInfo.InvariantCulture, $"{Name}: loaded {data.Count} commits in {stopwatch.ElapsedMilliseconds} ms"));
    }

    private void ApplyRefs(RepoRefs refs)
    {
        _fingerprint = refs.Fingerprint;
        _headSha = refs.Head.Sha;
        BranchText = refs.Head switch
        {
            { BranchName: { } branch } => branch,
            { Sha: { } sha } => $"Detached at {sha[..Math.Min(7, sha.Length)]}",
            _ => string.Empty,
        };
        LeftPanel.Update(refs);
    }

    private void SwapGraph(CommitGraphData data)
    {
        var selected = _selectedSha;
        _restoringSelection = true;
        try
        {
            Graph = data;
            SelectedIndex = selected is null ? -1 : data.IndexOf(selected);
        }
        finally
        {
            _restoringSelection = false;
        }

        if (SelectedIndex < 0 && selected is not null)
        {
            // The selected commit is gone (a reset, a deleted branch, an amend): show the placeholder.
            _selectedSha = null;
            _ = Details.ShowAsync(null, null, _lifetime.Token);
        }
        else if (selected is not null && data.Refs.Stashes.FirstOrDefault(entry => entry.Sha == selected) is { } stash)
        {
            // A selected stash keeps its commit when stashes are pushed or dropped, but not its
            // name: stash@{0} becomes stash@{1}. Show it again under its new name.
            _ = Details.ShowAsync(selected, stash, _lifetime.Token);
        }
    }

    private void ApplyPendingSelection(CommitGraphData data)
    {
        if (_pendingSelection is null || !ReferenceEquals(data, Graph))
        {
            return;
        }

        var sha = _pendingSelection;
        if (data.IndexOf(sha) >= 0 || data.IsComplete)
        {
            _pendingSelection = null;
            Select(sha, reveal: true);
        }
    }

    private void OnUiThread(Action action)
    {
        if (_ui is null)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }

    private void LogFrameStatistics()
    {
        Log("graph frames while loading", _loadingFrameTimes);
        Log("graph frames", _frameTimes);

        void Log(string what, List<double> times)
        {
            if (times.Count == 0)
            {
                return;
            }

            var sorted = times.Order().ToList();
            var p95 = sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
            _log.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"{Name}: {what}: {sorted.Count} drawn, 95th percentile {p95:F1} ms, longest {sorted[^1]:F1} ms"));
        }
    }
}
