using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Graph;

/// <summary>
/// What the search bar (<c>SearchViewModel</c>, docs/phase-5-design.md 3) asks of its repository:
/// <see cref="RepositoryViewModel"/> implements it, so that the search is tested with a fake.
/// Every member is called on the UI thread but <see cref="SearchAsync"/>'s callbacks.
/// </summary>
public interface ISearchHost
{
    /// <summary>The graph whose rows are searched (3.2): exactly the rows it shows, commits and stashes.</summary>
    CommitGraphData Graph { get; }

    /// <summary>The selected commit's row, or -1 when none or several are selected: where Enter and Shift+Enter go on from (3.1).</summary>
    int SelectedRow { get; }

    /// <summary>Cancelled when the tab closes: a search runs no longer than its tab (3.4).</summary>
    CancellationToken Lifetime { get; }

    IAppLog Log { get; }

    /// <summary>The git part of a search (<see cref="IGitRepository.SearchAsync"/>): <paramref name="shas"/> searched in chunks, each match handed over as git prints it.</summary>
    Task SearchAsync(SearchMode mode, string text, IReadOnlyList<string> shas, Action<string> onMatch, IProgress<int>? progress, CancellationToken cancellationToken);

    /// <summary>Selects the match on <paramref name="row"/> as a click selects it, and scrolls it into view without centring it (3.1).</summary>
    void SelectMatch(int row);

    /// <summary>What the graph marks and dims (3.3).</summary>
    void ShowMarks(GraphSearchMarks marks);

    /// <summary>The bar closes (Esc, its close button): the marks go, the selection stays, the graph takes the focus (3.1).</summary>
    void CloseSearch();
}

/// <summary>
/// The search bar above the graph (docs/phase-5-design.md 3, D157, D174): the mode, the box, the
/// count, the progress of a Changes search, previous, next and close. The box searches as the user
/// types, <see cref="DelayOf"/> after the last key, and at once on Enter when nothing ran for the
/// text yet; a new text or mode cancels the search that runs. Git is given the graph's rows, the
/// loaded ones first and the later ones as they come; the Commits mode also matches authors and
/// SHA prefixes over the rows itself. Matches are applied in batches at most every
/// <see cref="BatchInterval"/>. Git's results are kept by SHA for the mode and text, so that a
/// reloaded graph gives git only its rows not searched yet (3.4); only a new text or mode starts
/// over. The view is <c>Views/Graph/SearchBarView</c>.
/// </summary>
public sealed partial class SearchViewModel : ObservableObject, IDisposable
{
    /// <summary>How often matches arriving from git are applied, at most (3.4).</summary>
    public static readonly TimeSpan BatchInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The fewest hex digits a text needs to match a SHA prefix (3.2).</summary>
    public const int MinShaPrefix = 4;

    /// <summary>The mode menu's header (FD5).</summary>
    public const string ModeMenuHeader = "Search";

    public const string PreviousToolTip = "Previous match (Shift+Enter)";

    public const string NextToolTip = "Next match (Enter)";

    public const string CloseToolTip = "Close the search (Esc)";

    public const string SearchingText = "Searching…";

    public const string NoMatchesText = "No matches";

    private readonly ISearchHost _host;
    private readonly TimeProvider _clock;
    private readonly SynchronizationContext? _ui;
    private readonly Lock _gate = new();

    /// <summary>The SHAs git was given for the text and mode searched, and the ones it matched (3.4).</summary>
    private readonly HashSet<string> _searched = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gitMatches = new(StringComparer.Ordinal);

    /// <summary>The Commits mode's author and SHA matches over the rows of <see cref="_memoryGraph"/>, the first <see cref="_memoryScanned"/> of them.</summary>
    private readonly HashSet<string> _memoryMatches = new(StringComparer.Ordinal);
    private CommitGraphData? _memoryGraph;
    private int _memoryScanned;

    /// <summary>Matches from git not applied yet, and whether a batch is due; both of the search of <see cref="_generation"/>, under <see cref="_gate"/>.</summary>
    private readonly List<string> _arrived = [];
    private bool _batchDue;

    /// <summary>
    /// Enter, Shift+Enter, previous or next waiting for the match they go to (backwards for
    /// Shift+Enter and previous), or null: while git still searches the rows before it, the match
    /// nearest the selected row is not known yet (3.1).
    /// </summary>
    private bool? _goWhenKnown;

    private CancellationTokenSource? _pending;
    private CancellationTokenSource? _running;
    private TaskCompletionSource? _rowsCame;
    private CommitGraphData? _watched;
    private int _generation;
    private bool _disposed;

    /// <param name="host">The repository.</param>
    /// <param name="mode">The mode the bar opens in: Commits the first time in a repository, the last one chosen after that (FD1).</param>
    /// <param name="clock">The clock of the box's delay and the batches.</param>
    public SearchViewModel(ISearchHost host, SearchMode mode, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _clock = clock ?? TimeProvider.System;
        _ui = SynchronizationContext.Current;
        Mode = mode;
        Watch(host.Graph);
    }

    /// <summary>What the box searches (3.2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeLabel), nameof(Placeholder))]
    public partial SearchMode Mode { get; private set; }

    /// <summary>The mode button's text: <c>Commits</c>, <c>Files</c> or <c>Changes</c>.</summary>
    public string ModeLabel => LabelOf(Mode);

    /// <summary>The box's placeholder, by mode (3.1).</summary>
    public string Placeholder => Mode switch
    {
        SearchMode.Files => "File name or path (case ignored for A–Z)",
        SearchMode.Changes => "Text added or removed (case ignored for A–Z)",
        _ => "Message, author or SHA",
    };

    /// <summary>The box's text, which the view binds both ways: a change searches after the mode's delay.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>The text the matches are for: empty before the first search and while the box is empty.</summary>
    [ObservableProperty]
    public partial string SearchedText { get; private set; } = string.Empty;

    /// <summary>The mode the matches are for.</summary>
    public SearchMode SearchedMode { get; private set; }

    /// <summary>The box's text changed and its delay has not passed yet: no search runs for it so far.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettled))]
    public partial bool IsPending { get; private set; }

    /// <summary>A search runs: git is busy with the rows, or waits for the rows the graph still loads.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettled), nameof(ShowsProgress))]
    public partial bool IsSearching { get; private set; }

    /// <summary>The search of the box's text and mode has ended and its matches are applied (or the box is empty): what a check waits for.</summary>
    public bool IsSettled => !IsPending && !IsSearching
        && (Text.Length == 0 || (string.Equals(SearchedText, Text, StringComparison.Ordinal) && SearchedMode == Mode));

    /// <summary>The matching commits' rows in the graph, top first.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatches))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand), nameof(NextCommand))]
    public partial IReadOnlyList<int> MatchRows { get; private set; } = [];

    /// <summary>Previous and next are enabled (3.1).</summary>
    public bool HasMatches => MatchRows.Count > 0;

    /// <summary>The count (3.1), the first form that holds; empty while the box is empty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountToolTip))]
    public partial string CountText { get; private set; } = string.Empty;

    /// <summary>The count's whole text, for its tooltip when the bar is too narrow for it; null while it is empty.</summary>
    public string? CountToolTip => CountText.Length == 0 ? null : CountText;

    /// <summary>The Changes mode's 2-px progress bar shows while it searches (3.1).</summary>
    public bool ShowsProgress => IsSearching && SearchedMode == SearchMode.Changes;

    /// <summary>How many of the graph's rows git has searched for the text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial int ProgressValue { get; private set; }

    /// <summary>How many rows the graph has.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial int ProgressMaximum { get; private set; }

    /// <summary>The progress bar's help text and tooltip: <c>Searched {k} of {n} commits</c>.</summary>
    public string ProgressText => string.Create(CultureInfo.InvariantCulture, $"Searched {ProgressValue} of {ProgressMaximum} commits");

    /// <summary>The matches as the graph marks them; <see cref="GraphSearchMarks.None"/> while the box is empty.</summary>
    public GraphSearchMarks Marks { get; private set; } = GraphSearchMarks.None;

    /// <summary>The search that runs, or the last one: completes when it has ended. Tests wait on it.</summary>
    public Task Completion { get; private set; } = Task.CompletedTask;

    /// <summary>Raised when the box should take the keyboard focus with its text selected: the bar opened, Search again, a mode chosen (3.1, FD4).</summary>
    public event EventHandler? FocusRequested;

    /// <summary>The mode button's text for <paramref name="mode"/>.</summary>
    public static string LabelOf(SearchMode mode) => mode switch
    {
        SearchMode.Files => "Files",
        SearchMode.Changes => "Changes",
        _ => "Commits",
    };

    /// <summary>The box's delay after the last key (3.1): 300 ms for Commits, 600 ms for Files and Changes.</summary>
    public static TimeSpan DelayOf(SearchMode mode) =>
        TimeSpan.FromMilliseconds(mode == SearchMode.Commits ? 300 : 600);

    /// <summary>
    /// The mode menu (3.1, FD5): header <c>Search</c>, then the three modes, always enabled and none
    /// marked. Choosing one keeps the text and searches it in that mode (FD4).
    /// </summary>
    public MenuModel ModeMenu() => new(
    [
        new MenuHeader(ModeMenuHeader),
        new MenuItem("Commits: message, author or SHA", () => ChooseMode(SearchMode.Commits)),
        new MenuItem("Files: a path or file name", () => ChooseMode(SearchMode.Files)),
        new MenuItem("Changes: text added or removed", () => ChooseMode(SearchMode.Changes)),
    ]);

    /// <summary>
    /// A mode chosen in the menu (FD4): the text stays and is searched in the new mode after its
    /// delay; the box takes the focus with its text selected.
    /// </summary>
    public void ChooseMode(SearchMode mode)
    {
        if (_disposed)
        {
            return;
        }

        if (mode != Mode)
        {
            Mode = mode;
            Restart();
        }

        RequestFocus();
    }

    /// <summary>Asks the view to give the box the keyboard focus with its text selected.</summary>
    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Enter in the box (3.1): the search starts at once when its delay has not passed yet, then the
    /// next match is selected; with <paramref name="backwards"/> (Shift+Enter) the previous one. A
    /// search that runs selects it once git has searched the rows between the selected row and it.
    /// </summary>
    public void Enter(bool backwards)
    {
        if (_disposed || Text.Length == 0)
        {
            return;
        }

        if (IsPending)
        {
            Start();
        }

        GoToMatch(backwards);
    }

    /// <summary>
    /// The repository's graph was replaced (a refresh loaded it again, 3.4): the marks and the
    /// count stay, the author and SHA matches are made again over its rows, and git is given only
    /// its rows not searched yet.
    /// </summary>
    public void OnGraphReplaced()
    {
        if (_disposed)
        {
            return;
        }

        Watch(_host.Graph);
        Apply();
        if (SearchedText.Length > 0 && !IsPending)
        {
            Run();
        }
    }

    /// <summary>The graph's selection changed: <c>{k} of {n}</c> follows it.</summary>
    public void OnSelectionChanged()
    {
        if (!_disposed)
        {
            UpdateCount();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _goWhenKnown = null;
        Cancel(ref _pending);
        Cancel(ref _running);
        Watch(null);
    }

    partial void OnTextChanged(string value) => Restart();

    /// <summary>Selects the first match after the selected row in row order, wrapping round (3.1).</summary>
    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void Next() => GoToMatch(backwards: false);

    /// <summary>Selects the last match before the selected row in row order, wrapping round (3.1); from the top it wraps to the last.</summary>
    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void Previous() => GoToMatch(backwards: true);

    /// <summary>Goes to the next or the previous match: at once when it is known, otherwise as soon as it is (<see cref="GoWhenKnown"/>).</summary>
    private void GoToMatch(bool backwards)
    {
        _goWhenKnown = backwards;
        GoWhenKnown();
    }

    /// <summary>
    /// Selects the match Enter, previous or next waits for, once it is known: the search has ended,
    /// or git has searched every row between the selected row and it, so that no nearer match can
    /// come (3.1). A search with no match selects nothing.
    /// </summary>
    private void GoWhenKnown()
    {
        if (_goWhenKnown is not { } backwards || (IsSearching && !IsKnown(backwards)))
        {
            return;
        }

        _goWhenKnown = null;
        var rows = MatchRows;
        if (rows.Count == 0)
        {
            return;
        }

        var from = _host.SelectedRow;
        var row = backwards
            ? (from < 0 ? rows[^1] : rows.LastOrDefault(row => row < from, rows[^1]))
            : rows.FirstOrDefault(row => row > from, rows[0]);
        _host.SelectMatch(row);
    }

    /// <summary>
    /// Whether the match after (or before) the selected row is known while the search runs: going
    /// from the selected row that way, wrapping round, a match is reached before any row git has
    /// not searched yet. Wrapping round needs the whole graph, since rows still to come lie between.
    /// </summary>
    private bool IsKnown(bool backwards)
    {
        var graph = _host.Graph;
        var matches = MatchRows.ToHashSet();
        var count = graph.Count;
        if (count == 0)
        {
            return graph.IsComplete;
        }

        var row = _host.SelectedRow;
        for (var step = 0; step < count; step++)
        {
            row += backwards ? -1 : 1;
            if (row < 0 || row >= count)
            {
                if (!graph.IsComplete)
                {
                    return false;
                }

                row = row < 0 ? count - 1 : 0;
            }

            if (matches.Contains(row))
            {
                return true;
            }

            if (!_searched.Contains(graph.CommitAt(row).Sha))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The close button and Esc (3.1).</summary>
    [RelayCommand]
    private void Close()
    {
        if (_disposed)
        {
            return;
        }

        Dispose();
        _host.CloseSearch();
    }

    /// <summary>
    /// A new text or mode: the search that runs stops, and the new one starts after the mode's
    /// delay. An empty box clears the marks at once.
    /// </summary>
    private void Restart()
    {
        if (_disposed)
        {
            return;
        }

        Cancel(ref _pending);
        Cancel(ref _running);
        _goWhenKnown = null;
        IsSearching = false;
        if (Text.Length == 0)
        {
            IsPending = false;
            Reset(string.Empty);
            return;
        }

        var pending = CancellationTokenSource.CreateLinkedTokenSource(_host.Lifetime);
        _pending = pending;
        IsPending = true;
        _ = StartAfterDelayAsync(DelayOf(Mode), pending);
    }

    private async Task StartAfterDelayAsync(TimeSpan delay, CancellationTokenSource pending)
    {
        try
        {
            await Task.Delay(delay, _clock, pending.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        OnUi(() =>
        {
            if (ReferenceEquals(_pending, pending))
            {
                Start();
            }
        });
    }

    /// <summary>Starts the search of the box's text in the mode now: what was found for another text or mode is forgotten.</summary>
    private void Start()
    {
        Cancel(ref _pending);
        IsPending = false;
        Reset(Text);
        Run();
    }

    /// <summary>Forgets every match and makes <paramref name="text"/> in <see cref="Mode"/> the search's.</summary>
    private void Reset(string text)
    {
        lock (_gate)
        {
            // A batch the last search left due is not this one's: its matches get their own.
            _generation++;
            _arrived.Clear();
            _batchDue = false;
        }

        _searched.Clear();
        _gitMatches.Clear();
        _memoryMatches.Clear();
        _memoryGraph = null;
        _memoryScanned = 0;
        SearchedMode = Mode;
        SearchedText = text;
        Apply();
    }

    /// <summary>Runs git over the rows not searched yet, until the graph is complete and every row searched.</summary>
    private void Run()
    {
        Cancel(ref _running);
        var running = CancellationTokenSource.CreateLinkedTokenSource(_host.Lifetime);
        _running = running;
        IsSearching = true;
        UpdateCount();
        Completion = RunAsync(_generation, running);
    }

    private async Task RunAsync(int generation, CancellationTokenSource running)
    {
        var token = running.Token;
        var mode = SearchedMode;
        var text = SearchedText;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var graph = _host.Graph;
                var rows = Unsearched(graph);
                if (rows.Count == 0)
                {
                    if (graph.IsComplete)
                    {
                        break;
                    }

                    // The graph still loads: its later rows are searched as they come.
                    var came = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _rowsCame = came;
                    await came.Task.WaitAsync(token);
                    continue;
                }

                var progress = new SearchProgress(this, generation, rows, _searched.Count(sha => graph.IndexOf(sha) >= 0));
                await _host.SearchAsync(mode, text, rows, sha => Arrived(generation, sha), progress, token);
                if (generation != _generation || token.IsCancellationRequested)
                {
                    return;
                }

                var searched = Math.Min(progress.Reported, rows.Count);
                for (var i = 0; i < searched; i++)
                {
                    _searched.Add(rows[i]);
                }

                if (searched < rows.Count)
                {
                    // Git no longer knows a commit of the graph (it is older than a gc that pruned
                    // it): the search stops quietly, and the next refresh's graph gives git the
                    // rows not searched again (3.2).
                    break;
                }

                ApplyArrived();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _host.Log.Warning($"The search for \"{text}\" failed.", ex);
        }

        if (ReferenceEquals(_running, running))
        {
            _running = null;
            running.Dispose();
            ApplyArrived();
            IsSearching = false;
            UpdateCount();
            GoWhenKnown();
        }
    }

    /// <summary>The rows of <paramref name="graph"/> git has not searched for the text yet, in row order.</summary>
    private List<string> Unsearched(CommitGraphData graph)
    {
        var rows = new List<string>();
        for (var row = 0; row < graph.Count; row++)
        {
            var sha = graph.CommitAt(row).Sha;
            if (!_searched.Contains(sha))
            {
                rows.Add(sha);
            }
        }

        return rows;
    }

    /// <summary>A match git printed, on its reader thread: applied with the next batch.</summary>
    private void Arrived(int generation, string sha)
    {
        bool schedule;
        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }

            _arrived.Add(sha);
            schedule = !_batchDue;
            _batchDue = true;
        }

        if (schedule)
        {
            _ = ApplyBatchLaterAsync(generation);
        }
    }

    private async Task ApplyBatchLaterAsync(int generation)
    {
        try
        {
            await Task.Delay(BatchInterval, _clock, _host.Lifetime).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        OnUi(() =>
        {
            if (generation == _generation && !_disposed)
            {
                ApplyArrived();
            }
        });
    }

    /// <summary>Applies the matches that came since the last batch.</summary>
    private void ApplyArrived()
    {
        string[] arrived;
        lock (_gate)
        {
            arrived = [.. _arrived];
            _arrived.Clear();
            _batchDue = false;
        }

        if (arrived.Length > 0)
        {
            var graph = _host.Graph;
            foreach (var sha in arrived.Where(sha => graph.IndexOf(sha) < 0))
            {
                // A match that is no row (none should come) is dropped (3.2).
                _host.Log.Debug($"The search matched {sha}, which is no row of the graph.");
            }

            _gitMatches.UnionWith(arrived);
            Apply();
        }
    }

    /// <summary>The marks, the matching rows and the count, from what was found so far over the graph's rows now.</summary>
    private void Apply()
    {
        var graph = _host.Graph;
        if (SearchedText.Length == 0)
        {
            MatchRows = [];
            Marks = GraphSearchMarks.None;
            _host.ShowMarks(Marks);
            ProgressValue = 0;
            ProgressMaximum = graph.Count;
            UpdateCount();
            return;
        }

        MatchInMemory(graph);
        var matches = new HashSet<string>(_gitMatches, StringComparer.Ordinal);
        matches.UnionWith(_memoryMatches);
        var rows = new List<int>(matches.Count);
        foreach (var sha in matches)
        {
            // Rows a reload took away keep their result for a later graph (3.4).
            if (graph.IndexOf(sha) is >= 0 and var row)
            {
                rows.Add(row);
            }
        }

        rows.Sort();
        MatchRows = rows;
        Marks = new GraphSearchMarks(matches, isActive: true);
        _host.ShowMarks(Marks);
        ProgressValue = _searched.Count(sha => graph.IndexOf(sha) >= 0);
        ProgressMaximum = graph.Count;
        UpdateCount();
        GoWhenKnown();
    }

    /// <summary>
    /// The Commits mode's part the app matches itself (3.2): an author's name or e-mail that
    /// contains the text, a SHA that starts with it (4 to 40 hex digits); case ignored. New rows
    /// of the same graph are matched as they come; another graph is matched whole.
    /// </summary>
    private void MatchInMemory(CommitGraphData graph)
    {
        if (SearchedMode != SearchMode.Commits)
        {
            return;
        }

        if (!ReferenceEquals(_memoryGraph, graph))
        {
            _memoryMatches.Clear();
            _memoryGraph = graph;
            _memoryScanned = 0;
        }

        var text = SearchedText;
        var prefix = IsShaPrefix(text) ? text.ToLowerInvariant() : null;
        for (; _memoryScanned < graph.Count; _memoryScanned++)
        {
            var commit = graph.CommitAt(_memoryScanned);
            if (commit.AuthorName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || commit.AuthorEmail.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (prefix is not null && commit.Sha.StartsWith(prefix, StringComparison.Ordinal)))
            {
                _memoryMatches.Add(commit.Sha);
            }
        }
    }

    /// <summary>Whether <paramref name="text"/> can be a SHA's beginning: 4 to 40 hex digits.</summary>
    private static bool IsShaPrefix(string text) =>
        text.Length is >= MinShaPrefix and <= 40 && text.All(char.IsAsciiHexDigit);

    /// <summary>The count's text (3.1), the first that holds; <c>1 match</c> and <c>1 so far…</c> in the singular (D113).</summary>
    private void UpdateCount()
    {
        var count = MatchRows.Count;
        if (Text.Length == 0 || SearchedText.Length == 0)
        {
            CountText = string.Empty;
        }
        else if (IsSearching)
        {
            CountText = count == 0 ? SearchingText : string.Create(CultureInfo.InvariantCulture, $"{count} so far…");
        }
        else if (count == 0)
        {
            CountText = NoMatchesText;
        }
        else if (IndexOfSelected() is >= 0 and var index)
        {
            CountText = string.Create(CultureInfo.InvariantCulture, $"{index + 1} of {count}");
        }
        else
        {
            CountText = count == 1 ? "1 match" : string.Create(CultureInfo.InvariantCulture, $"{count} matches");
        }
    }

    /// <summary>Where the selected row is among the matching rows, or -1.</summary>
    private int IndexOfSelected()
    {
        var selected = _host.SelectedRow;
        if (selected < 0)
        {
            return -1;
        }

        var rows = MatchRows;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i] == selected)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Follows <paramref name="graph"/>'s pages: a search that waits for rows goes on when they come, and new rows are matched in memory.</summary>
    private void Watch(CommitGraphData? graph)
    {
        if (ReferenceEquals(_watched, graph))
        {
            return;
        }

        if (_watched is not null)
        {
            _watched.Changed -= OnGraphChanged;
        }

        _watched = graph;
        if (graph is not null)
        {
            graph.Changed += OnGraphChanged;
        }
    }

    private void OnGraphChanged(object? sender, EventArgs e)
    {
        if (_disposed || !ReferenceEquals(sender, _host.Graph))
        {
            return;
        }

        if (SearchedText.Length > 0)
        {
            Apply();
        }

        _rowsCame?.TrySetResult();
    }

    private void OnUi(Action action)
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

    private static void Cancel(ref CancellationTokenSource? source)
    {
        var cancelled = source;
        source = null;
        if (cancelled is not null)
        {
            cancelled.Cancel();
            cancelled.Dispose();
        }
    }

    /// <summary>
    /// How far git got through the rows given, reported after each of its calls: what was searched
    /// is read when the search returns, and meanwhile the rows git has searched are known with the
    /// matches it handed over for them, so that Enter can go on (<see cref="IsKnown"/>), and a
    /// Changes search's progress bar follows.
    /// </summary>
    private sealed class SearchProgress(SearchViewModel search, int generation, IReadOnlyList<string> rows, int searchedBefore) : IProgress<int>
    {
        private int _reported;

        /// <summary>How many of the rows given git has searched.</summary>
        public int Reported => Volatile.Read(ref _reported);

        public void Report(int value)
        {
            Volatile.Write(ref _reported, value);
            search.OnUi(() =>
            {
                if (generation == search._generation && !search._disposed)
                {
                    // Git hands over a call's matches before it reports the call's rows searched.
                    for (var i = 0; i < Math.Min(value, rows.Count); i++)
                    {
                        search._searched.Add(rows[i]);
                    }

                    search.ApplyArrived();
                    search.ProgressValue = searchedBefore + value;
                    search.ProgressMaximum = search._host.Graph.Count;
                    search.GoWhenKnown();
                }
            });
        }
    }
}
