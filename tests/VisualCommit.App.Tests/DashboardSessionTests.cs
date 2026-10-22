using System.ComponentModel;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Dashboards in the session (docs/phase-6-design.md 1.11, 2.11; D46, D189): the restore puts the
/// dashboards first and the other tabs after, each in its saved order, follows the saved active
/// tab, drops a dashboard whose workspace is gone and a second of one workspace; the save keeps a
/// dashboard by its workspace's id; the active tab's index follows the strip.
/// </summary>
public class DashboardSessionTests
{
    private static readonly GitDetection GitFound = new(GitAvailability.Available, "/usr/bin/git", new GitVersion(2, 47, 1), null);

    private static readonly string Repository = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "not-mounted", "api");

    [Fact]
    public void Dashboards_come_first_and_the_other_tabs_after_each_in_their_saved_order()
    {
        var shell = Create(
            new SessionState
            {
                Tabs = [new TabState(Repository), new TabState(null, "w1"), new TabState(null), new TabState(null, "w2")],
                ActiveTab = 0,
            },
            out _);

        Assert.Equal(["W one", "W two", "api", "New tab"], shell.Tabs.Select(tab => tab.Title));
        Assert.Equal([true, true, false, false], shell.Tabs.Select(tab => tab.IsDashboard));
        Assert.Equal(["w1", "w2", null, null], shell.Tabs.Select(tab => tab.SavedWorkspaceId));
        Assert.Equal(["w1", "w2"], shell.Tabs.Take(2).Select(tab => tab.Dashboard!.WorkspaceId));

        // The active tab follows its tab: the repository's, now third.
        Assert.Same(shell.Tabs[2], shell.ActiveTab);
        Assert.Equal(2, shell.ActiveTabIndex);
        Assert.True(shell.Tabs[2].IsActive);

        // A restored dashboard is no pending repository; the repository's tab is.
        Assert.All(shell.Tabs.Take(2), tab => Assert.Null(tab.PendingPath));
        Assert.Equal(Repository, shell.Tabs[2].PendingPath);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 1)]
    [InlineData(2, 3)]
    public void The_saved_active_tab_is_mapped_to_its_tab(int saved, int restored)
    {
        var shell = Create(
            new SessionState
            {
                Tabs = [new TabState(Repository), new TabState(null, "w1"), new TabState(null), new TabState(null, "w2")],
                ActiveTab = saved,
            },
            out _);

        Assert.Equal(restored, shell.ActiveTabIndex);
        Assert.Same(shell.Tabs[restored], shell.ActiveTab);
    }

    [Fact]
    public void A_dashboard_of_a_workspace_the_store_lacks_and_a_second_of_one_workspace_are_dropped()
    {
        var shell = Create(
            new SessionState
            {
                Tabs = [new TabState(null, "gone"), new TabState(null, "w1"), new TabState(Repository), new TabState(null, "w1")],
                ActiveTab = 0,
            },
            out _);

        Assert.Equal(["W one", "api"], shell.Tabs.Select(tab => tab.Title));

        // The saved active tab was dropped: the first tab is active.
        Assert.Same(shell.Tabs[0], shell.ActiveTab);
    }

    [Fact]
    public void A_path_beside_a_workspace_id_is_ignored()
    {
        var shell = Create(new SessionState { Tabs = [new TabState(Repository, "w1")] }, out _);

        var tab = Assert.Single(shell.Tabs);
        Assert.True(tab.IsDashboard);
        Assert.Null(tab.PendingPath);
        Assert.Null(tab.SavedPath);
        Assert.Equal("W one", tab.Title);
    }

    [Fact]
    public void When_every_tab_is_dropped_one_empty_tab_is_left()
    {
        var shell = Create(new SessionState { Tabs = [new TabState(null, "gone"), new TabState(null, "also-gone")], ActiveTab = 1 }, out var session);

        var tab = Assert.Single(shell.Tabs);
        Assert.False(tab.IsDashboard);
        Assert.Equal("New tab", tab.Title);
        Assert.Same(tab, shell.ActiveTab);

        // The restore saves nothing; the first save drops what the restore dropped.
        Assert.Equal(2, session.Current.Tabs.Count);
        shell.NewTabCommand.Execute(null);
        Assert.Equal([new TabState(null), new TabState(null)], session.Current.Tabs);
    }

    [Fact]
    public void The_save_keeps_a_dashboard_by_its_workspaces_id_and_drops_what_the_restore_dropped()
    {
        var shell = Create(
            new SessionState
            {
                Tabs = [new TabState(Repository), new TabState(null, "w2"), new TabState(null, "gone"), new TabState(null, "w1"), new TabState(null)],
                ActiveTab = 3,
            },
            out var session);

        Assert.Same(shell.Tabs[1], shell.ActiveTab);
        shell.NewTabCommand.Execute(null);

        Assert.Equal(
            [new TabState(null, "w2"), new TabState(null, "w1"), new TabState(Repository), new TabState(null), new TabState(null)],
            session.Current.Tabs);
        Assert.Equal(4, session.Current.ActiveTab);
    }

    [Fact]
    public async Task The_window_does_not_open_a_restored_dashboard_as_a_repository()
    {
        var provider = new CountingRepositories();
        var shell = Create(new SessionState { Tabs = [new TabState(null, "w1"), new TabState(Repository)] }, out _, provider);

        await shell.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal([Repository], provider.Opened);
        Assert.True(shell.Tabs[0].IsDashboard);
        Assert.Equal("W one", shell.Tabs[0].Title);
    }

    [Fact]
    public void The_active_tabs_index_follows_the_active_tab_and_the_tabs()
    {
        var shell = Create(new SessionState { Tabs = [new TabState(null, "w1"), new TabState(null)], ActiveTab = 1 }, out _);
        var changes = new List<string?>();
        ((INotifyPropertyChanged)shell).PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.Equal(1, shell.ActiveTabIndex);

        shell.NewTabCommand.Execute(null);
        Assert.Equal(2, shell.ActiveTabIndex);
        Assert.Contains(nameof(MainWindowViewModel.ActiveTabIndex), changes);

        changes.Clear();
        shell.ActivateTabCommand.Execute(shell.Tabs[0]);
        Assert.Equal(0, shell.ActiveTabIndex);
        Assert.Contains(nameof(MainWindowViewModel.ActiveTabIndex), changes);

        // A tab moved or closed before the active one moves its index too.
        shell.ActivateTabCommand.Execute(shell.Tabs[2]);
        changes.Clear();
        shell.CloseTabCommand.Execute(shell.Tabs[1]);
        Assert.Equal(1, shell.ActiveTabIndex);
        Assert.Contains(nameof(MainWindowViewModel.ActiveTabIndex), changes);
        changes.Clear();
        shell.Tabs.Move(1, 0);
        Assert.Equal(0, shell.ActiveTabIndex);
        Assert.Contains(nameof(MainWindowViewModel.ActiveTabIndex), changes);
    }

    [Fact]
    public void The_window_tells_the_workspace_code_which_dashboards_are_open()
    {
        var shell = Create(new SessionState { Tabs = [new TabState(null, "w1"), new TabState(null)] }, out _);

        Assert.True(shell.HasOpenDashboard("w1"));
        Assert.False(shell.HasOpenDashboard("w2"));
        Assert.Null(shell.OpenedWorkingDirectoryOf("w2", Repository));
    }

    private static MainWindowViewModel Create(SessionState state, out MemorySession session, IRepositoryProvider? provider = null)
    {
        session = new MemorySession(state);
        var settings = new MainWindowViewModelTests.FakeSettings();
        var workspaces = new InMemoryWorkspaceStore(new WorkspaceList
        {
            Workspaces = [new Workspace("w1", "W one", []), new Workspace("w2", "W two", [])],
        });
        var tabs = new TabServices(provider ?? new CountingRepositories(), new NoFolders(), settings, session, new DateDisplay(TimeZoneInfo.Utc), workspaces: workspaces);
        return new MainWindowViewModel(settings, new MainWindowViewModelTests.FakeThemes(), _ => Task.FromResult(GitFound), NullAppLog.Instance, tabs, session);
    }

    private sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    /// <summary>Records the folders the window asked to open; none is a repository.</summary>
    private sealed class CountingRepositories : IRepositoryProvider
    {
        public List<string> Opened { get; } = [];

        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
        {
            Opened.Add(path);
            return Task.FromException<IGitRepository>(new NotARepositoryException(path));
        }

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
