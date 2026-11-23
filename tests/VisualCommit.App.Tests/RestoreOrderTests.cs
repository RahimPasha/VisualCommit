using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The restore order (D457 (5), ruling 76; docs/phase-8a-design.md 14.3, 14.4): when the session's
/// active restored tab is a repository's, it opens first and alone; the other restored tabs open at
/// its first page, its opening's or first load's end without one, its close or after 5 s on the
/// restore clock, at most <see cref="RestoreOrder.OpensAtOnce"/> at once, in tab order; a waiting
/// tab that is shown opens at once, one that is closed never opens, and the window's close opens
/// none of them. With a dashboard or an empty tab active, or one restored tab, every restored tab
/// opens at once, as before. A window over fakes on the headless dispatcher, with no session and no
/// git: the provider holds each opening until the test answers it, each fake hands its one commit
/// and ends its load when the test says, and the restore clock moves only when the test moves it.
/// </summary>
public class RestoreOrderTests
{
    private const string OrderPrefix = "Restore order:";

    private static readonly GitDetection GitFound = new(GitAvailability.Available, "/usr/bin/git", new GitVersion(2, 47, 1), null);

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>How the active tab's opening or first load ends without a first page (14.4 test 3).</summary>
    public enum ActiveEnding
    {
        OpeningFails,
        NoCommits,
        RefsFail,
        LoadFails,
    }

    /// <summary>How the window's start ends while tabs wait (14.4 test 10).</summary>
    public enum WindowEnd
    {
        CloseAll,
        LifetimeCancelled,
    }

    /// <summary>What is active in a start that makes no order (14.4 test 11).</summary>
    public enum ActiveWithoutOrder
    {
        Dashboard,
        EmptyTab,
    }

    [AvaloniaFact]
    public async Task Behind_an_active_restored_tab_only_its_opening_is_asked_for_at_first()
    {
        using var lab = Lab.Of(1, "a", "b", "c");

        lab.Start();

        Assert.Equal(Folders("b"), lab.Provider.Opened);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b"), lab.Provider.Opened);
        Assert.False(lab.Starting.IsCompleted);
    }

    [AvaloniaFact]
    public async Task The_others_open_in_tab_order_once_the_active_tabs_graph_has_its_first_page()
    {
        using var lab = Lab.Of(1, "a", "b", "c");
        var (tabA, tabB, tabC) = (lab.Window.Tabs[0], lab.Window.Tabs[1], lab.Window.Tabs[2]);
        lab.Start();
        Assert.Equal(Folders("b"), lab.Provider.Opened);

        var b = lab.Provider.Answer(Folder("b"));
        await DashboardTestSupport.WaitUntilAsync(() => tabB.Repository?.Refs is not null, "b's refs to be read");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b"), lab.Provider.Opened);
        Assert.Equal(1, lab.Clock.ArmedTimers);

        b.SendFirstPage();
        await lab.WaitForOpenedAsync(3);
        Assert.Equal(Folders("b", "a", "c"), lab.Provider.Opened);
        Assert.Equal(0, lab.Clock.ArmedTimers);

        // The start ends when all three loads have ended, not before.
        var a = lab.Provider.Answer(Folder("a"));
        var c = lab.Provider.Answer(Folder("c"));
        a.SendFirstPage();
        c.SendFirstPage();
        b.End();
        a.End();
        await DashboardTestSupport.WaitUntilAsync(
            () => tabA.Repository is { Graph.IsComplete: true } && tabB.Repository is { Graph.IsComplete: true },
            "a's and b's loads to end");
        await DashboardTestSupport.SettleAsync();
        Assert.False(lab.Starting.IsCompleted);

        c.End();
        await lab.Starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.All(new[] { tabA, tabB, tabC }, tab => Assert.True(tab.Repository is { Graph: { Count: 1, IsComplete: true } }, $"{tab.Title} shows its graph."));
        Assert.Equal([Started("b", 2), Released("b", RestoreOrder.FirstPageReason, 0, 2)], lab.OrderLines);
    }

    [AvaloniaTheory]
    [InlineData(ActiveEnding.OpeningFails)]
    [InlineData(ActiveEnding.NoCommits)]
    [InlineData(ActiveEnding.RefsFail)]
    [InlineData(ActiveEnding.LoadFails)]
    public async Task When_the_active_tabs_opening_or_first_load_fails_or_ends_without_a_page_the_others_open(ActiveEnding ending)
    {
        using var lab = Lab.Of(1, "a", "b", "c");
        lab.Start();
        Assert.Equal(Folders("b"), lab.Provider.Opened);

        switch (ending)
        {
            case ActiveEnding.OpeningFails:
                lab.Provider.Fail(Folder("b"));
                break;
            case ActiveEnding.NoCommits:
                lab.Provider.Answer(Folder("b"), RestoreHistory.NoCommits).End();
                break;
            case ActiveEnding.RefsFail:
                lab.Provider.Answer(Folder("b"), RestoreHistory.RefsFail);
                break;
            case ActiveEnding.LoadFails:
                lab.Provider.Answer(Folder("b"), RestoreHistory.LoadFails);
                break;
        }

        await lab.WaitForOpenedAsync(3);
        Assert.Equal(Folders("b", "a", "c"), lab.Provider.Opened);
        Assert.Equal([Started("b", 2), Released("b", RestoreOrder.EndedReason, 0, 2)], lab.OrderLines);
    }

    [AvaloniaFact]
    public async Task Closing_the_active_tab_while_it_opens_opens_the_others()
    {
        using var lab = Lab.Of(1, "a", "b", "c");
        var (tabA, tabB, tabC) = (lab.Window.Tabs[0], lab.Window.Tabs[1], lab.Window.Tabs[2]);
        lab.Start();
        Assert.Equal(Folders("b"), lab.Provider.Opened);

        // The order hears of the close inside CloseTab, before the neighbour is activated.
        lab.Window.CloseTabCommand.Execute(tabB);

        Assert.Equal(Folders("b", "a", "c"), lab.Provider.Opened);
        Assert.Equal([Started("b", 2), Released("b", RestoreOrder.ClosedReason, 0, 2)], lab.OrderLines);
        Assert.Same(tabC, lab.Window.ActiveTab);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(1, lab.Provider.Opened.Count(path => path == Folder("c")));

        // A closed tab's opening ends when the provider answers.
        lab.Provider.Fail(Folder("b"));
        var a = lab.Provider.Answer(Folder("a"));
        var c = lab.Provider.Answer(Folder("c"));
        a.SendFirstPage();
        c.SendFirstPage();
        a.End();
        c.End();
        await lab.Starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal([tabA, tabC], lab.Window.Tabs);
    }

    [AvaloniaFact]
    public async Task After_5_s_on_the_restore_clock_the_others_open_without_a_first_page()
    {
        using var lab = Lab.Of(1, "a", "b", "c");
        var tabB = lab.Window.Tabs[1];
        lab.Start();
        Assert.Equal(Folders("b"), lab.Provider.Opened);
        var b = lab.Provider.Answer(Folder("b"));
        await DashboardTestSupport.WaitUntilAsync(() => tabB.Repository?.Refs is not null, "b's refs to be read");

        lab.Clock.Advance(RestoreOrder.LongestWait - TimeSpan.FromMilliseconds(1));
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b"), lab.Provider.Opened);
        Assert.Equal(1, lab.Clock.ArmedTimers);

        lab.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await lab.WaitForOpenedAsync(3);
        Assert.Equal(Folders("b", "a", "c"), lab.Provider.Opened);
        Assert.Equal([Started("b", 2), Released("b", RestoreOrder.WaitedReason, 5000, 2)], lab.OrderLines);

        // b's page after the release changes nothing.
        b.SendFirstPage();
        await DashboardTestSupport.WaitUntilAsync(() => tabB.Repository!.Graph.Count == 1, "b's first page");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b", "a", "c"), lab.Provider.Opened);
        Assert.Equal(2, lab.OrderLines.Count);
    }

    [AvaloniaFact]
    public async Task At_most_OpensAtOnce_open_at_once_and_each_freed_slot_opens_the_next_in_tab_order()
    {
        var names = Names(1 + RestoreOrder.OpensAtOnce + 2);
        using var lab = Lab.Of(0, names);
        var tabs = lab.Window.Tabs.ToList();
        lab.Start();
        Assert.Equal(Folders(names[0]), lab.Provider.Opened);

        var first = lab.Provider.Answer(Folder(names[0]));
        first.SendFirstPage();
        await lab.WaitForOpenedAsync(1 + RestoreOrder.OpensAtOnce);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders(names[..(1 + RestoreOrder.OpensAtOnce)]), lab.Provider.Opened);

        // One of them sends its first page: the next opens.
        var second = lab.Provider.Answer(Folder(names[1]));
        second.SendFirstPage();
        await lab.WaitForOpenedAsync(2 + RestoreOrder.OpensAtOnce);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders(names[..(2 + RestoreOrder.OpensAtOnce)]), lab.Provider.Opened);

        // Another fails: the last opens.
        lab.Provider.Fail(Folder(names[2]));
        await lab.WaitForOpenedAsync(3 + RestoreOrder.OpensAtOnce);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders(names), lab.Provider.Opened);

        // A third is closed, and its opening ends when the provider answers: nothing more opens.
        lab.Window.CloseTabCommand.Execute(tabs[3]);
        lab.Provider.Fail(Folder(names[3]));
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders(names), lab.Provider.Opened);

        first.End();
        second.End();
        foreach (var name in names[4..])
        {
            var fake = lab.Provider.Answer(Folder(name));
            fake.SendFirstPage();
            fake.End();
        }

        await lab.Starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal(Folders(names), lab.Provider.Opened);
    }

    [AvaloniaFact]
    public async Task No_slot_is_freed_by_the_clock()
    {
        var names = Names(1 + RestoreOrder.OpensAtOnce + 1);
        using var lab = Lab.Of(0, names);
        lab.Start();
        Assert.Equal(Folders(names[0]), lab.Provider.Opened);

        lab.Provider.Answer(Folder(names[0])).SendFirstPage();
        await lab.WaitForOpenedAsync(1 + RestoreOrder.OpensAtOnce);

        lab.Clock.Advance(RestoreOrder.LongestWait * 2);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders(names[..(1 + RestoreOrder.OpensAtOnce)]), lab.Provider.Opened);

        lab.Provider.Answer(Folder(names[1])).SendFirstPage();
        await lab.WaitForOpenedAsync(names.Length);
        Assert.Equal(Folders(names), lab.Provider.Opened);
    }

    [AvaloniaFact]
    public async Task A_waiting_tab_that_is_shown_opens_at_once_and_the_others_still_wait()
    {
        using var lab = Lab.Of(1, "a", "b", "c");
        var (tabB, tabC) = (lab.Window.Tabs[1], lab.Window.Tabs[2]);
        lab.Start();
        var b = lab.Provider.Answer(Folder("b"));
        await DashboardTestSupport.WaitUntilAsync(() => tabB.Repository?.Refs is not null, "b's refs to be read");

        lab.Window.ActivateTabCommand.Execute(tabC);

        Assert.Equal(Folders("b", "c"), lab.Provider.Opened);
        Assert.Equal([Started("b", 2), Shown("c")], lab.OrderLines);

        var c = lab.Provider.Answer(Folder("c"));
        await DashboardTestSupport.WaitUntilAsync(() => tabC.Repository?.Refs is not null, "c's refs to be read");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b", "c"), lab.Provider.Opened);

        // A shown tab's first page releases nobody.
        c.SendFirstPage();
        await DashboardTestSupport.WaitUntilAsync(() => tabC.Repository!.Graph.Count == 1, "c's first page");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b", "c"), lab.Provider.Opened);
        Assert.Equal([Started("b", 2), Shown("c")], lab.OrderLines);

        b.SendFirstPage();
        await lab.WaitForOpenedAsync(3);
        Assert.Equal(Folders("b", "c", "a"), lab.Provider.Opened);
        Assert.Equal([Started("b", 2), Shown("c"), Released("b", RestoreOrder.FirstPageReason, 0, 1)], lab.OrderLines);
    }

    [AvaloniaFact]
    public async Task A_waiting_tab_that_is_closed_never_opens_and_the_start_still_ends()
    {
        using var lab = Lab.Of(1, "a", "b", "c");
        var (tabA, tabB, tabC) = (lab.Window.Tabs[0], lab.Window.Tabs[1], lab.Window.Tabs[2]);
        lab.Start();

        lab.Window.CloseTabCommand.Execute(tabA);
        Assert.Equal([Started("b", 2), Dropped("a")], lab.OrderLines);

        var b = lab.Provider.Answer(Folder("b"));
        b.SendFirstPage();
        await lab.WaitForOpenedAsync(2);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b", "c"), lab.Provider.Opened);

        var c = lab.Provider.Answer(Folder("c"));
        c.SendFirstPage();
        b.End();
        c.End();
        await lab.Starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal(Folders("b", "c"), lab.Provider.Opened);
        Assert.Equal([tabB, tabC], lab.Window.Tabs);
        Assert.Equal([Started("b", 2), Dropped("a"), Released("b", RestoreOrder.FirstPageReason, 0, 1)], lab.OrderLines);
    }

    [AvaloniaTheory]
    [InlineData(WindowEnd.CloseAll)]
    [InlineData(WindowEnd.LifetimeCancelled)]
    public async Task Closing_the_window_while_tabs_wait_opens_none_of_them(WindowEnd end)
    {
        using var lab = Lab.Of(1, "a", "b", "c");
        lab.Start();
        Assert.Equal(Folders("b"), lab.Provider.Opened);

        if (end == WindowEnd.CloseAll)
        {
            lab.CloseWindow();
        }
        else
        {
            lab.CancelLifetime();
        }

        Assert.Equal([Started("b", 2), Stopped(2)], lab.OrderLines);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(Folders("b"), lab.Provider.Opened);

        // The start ends once the opened tab's opening ends.
        lab.Provider.Fail(Folder("b"));
        await lab.Starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal(Folders("b"), lab.Provider.Opened);
        Assert.Equal(0, lab.Clock.ArmedTimers);
    }

    [AvaloniaTheory]
    [InlineData(ActiveWithoutOrder.Dashboard)]
    [InlineData(ActiveWithoutOrder.EmptyTab)]
    public async Task With_a_dashboard_or_an_empty_tab_active_every_restored_tab_is_asked_for_at_once(ActiveWithoutOrder active)
    {
        var names = Names(6);
        TabState activeTab = active == ActiveWithoutOrder.Dashboard ? new TabState(null, "web") : new TabState(null);
        var workspaces = active == ActiveWithoutOrder.Dashboard
            ? new InMemoryWorkspaceStore(new WorkspaceList { Workspaces = [new Workspace("web", "Web", [])] })
            : null;
        using var lab = new Lab([activeTab, .. names.Select(name => new TabState(Folder(name)))], 0, workspaces);
        Assert.Equal(active == ActiveWithoutOrder.Dashboard, lab.Window.ActiveTab.IsDashboard);
        Assert.Null(lab.Window.ActiveTab.PendingPath);

        lab.Start();

        Assert.Equal(Folders(names), lab.Provider.Opened);
        await DashboardTestSupport.SettleAsync();
        Assert.Empty(lab.OrderLines);
    }

    [AvaloniaFact]
    public async Task A_lone_restored_tab_opens_as_today()
    {
        using var lab = Lab.Of(0, "a");

        lab.Start();

        Assert.Equal(Folders("a"), lab.Provider.Opened);
        await DashboardTestSupport.SettleAsync();
        Assert.Empty(lab.OrderLines);
        Assert.Equal(0, lab.Clock.ArmedTimers);
    }

    [AvaloniaFact]
    public void The_start_up_timeline_counts_every_restored_tab_before_any_opens()
    {
        var timeline = new RecordingTimeline();
        using var lab = new Lab([.. Names("a", "b", "c").Select(name => new TabState(Folder(name)))], 1, startup: timeline);
        timeline.Openings = () => lab.Provider.Opened.Count;

        lab.Start();

        Assert.Equal([(3, 0)], timeline.Restored);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_restored_tab_that_is_closed_is_let_go(bool loadedFirst)
    {
        // The order keeps no closed tab: it forgets a waiting tab at its close and an opened one when
        // its task ends, and lets go of the lifetime once every task has ended (14.3.1).
        using var lab = Lab.Of(1, "a", "b", "c");
        var closing = Folder("c");
        lab.Start();
        WeakReference? closed = null;
        if (!loadedFirst)
        {
            closed = CloseTab(lab.Window, 2);
        }

        await DashboardTestSupport.WaitUntilAsync(
            () =>
            {
                lab.Provider.EndHeld(path => loadedFirst && path == closing);
                return lab.Starting.IsCompleted;
            },
            "the start to end");
        await lab.Starting;
        if (loadedFirst)
        {
            Assert.True(lab.Window.Tabs[2].Repository is { Graph.IsComplete: true }, "c has loaded its history.");
            closed = CloseTab(lab.Window, 2);
        }

        await DashboardTestSupport.SettleAsync();
        for (var i = 0; i < 5 && closed!.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await DashboardTestSupport.SettleAsync();
        }

        Assert.False(closed!.IsAlive);
    }

    /// <summary>Closes the window's tab at <paramref name="index"/> and returns a weak reference to it, holding none itself.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CloseTab(MainWindowViewModel window, int index)
    {
        var tab = window.Tabs[index];
        window.CloseTabCommand.Execute(tab);
        return new WeakReference(tab);
    }

    private static string Folder(string name) => Path.Combine(Path.GetTempPath(), "restored", name);

    private static string[] Folders(params string[] names) => [.. names.Select(Folder)];

    private static string[] Names(params string[] names) => names;

    private static string[] Names(int count) => [.. Enumerable.Range(0, count).Select(index => string.Create(CultureInfo.InvariantCulture, $"t{index}"))];

    private static string Started(string first, int others) =>
        string.Format(CultureInfo.InvariantCulture, RestoreOrder.StartedFormat, first, others, RestoreOrder.OpensAtOnce);

    private static string Released(string first, string reason, long milliseconds, int waiting) =>
        string.Format(CultureInfo.InvariantCulture, RestoreOrder.ReleasedFormat, first, reason, milliseconds, waiting, RestoreOrder.OpensAtOnce);

    private static string Shown(string tab) => string.Format(CultureInfo.InvariantCulture, RestoreOrder.ShownFormat, tab);

    private static string Dropped(string tab) => string.Format(CultureInfo.InvariantCulture, RestoreOrder.DroppedFormat, tab);

    private static string Stopped(int waiting) => string.Format(CultureInfo.InvariantCulture, RestoreOrder.StoppedFormat, waiting);

    /// <summary>
    /// The window over fakes (as <c>CheckGitAgainTests</c>' lab): the session's tabs as given, git
    /// found at once, the provider <see cref="HeldRestores"/>, a <see cref="ManualTimeProvider"/> as
    /// the restore clock alone (every other time of the window runs on the system's clock), and one
    /// log for the services and the window, which the order writes through.
    /// </summary>
    private sealed class Lab : IDisposable
    {
        private readonly CancellationTokenSource _lifetime = new();
        private bool _closed;

        public Lab(IReadOnlyList<TabState> tabs, int active, IWorkspaceStore? workspaces = null, IStartupTimeline? startup = null)
        {
            Session = new DashboardTestSupport.MemorySession(new SessionState { Tabs = [.. tabs], ActiveTab = active });
            Services = new TabServices(
                Provider,
                new FakeFolderPicker(),
                Settings,
                Session,
                new DateDisplay(TimeZoneInfo.Utc),
                Log,
                workspaces: workspaces,
                startup: startup,
                restoreClock: Clock);
            Window = new MainWindowViewModel(Settings, new MainWindowViewModelTests.FakeThemes(), _ => Task.FromResult(GitFound), Log, Services, Session);
        }

        public HeldRestores Provider { get; } = new();

        public ManualTimeProvider Clock { get; } = new();

        public RecordingLog Log { get; } = new();

        public MainWindowViewModelTests.FakeSettings Settings { get; } = new();

        public DashboardTestSupport.MemorySession Session { get; }

        public TabServices Services { get; }

        public MainWindowViewModel Window { get; }

        /// <summary>The window's <c>InitializeAsync</c>, once <see cref="Start"/> has called it.</summary>
        public Task Starting { get; private set; } = Task.CompletedTask;

        /// <summary>The order's lines in the log, in order.</summary>
        public IReadOnlyList<string> OrderLines => [.. Log.Messages.Where(message => message.StartsWith(OrderPrefix, StringComparison.Ordinal))];

        /// <summary>The session's tabs restored in the given order, the one at <paramref name="active"/> active.</summary>
        public static Lab Of(int active, params string[] names) => new([.. names.Select(name => new TabState(Folder(name)))], active);

        public void Start() => Starting = Window.InitializeAsync(_lifetime.Token);

        public void CancelLifetime() => _lifetime.Cancel();

        public void CloseWindow()
        {
            _closed = true;
            Window.CloseAll();
        }

        public Task WaitForOpenedAsync(int count) =>
            DashboardTestSupport.WaitUntilAsync(() => Provider.Opened.Count >= count, string.Create(CultureInfo.InvariantCulture, $"{count} openings to be asked for"));

        public void Dispose()
        {
            if (!_closed)
            {
                Window.CloseAll();
            }

            Provider.EndHeld(_ => false);
            _lifetime.Dispose();
        }
    }

    /// <summary>A start-up timeline that records each count of restored tabs with the openings asked for by then.</summary>
    private sealed class RecordingTimeline : IStartupTimeline
    {
        public Func<int> Openings { get; set; } = () => 0;

        public List<(int Count, int OpenedThen)> Restored { get; } = [];

        public void WindowOpened()
        {
        }

        public void GitFound()
        {
        }

        public void TabsRestored(int count) => Restored.Add((count, Openings()));

        public void FirstRows(string repositoryName)
        {
        }
    }
}

/// <summary>What a <see cref="RestoreFake"/>'s history does.</summary>
internal enum RestoreHistory
{
    /// <summary><c>main</c> at one commit, handed at <see cref="RestoreFake.SendFirstPage"/>, the load ending at <see cref="RestoreFake.End"/>.</summary>
    OneCommit,

    /// <summary>No commits: no page, the load ending at <see cref="RestoreFake.End"/>.</summary>
    NoCommits,

    /// <summary>The refs read throws.</summary>
    RefsFail,

    /// <summary>The load throws once the refs are read.</summary>
    LoadFails,
}

/// <summary>
/// A provider that records the folder of each open, in call order, and holds the open until the
/// test answers it with a <see cref="RestoreFake"/> (<see cref="Answer"/>) or fails it
/// (<see cref="Fail"/>). Its record and its openings are kept under a lock: an order's continuation
/// may ask for an opening beside the test.
/// </summary>
internal sealed class HeldRestores : IRepositoryProvider
{
    private readonly Lock _gate = new();
    private readonly List<string> _opened = [];
    private readonly Dictionary<string, TaskCompletionSource<IGitRepository>> _openings = new(StringComparer.Ordinal);

    /// <summary>The folders asked for, in call order.</summary>
    public IReadOnlyList<string> Opened
    {
        get
        {
            lock (_gate)
            {
                return [.. _opened];
            }
        }
    }

    public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var opening = new TaskCompletionSource<IGitRepository>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _openings[path] = opening;
            _opened.Add(path);
        }

        return opening.Task;
    }

    /// <summary>Answers the held open of <paramref name="path"/> with a fake over that folder.</summary>
    public RestoreFake Answer(string path, RestoreHistory history = RestoreHistory.OneCommit)
    {
        var fake = new RestoreFake(path, history);
        Opening(path).SetResult(fake);
        return fake;
    }

    /// <summary>Fails the held open of <paramref name="path"/>, as a folder that is gone does.</summary>
    public void Fail(string path) => Opening(path).SetException(new IOException(string.Create(CultureInfo.InvariantCulture, $"{path} is gone.")));

    /// <summary>
    /// Ends every open held now: answers those <paramref name="answer"/> picks with a fake whose one
    /// commit is sent and whose load ends, and fails the others.
    /// </summary>
    public void EndHeld(Func<string, bool> answer)
    {
        List<(string Path, TaskCompletionSource<IGitRepository> Opening)> held;
        lock (_gate)
        {
            held = [.. _openings.Where(entry => !entry.Value.Task.IsCompleted).Select(entry => (entry.Key, entry.Value))];
        }

        foreach (var (path, opening) in held)
        {
            if (answer(path))
            {
                var fake = new RestoreFake(path);
                fake.SendFirstPage();
                fake.End();
                opening.TrySetResult(fake);
            }
            else
            {
                opening.TrySetException(new IOException(string.Create(CultureInfo.InvariantCulture, $"{path} is gone.")));
            }
        }
    }

    public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IGitRepository> CloneAsync(
        string url,
        string destination,
        IProgress<CloneProgress>? progress,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string?>? environment = null) => throw new NotSupportedException();

    private TaskCompletionSource<IGitRepository> Opening(string path)
    {
        lock (_gate)
        {
            return _openings[path];
        }
    }
}

/// <summary>
/// A repository over a folder that is never made: <c>main</c> at one commit by Demo Author, handed
/// at <see cref="SendFirstPage"/>, the load ending at <see cref="End"/>; or what its
/// <see cref="RestoreHistory"/> says. Its status is clean and its watcher quiet.
/// </summary>
internal sealed class RestoreFake(string folder, RestoreHistory history = RestoreHistory.OneCommit) : FakeRepositoryBase
{
    private static readonly DateTimeOffset Date = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TaskCompletionSource _page = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _end = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The one commit of <see cref="RestoreHistory.OneCommit"/>.</summary>
    public static CommitInfo Commit { get; } = new(new string('1', 40), [], "Demo Author", "demo@example.com", Date, Date, "Initial commit");

    public override string WorkingDirectory { get; } = folder;

    public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

    public override string CommonDirectory => GitDirectory;

    public override string Name => Path.GetFileName(WorkingDirectory);

    /// <summary>Hands the commit, now or when the load gets there.</summary>
    public void SendFirstPage() => _page.TrySetResult();

    /// <summary>Ends the load, now or when it gets there.</summary>
    public void End() => _end.TrySetResult();

    public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => history switch
    {
        RestoreHistory.RefsFail => Task.FromException<RepoRefs>(new IOException("The refs could not be read.")),
        RestoreHistory.NoCommits => Task.FromResult(new RepoRefs(new HeadState("main", null), [], [], [])),
        _ => Task.FromResult(new RepoRefs(
            new HeadState("main", Commit.Sha),
            [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Commit.Sha, IsHead: true)],
            [],
            [])),
    };

    public override async Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
    {
        if (history == RestoreHistory.LoadFails)
        {
            throw new IOException("The history could not be read.");
        }

        if (history != RestoreHistory.NoCommits)
        {
            await _page.Task.WaitAsync(cancellationToken);
            onPage([Commit]);
        }

        await _end.Task.WaitAsync(cancellationToken);
    }

    public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public override IRepositoryWatcher CreateWatcher() => new QuietWatcher();

    private sealed class QuietWatcher : IRepositoryWatcher
    {
        public event EventHandler<RepositoryChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public IDisposable Pause() => this;

        public void Dispose()
        {
        }
    }
}
