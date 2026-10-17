using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>The worktree whose log the panel shows (docs/phase-5-design.md 2.1, 2.3).</summary>
/// <param name="Key">The worktree's key (<see cref="WorktreeKey"/>): its own git folder, as the operation records name it.</param>
/// <param name="WorkingTree">The folder of its working tree, as git reports it: a read made in it, or in a folder under it, is its read.</param>
/// <param name="Name">Its repository's name, which the panel's header shows.</param>
/// <param name="OpenedFolder">
/// The folder its repository was opened from (<see cref="IGitRepository.OpenedFolder"/>): a read made
/// in it, or in a folder under it, is its read too. The opening's first read runs there, and when
/// the folder was reached through a symbolic link or a junction, git reports the working tree by
/// another path. It is <paramref name="WorkingTree"/> itself for a repository opened from there;
/// null adds no folder.
/// </param>
public sealed record ActivityLogSource(string Key, string WorkingTree, string Name, string? OpenedFolder = null)
{
    /// <summary>The worktree of an open repository.</summary>
    public static ActivityLogSource Of(RepositoryViewModel repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new ActivityLogSource(WorktreeKey.Of(repository.Repository.GitDirectory), repository.Path, repository.Name, repository.Repository.OpenedFolder);
    }

    /// <summary>The folders whose reads are this worktree's: its working tree, and the folder it was opened from when that is another.</summary>
    internal IEnumerable<string> Folders =>
        OpenedFolder is null ? [WorkingTree] : [WorkingTree, OpenedFolder];
}

/// <summary>
/// One worktree's log as the panel shows it (docs/phase-5-design.md 2.1, 2.2): its rows, and
/// what the user left open and where the list was scrolled, which the panel keeps for it while
/// another tab is shown.
/// </summary>
public sealed class ActivityLogWorktree
{
    internal ActivityLogWorktree(ActivityLogSource source)
    {
        Source = source;
        AddFolders(source);
    }

    public ActivityLogSource Source { get; internal set; }

    /// <summary>
    /// The folders whose reads are this worktree's (2.3): those of every source it was shown for,
    /// so that two tabs that opened it from different folders, or a working tree that moved, give
    /// each read made in one of them to it whichever tab is shown. Kept in full, without a trailing
    /// separator.
    /// </summary>
    internal List<string> Folders { get; } = [];

    /// <summary>Adds <paramref name="source"/>'s folders to <see cref="Folders"/>; true when one was new.</summary>
    internal bool AddFolders(ActivityLogSource source)
    {
        var added = false;
        foreach (var folder in source.Folders)
        {
            if (ActivityLogViewModel.Normalized(folder) is { } full && !Folders.Contains(full, WorktreeKey.Comparer))
            {
                Folders.Add(full);
                added = true;
            }
        }

        return added;
    }

    /// <summary>The list's rows, top to bottom: the operations and, with "Show reads", the reads, in the order they started; under an opened row what it opens to.</summary>
    public ObservableCollection<ActivityLogRow> Rows { get; } = [];

    /// <summary>The list was scrolled to its end when it was last left: it shows its end again, and stays there while rows come (2.2, AD15).</summary>
    public bool IsScrolledToEnd { get; set; } = true;

    /// <summary>Where the list was scrolled when it was last left, while not at its end.</summary>
    public double ScrollOffset { get; set; }

    /// <summary>The operations' rows, by id: they keep whether they are open.</summary>
    internal Dictionary<string, ActivityLogOperationRow> OperationRows { get; } = new(StringComparer.Ordinal);

    /// <summary>The calls' rows, by record: they keep whether they are open.</summary>
    internal Dictionary<GitCallRecord, ActivityLogCallRow> CallRows { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The lines under each operation, made again when its record changes.</summary>
    internal Dictionary<string, (OperationRecord Record, bool Dropped, IReadOnlyList<ActivityLogLineRow> Lines)> Lines { get; } = new(StringComparer.Ordinal);

    /// <summary>The rows changed while another worktree was shown: they are made again when this one shows.</summary>
    internal bool IsStale { get; set; } = true;
}

/// <summary>
/// The activity log (docs/phase-5-design.md 2, D156, D173): a panel under the graph area that
/// shows, for the active tab's worktree, every operation of the app session in the order it
/// started, each opening to its notes, its failure's summary and its git calls, each call to what
/// it was given and printed; with "Show reads", the reads of that worktree too. It reads the
/// session's structured records (<see cref="IOperationLog"/>, <see cref="IGitCallLog"/>), never
/// the log file, from the start of the session, whether the panel is open or not.
/// <para>
/// The panel's being open is the window's, kept while a tab without a repository hides it; which
/// rows are open and where the list was scrolled are each worktree's
/// (<see cref="ActivityLogWorktree"/>). The records arrive on any thread: they are taken over on
/// the thread the view model was made on, in batches, and the rows change only where they must.
/// </para>
/// </summary>
public sealed partial class ActivityLogViewModel : ObservableObject, IDisposable
{
    /// <summary>The panel's height until the user drags its splitter (2.1).</summary>
    public const double DefaultHeight = 200;

    /// <summary>The panel's least height while the window has room for it (2.1).</summary>
    public const double MinimumHeight = 120;

    /// <summary>What the graph area's content above the splitter keeps at least (2.1).</summary>
    public const double ContentMinimumHeight = 160;

    /// <summary>The splitter's grab area between the content and the panel (2.1).</summary>
    public const double SplitterHeight = 5;

    /// <summary>The panel's header (2.2): all that shows of it when the window is too low for more.</summary>
    public const double HeaderHeight = 30;

    /// <summary>The status bar's toggle while the panel is hidden (1.2).</summary>
    public const string ShowText = "Show every git command the app has run";

    /// <summary>The status bar's toggle while the panel shows (1.2).</summary>
    public const string HideText = "Hide the activity log";

    /// <summary>A running operation's message (2.2).</summary>
    public const string RunningText = "Running…";

    /// <summary>Under an operation whose calls the memory budget dropped (2.2, 2.3).</summary>
    public const string NoLongerKeptText = "Its git commands are no longer kept.";

    /// <summary>An output box whose parts are all empty (2.2).</summary>
    public const string NoOutputText = "No output";

    /// <summary>After the command of a call made on a temporary index (2.2).</summary>
    public const string TemporaryIndexText = "(on a temporary index)";

    private readonly IOperationLog _operations;
    private readonly IGitCallLog? _calls;
    private readonly ISessionStore _session;
    private readonly DateDisplay _dates;
    private readonly IAppLog _log;
    private readonly SynchronizationContext? _ui;
    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly Dictionary<string, OperationEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GitCallRecord>> _callsByOperation = new(StringComparer.Ordinal);
    private readonly List<GitCallRecord> _reads = [];
    private readonly HashSet<GitCallRecord> _knownCalls = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, ActivityLogWorktree> _worktrees = new(WorktreeKey.Comparer);
    private readonly Dictionary<string, string?> _readOwners = new(StringComparer.Ordinal);
    private readonly Lock _drainGate = new();
    private MainWindowViewModel? _shell;
    private RepoTabViewModel? _followedTab;
    private int _drainPosted;
    private bool _touchedUnknown;
    private bool _disposed;

    /// <param name="operations">The session's operation records.</param>
    /// <param name="calls">The session's git call records; null where nothing records them (view-model tests): the operations show without calls.</param>
    /// <param name="session">The session file, which keeps the panel's height (<see cref="SessionState.ActivityLogHeight"/>).</param>
    /// <param name="dates">The zone the times are shown in (D43).</param>
    /// <param name="log">The app's log.</param>
    public ActivityLogViewModel(IOperationLog operations, IGitCallLog? calls, ISessionStore session, DateDisplay dates, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dates);
        _operations = operations;
        _calls = calls;
        _session = session;
        _dates = dates;
        _log = log ?? NullAppLog.Instance;
        _ui = SynchronizationContext.Current;
        PreferredHeight = session.Current.ActivityLogHeight ?? DefaultHeight;

        // Listening first and then reading what is there already: a record that comes between
        // the two is taken once (by its id, or the call's identity).
        operations.Started += OnStarted;
        operations.Added += OnAdded;
        operations.Updated += OnUpdated;
        operations.Evicted += OnEvicted;
        if (calls is not null)
        {
            calls.Added += OnCallAdded;
            calls.Removed += OnCallsRemoved;
        }

        foreach (var start in operations.Running())
        {
            TakeStart(start);
        }

        foreach (var record in operations.Snapshot())
        {
            TakeRecord(record);
        }

        foreach (var call in calls?.Snapshot() ?? [])
        {
            TakeCall(call);
        }
    }

    /// <summary>The panel is open (1.2, 2.1): the window's state, kept while a tab without a repository hides the panel. Closed when the app starts.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown), nameof(ToggleToolTip))]
    public partial bool IsOpen { get; private set; }

    /// <summary>The worktree of the active tab's repository, whose log the panel shows; null on a tab without a repository.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown), nameof(ToggleToolTip), nameof(RepositoryName), nameof(CanToggle))]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial ActivityLogWorktree? Current { get; private set; }

    /// <summary>The panel shows: it is open and the active tab has a repository.</summary>
    public bool IsShown => IsOpen && Current is not null;

    /// <summary>The status bar's toggle is enabled: the active tab has a repository (1.2).</summary>
    public bool CanToggle => Current is not null;

    /// <summary>The toggle's tooltip and help text (1.2).</summary>
    public string ToggleToolTip => IsShown ? HideText : ShowText;

    /// <summary>The name the header shows after "Activity log", in the secondary colour (2.2).</summary>
    public string RepositoryName => Current?.Source.Name ?? string.Empty;

    /// <summary>"Show reads" (2.2): the reads of the shown worktree are rows of their own between the operations. Unchecked when the app starts.</summary>
    [ObservableProperty]
    public partial bool ShowReads { get; set; }

    /// <summary>The height the user gave the panel by its splitter (2.1), kept in the session; <see cref="DefaultHeight"/> until then.</summary>
    [ObservableProperty]
    public partial double PreferredHeight { get; private set; }

    /// <summary>The panel is about to close (its button, the toggle, Esc): the view gives the focus away first when it is inside the panel.</summary>
    public event EventHandler? Closing;

    /// <summary>
    /// The height the panel takes in a graph area of <paramref name="graphAreaHeight"/> (2.1, AD1):
    /// <paramref name="preferred"/>, at least <see cref="MinimumHeight"/> and at most what leaves
    /// the content above the splitter <see cref="ContentMinimumHeight"/>; when the window is too low
    /// even for the minimum, the panel gives way further, down to its header, so that its rows hide.
    /// </summary>
    public static double Fit(double graphAreaHeight, double preferred)
    {
        var maximum = graphAreaHeight - ContentMinimumHeight - SplitterHeight;
        return maximum >= MinimumHeight
            ? Math.Clamp(preferred, MinimumHeight, maximum)
            : Math.Max(HeaderHeight, maximum);
    }

    /// <summary>
    /// Follows <paramref name="shell"/>'s active tab: the panel shows its repository's worktree,
    /// and hides on a tab without one (2.1).
    /// </summary>
    public void Follow(MainWindowViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellChanged;
        }

        _shell = shell;
        shell.PropertyChanged += OnShellChanged;
        FollowTab(shell.ActiveTab);
    }

    /// <summary>Shows the log of <paramref name="source"/>'s worktree, or hides the panel for a tab without a repository (null).</summary>
    public void Show(ActivityLogSource? source)
    {
        var previous = Current;
        if (source is null)
        {
            Current = null;
            Release(previous);
            return;
        }

        if (!_worktrees.TryGetValue(source.Key, out var worktree))
        {
            worktree = new ActivityLogWorktree(source);
            _worktrees[source.Key] = worktree;

            // A new working tree can own reads made before (the opening's own reads, AD11).
            _readOwners.Clear();
        }
        else if (worktree.Source != source)
        {
            worktree.Source = source;
            if (worktree.AddFolders(source))
            {
                // Another tab opened it from another folder: the reads made there are its too.
                _readOwners.Clear();
                worktree.IsStale = true;
            }
        }

        if (worktree.IsStale || !ReferenceEquals(Current, worktree))
        {
            Rebuild(worktree);
        }

        Current = worktree;
        OnPropertyChanged(nameof(RepositoryName));
        if (!ReferenceEquals(previous, worktree))
        {
            Release(previous);
        }
    }

    /// <summary>
    /// A worktree no longer shown keeps no rows (15.9): they hold call records the call log may
    /// drop under its bounds, and <see cref="Show"/> makes them again. What the user left (which
    /// rows are open, where the list was scrolled) is kept apart from the rows. Called after the
    /// view has left them.
    /// </summary>
    private static void Release(ActivityLogWorktree? worktree)
    {
        if (worktree is not null)
        {
            worktree.Rows.Clear();
            worktree.IsStale = true;
        }
    }

    /// <summary>Opens the panel (the status bar's toggle).</summary>
    public void Open()
    {
        if (Current is { } current && current.IsStale)
        {
            Rebuild(current);
        }

        IsOpen = true;
    }

    /// <summary>Opens or closes the panel: the status bar's toggle (1.2).</summary>
    [RelayCommand(CanExecute = nameof(CanToggle))]
    private void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>Closes the panel: its close button, the toggle, Esc with the focus in it (2.1).</summary>
    [RelayCommand]
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        Closing?.Invoke(this, EventArgs.Empty);
        IsOpen = false;
    }

    /// <summary>Opens or closes an operation's or a call's row (a click on it, 2.2); other rows do not open.</summary>
    public void ToggleRow(ActivityLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        switch (row)
        {
            case ActivityLogOperationRow operation:
                SetOpen(operation, !operation.IsOpen);
                break;
            case ActivityLogCallRow call:
                SetOpen(call, !call.IsOpen);
                break;
        }
    }

    /// <summary>Opens (Right) or closes (Left) an operation's or a call's row (2.2); other rows do not open.</summary>
    public void SetOpen(ActivityLogRow row, bool open)
    {
        ArgumentNullException.ThrowIfNull(row);
        switch (row)
        {
            case ActivityLogOperationRow operation when operation.IsOpen != open:
                operation.IsOpen = open;
                break;
            case ActivityLogCallRow call when call.IsOpen != open:
                call.IsOpen = open;
                break;
            default:
                return;
        }

        if (Current is { } current)
        {
            Rebuild(current);
        }
    }

    /// <summary>
    /// The user let go of the splitter (2.1): <paramref name="height"/> is the panel's height from
    /// now on, saved in the session.
    /// </summary>
    public void SaveHeight(double height)
    {
        PreferredHeight = Math.Max(MinimumHeight, height);
        _session.Update(state => state with { ActivityLogHeight = PreferredHeight });
    }

    /// <summary>
    /// What a row's menu acts on (2.2): an operation's title, a call's command and output (also from
    /// its output box); null for the lines under an operation, which have no menu.
    /// </summary>
    public static Menus.ActivityLogMenuTarget? MenuTargetOf(ActivityLogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row switch
        {
            ActivityLogOperationRow operation => new Menus.ActivityLogMenuTarget(operation.Title, null, null),
            ActivityLogCallRow call => new Menus.ActivityLogMenuTarget(null, call.Command, OutputOf(call.Call)),
            ActivityLogOutputRow output => new Menus.ActivityLogMenuTarget(null, output.Call.CommandText, OutputOf(output.Call)),
            _ => null,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _operations.Started -= OnStarted;
        _operations.Added -= OnAdded;
        _operations.Updated -= OnUpdated;
        _operations.Evicted -= OnEvicted;
        if (_calls is not null)
        {
            _calls.Added -= OnCallAdded;
            _calls.Removed -= OnCallsRemoved;
        }

        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellChanged;
        }

        FollowTab(null);
    }

    partial void OnShowReadsChanged(bool value)
    {
        foreach (var worktree in _worktrees.Values)
        {
            worktree.IsStale = true;
        }

        if (Current is { } current)
        {
            Rebuild(current);
        }
    }

    /// <summary>A call's output as "Copy output" copies it: its standard output, then its standard error; null when both are empty.</summary>
    private static string? OutputOf(GitCallRecord call)
    {
        var parts = new[] { call.StandardOutput, call.StandardError }
            .Where(part => !string.IsNullOrEmpty(part))
            .Select(part => part.TrimEnd('\r', '\n'))
            .ToList();
        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ActiveTab) && _shell is { } shell)
        {
            FollowTab(shell.ActiveTab);
        }
    }

    private void FollowTab(RepoTabViewModel? tab)
    {
        if (_followedTab is not null)
        {
            _followedTab.PropertyChanged -= OnTabChanged;
        }

        _followedTab = tab;
        if (tab is not null)
        {
            tab.PropertyChanged += OnTabChanged;
        }

        Show(tab?.Repository is { } repository ? ActivityLogSource.Of(repository) : null);
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepoTabViewModel.Repository) && ReferenceEquals(sender, _followedTab))
        {
            Show(_followedTab?.Repository is { } repository ? ActivityLogSource.Of(repository) : null);
        }
    }

    // The records' events, on whatever thread acted: taken over on the view model's own.

    private void OnStarted(object? sender, OperationStart start) => Post(() => TakeStart(start));

    private void OnAdded(object? sender, OperationRecord record) => Post(() => TakeRecord(record));

    private void OnUpdated(object? sender, string id)
    {
        var record = _operations.Snapshot().LastOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (record is not null)
        {
            Post(() => TakeRecord(record));
        }
    }

    private void OnEvicted(object? sender, string id) => Post(() =>
    {
        if (_entries.Remove(id, out var entry))
        {
            Touch(entry.RepositoryKey);
        }

        if (_callsByOperation.Remove(id, out var calls))
        {
            _knownCalls.ExceptWith(calls);
        }

        foreach (var worktree in _worktrees.Values)
        {
            worktree.OperationRows.Remove(id);
            worktree.Lines.Remove(id);
        }
    });

    private void OnCallAdded(object? sender, GitCallRecord call) => Post(() => TakeCall(call));

    private void OnCallsRemoved(object? sender, IReadOnlyList<GitCallRecord> calls) => Post(() =>
    {
        foreach (var call in calls)
        {
            if (!_knownCalls.Remove(call))
            {
                continue;
            }

            if (call.OperationId is { } id)
            {
                if (_callsByOperation.TryGetValue(id, out var list))
                {
                    RemoveCall(list, call);
                }

                Touch(_entries.TryGetValue(id, out var entry) ? entry.RepositoryKey : null);
            }
            else
            {
                RemoveCall(_reads, call);
                Touch(OwnerOf(call));
            }

            foreach (var worktree in _worktrees.Values)
            {
                worktree.CallRows.Remove(call);
            }
        }
    });

    private void TakeStart(OperationStart start)
    {
        if (_entries.TryGetValue(start.Id, out var known) && known.Record is not null)
        {
            // Its record came first: it has ended already.
            return;
        }

        _entries[start.Id] = new OperationEntry(start.Id, start.RepositoryKey, start.Sequence, start, null);
        Touch(start.RepositoryKey);
    }

    private void TakeRecord(OperationRecord record)
    {
        _entries.TryGetValue(record.Id, out var known);
        var key = record.RepositoryKey.Length > 0 ? record.RepositoryKey : known?.RepositoryKey ?? string.Empty;
        var sequence = record.Sequence != 0 ? record.Sequence : known?.Sequence ?? 0;
        _entries[record.Id] = new OperationEntry(record.Id, key, sequence, known?.Start, record);
        Touch(key);
    }

    private void TakeCall(GitCallRecord call)
    {
        if (!_knownCalls.Add(call))
        {
            return;
        }

        if (call.OperationId is { } id)
        {
            if (!_callsByOperation.TryGetValue(id, out var list))
            {
                list = [];
                _callsByOperation[id] = list;
            }

            InsertBySequence(list, call);
            Touch(_entries.TryGetValue(id, out var entry) ? entry.RepositoryKey : null);
        }
        else
        {
            InsertBySequence(_reads, call);
            if (ShowReads)
            {
                Touch(OwnerOf(call));
            }
        }
    }

    /// <summary>Takes <paramref name="call"/> itself out of <paramref name="calls"/>: two records can be equal in every field.</summary>
    private static void RemoveCall(List<GitCallRecord> calls, GitCallRecord call)
    {
        var index = calls.FindIndex(candidate => ReferenceEquals(candidate, call));
        if (index >= 0)
        {
            calls.RemoveAt(index);
        }
    }

    /// <summary>Calls arrive in the order they ended; they are kept in the order they started.</summary>
    private static void InsertBySequence(List<GitCallRecord> calls, GitCallRecord call)
    {
        var at = calls.Count;
        while (at > 0 && calls[at - 1].Sequence > call.Sequence)
        {
            at--;
        }

        calls.Insert(at, call);
    }

    /// <summary>
    /// Something of the worktree <paramref name="key"/> changed: its rows are made again, at once
    /// when it shows (at the end of the batch), when it shows next otherwise. A change of no known
    /// worktree (a call whose operation has not started here yet) is shown with the next one.
    /// </summary>
    private void Touch(string? key)
    {
        if (key is null)
        {
            _touchedUnknown = true;
            return;
        }

        if (_worktrees.TryGetValue(key, out var worktree))
        {
            worktree.IsStale = true;
        }
    }

    private void Post(Action action)
    {
        if (_disposed)
        {
            return;
        }

        _pending.Enqueue(action);
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            Drain();
        }
        else if (Interlocked.Exchange(ref _drainPosted, 1) == 0)
        {
            _ui.Post(_ => Drain(), null);
        }
    }

    /// <summary>
    /// Takes over every record that came since the last batch, then makes the shown rows again
    /// once. Without a synchronization context (view-model tests) it runs on the thread that acted,
    /// one batch at a time.
    /// </summary>
    private void Drain()
    {
        lock (_drainGate)
        {
            Volatile.Write(ref _drainPosted, 0);
            if (_disposed)
            {
                return;
            }

            while (_pending.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _log.Error("The activity log could not take a record.", ex);
                }
            }

            if (Current is { } current && (current.IsStale || _touchedUnknown))
            {
                Rebuild(current);
            }

            _touchedUnknown = false;
        }
    }

    /// <summary>
    /// The worktree whose working tree, or the folder it was opened from, holds the read's folder
    /// (2.3), the innermost when they nest; null for none (<c>git --version</c>). A folder reached
    /// through a link is compared as the read named it: the opening's first read runs in the folder
    /// as it was opened, while git reports the working tree with the links resolved.
    /// </summary>
    private string? OwnerOf(GitCallRecord read)
    {
        if (read.WorkingDirectory is not { Length: > 0 } folder)
        {
            return null;
        }

        if (_readOwners.TryGetValue(folder, out var known))
        {
            return known;
        }

        string? owner = null;
        var longest = -1;
        if (Normalized(folder) is { } full)
        {
            foreach (var worktree in _worktrees.Values)
            {
                foreach (var tree in worktree.Folders)
                {
                    if (tree.Length > longest && IsSameOrUnder(full, tree))
                    {
                        owner = worktree.Source.Key;
                        longest = tree.Length;
                    }
                }
            }
        }

        _readOwners[folder] = owner;
        return owner;
    }

    /// <summary>A folder in full, without a trailing separator; null for a path that cannot be one.</summary>
    internal static string? Normalized(string folder)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsSameOrUnder(string folder, string tree)
    {
        var comparison = WorktreeKey.Comparer.Equals("a", "A") ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(folder, tree, comparison)
            || (folder.Length > tree.Length
                && folder.StartsWith(tree, comparison)
                && (folder[tree.Length] == Path.DirectorySeparatorChar || folder[tree.Length] == Path.AltDirectorySeparatorChar));
    }

    /// <summary>Makes <paramref name="worktree"/>'s rows from the records, changing its list only where it differs.</summary>
    private void Rebuild(ActivityLogWorktree worktree)
    {
        worktree.IsStale = false;
        var key = worktree.Source.Key;
        var top = new List<(long Sequence, object Item)>();
        foreach (var entry in _entries.Values)
        {
            if (WorktreeKey.Comparer.Equals(entry.RepositoryKey, key))
            {
                top.Add((entry.Sequence, entry));
            }
        }

        if (ShowReads)
        {
            foreach (var read in _reads)
            {
                if (OwnerOf(read) is { } owner && WorktreeKey.Comparer.Equals(owner, key))
                {
                    top.Add((read.Sequence, read));
                }
            }
        }

        top.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        var rows = new List<ActivityLogRow>(top.Count);
        foreach (var (_, item) in top)
        {
            if (item is OperationEntry entry)
            {
                AddOperation(worktree, entry, rows);
            }
            else
            {
                AddCall(worktree, (GitCallRecord)item, 0, rows);
            }
        }

        Apply(worktree.Rows, rows);
    }

    private void AddOperation(ActivityLogWorktree worktree, OperationEntry entry, List<ActivityLogRow> rows)
    {
        if (!worktree.OperationRows.TryGetValue(entry.Id, out var row))
        {
            row = new ActivityLogOperationRow(entry.Id);
            worktree.OperationRows[entry.Id] = row;
        }

        if (entry.Record is { } record)
        {
            row.Show(record, TimeOf(record.DisplayedStart));
        }
        else
        {
            row.Show(entry.Start!, TimeOf(entry.Start!.DisplayedStart));
        }

        rows.Add(row);
        if (!row.IsOpen)
        {
            return;
        }

        var dropped = _calls?.HasDroppedCalls(entry.Id) ?? false;
        var lines = LinesOf(worktree, entry, dropped);
        var noLongerKept = lines.FirstOrDefault(line => line.Kind == ActivityLogRowKind.NoLongerKept);
        rows.AddRange(lines.Where(line => line.Kind != ActivityLogRowKind.NoLongerKept));
        foreach (var call in _callsByOperation.GetValueOrDefault(entry.Id) ?? [])
        {
            AddCall(worktree, call, 1, rows);
        }

        if (noLongerKept is not null)
        {
            rows.Add(noLongerKept);
        }
    }

    /// <summary>An opened operation's lines (2.2): its failure's summary, its notes, and, after its calls, that they are no longer kept; made again when its record changes.</summary>
    private static IReadOnlyList<ActivityLogLineRow> LinesOf(ActivityLogWorktree worktree, OperationEntry entry, bool dropped)
    {
        var record = entry.Record;
        if (worktree.Lines.TryGetValue(entry.Id, out var known) && ReferenceEquals(known.Record, record) && known.Dropped == dropped && record is not null)
        {
            return known.Lines;
        }

        var lines = new List<ActivityLogLineRow>();
        if (record?.FailureSummary is { Length: > 0 } summary)
        {
            lines.Add(new ActivityLogLineRow(ActivityLogRowKind.FailureSummary, summary));
        }

        foreach (var note in record?.Notes ?? [])
        {
            lines.Add(new ActivityLogLineRow(ActivityLogRowKind.Note, note));
        }

        if (dropped)
        {
            lines.Add(new ActivityLogLineRow(ActivityLogRowKind.NoLongerKept, NoLongerKeptText));
        }

        if (record is not null)
        {
            worktree.Lines[entry.Id] = (record, dropped, lines);
        }

        return lines;
    }

    private static void AddCall(ActivityLogWorktree worktree, GitCallRecord call, int depth, List<ActivityLogRow> rows)
    {
        if (!worktree.CallRows.TryGetValue(call, out var row))
        {
            row = new ActivityLogCallRow(call, depth);
            worktree.CallRows[call] = row;
        }

        rows.Add(row);
        if (row.IsOpen)
        {
            rows.Add(row.Output);
        }
    }

    /// <summary>
    /// Makes <paramref name="list"/> hold <paramref name="rows"/>, keeping the rows the two share at
    /// their start and at their end in place: a new last row is one insertion, an opened row's lines
    /// one insertion in the middle, so the view keeps its containers and its scroll.
    /// </summary>
    private static void Apply(ObservableCollection<ActivityLogRow> list, List<ActivityLogRow> rows)
    {
        var prefix = 0;
        while (prefix < list.Count && prefix < rows.Count && ReferenceEquals(list[prefix], rows[prefix]))
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < list.Count - prefix && suffix < rows.Count - prefix
            && ReferenceEquals(list[list.Count - 1 - suffix], rows[rows.Count - 1 - suffix]))
        {
            suffix++;
        }

        for (var i = list.Count - suffix - 1; i >= prefix; i--)
        {
            list.RemoveAt(i);
        }

        for (var i = prefix; i < rows.Count - suffix; i++)
        {
            list.Insert(i, rows[i]);
        }
    }

    private string TimeOf(DateTimeOffset start) =>
        TimeZoneInfo.ConvertTime(start, _dates.TimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>An operation the log knows: started, ended, or both.</summary>
    private sealed record OperationEntry(string Id, string RepositoryKey, long Sequence, OperationStart? Start, OperationRecord? Record);
}
