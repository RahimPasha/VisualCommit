using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>Tests of the tab model (D45) and the recent list (D42, D46), without a UI and without git.</summary>
public class RepoTabsTests
{
    private static readonly GitDetection GitFound = new(GitAvailability.Available, "/usr/bin/git", new GitVersion(2, 47, 1), null);

    [Fact]
    public void A_first_start_has_one_new_tab_without_a_close_button()
    {
        var viewModel = Create(new MemorySession());

        var tab = Assert.Single(viewModel.Tabs);
        Assert.Same(tab, viewModel.ActiveTab);
        Assert.True(tab.IsActive);
        Assert.Equal("New tab", tab.Title);
        Assert.False(tab.CanClose);
        Assert.True(tab.ShowsWelcome);
        Assert.Equal("No repository", tab.BranchText);
    }

    [Fact]
    public void Plus_opens_a_new_active_tab_and_closing_it_leaves_one()
    {
        var session = new MemorySession();
        var viewModel = Create(session);

        viewModel.NewTabCommand.Execute(null);

        Assert.Equal(2, viewModel.Tabs.Count);
        Assert.Same(viewModel.Tabs[1], viewModel.ActiveTab);
        Assert.False(viewModel.Tabs[0].IsActive);
        Assert.All(viewModel.Tabs, tab => Assert.True(tab.CanClose));
        Assert.Equal(1, session.Current.ActiveTab);
        Assert.Equal(2, session.Current.Tabs.Count);

        viewModel.CloseTabCommand.Execute(viewModel.Tabs[1]);

        var left = Assert.Single(viewModel.Tabs);
        Assert.Same(left, viewModel.ActiveTab);
        Assert.False(left.CanClose);

        viewModel.CloseTabCommand.Execute(left);

        var fresh = Assert.Single(viewModel.Tabs);
        Assert.NotSame(left, fresh);
        Assert.Same(fresh, viewModel.ActiveTab);
    }

    [Fact]
    public void Tabs_of_the_last_session_are_shown_under_their_folder_names_before_they_open()
    {
        var session = new MemorySession(new SessionState
        {
            Tabs = [new TabState(Path.Combine("repos", "graph")), new TabState(null), new TabState(Path.Combine("repos", "linear"))],
            ActiveTab = 2,
        });

        var viewModel = Create(session);

        Assert.Equal(["graph", "New tab", "linear"], viewModel.Tabs.Select(tab => tab.Title));
        Assert.Same(viewModel.Tabs[2], viewModel.ActiveTab);
        Assert.False(viewModel.Tabs[0].ShowsWelcome);
        Assert.True(viewModel.Tabs[1].ShowsWelcome);
        Assert.All(viewModel.Tabs, tab => Assert.True(tab.CanClose));
    }

    [Fact]
    public void An_active_tab_out_of_range_falls_back_to_the_first()
    {
        var viewModel = Create(new MemorySession(new SessionState { Tabs = [new TabState(null), new TabState(null)], ActiveTab = 7 }));

        Assert.Same(viewModel.Tabs[0], viewModel.ActiveTab);
    }

    [Fact]
    public void The_recent_list_puts_the_newest_first_drops_duplicates_and_keeps_ten()
    {
        var session = new MemorySession();
        var recent = new RecentRepositories(session, TimeProvider.System);

        for (var i = 1; i <= 12; i++)
        {
            recent.Add(Path.GetFullPath($"repo{i}"), $"repo{i}");
        }

        recent.Add(Path.GetFullPath("repo5"), "repo5");

        Assert.Equal(10, recent.Items.Count);
        Assert.Equal("repo5", recent.Items[0].Name);
        Assert.Equal("repo12", recent.Items[1].Name);
        Assert.Single(recent.Items, item => item.Name == "repo5");
        Assert.Equal(recent.Items.Select(item => item.Path), session.Current.Recent.Select(item => item.Path));
    }

    [Theory]
    [InlineData("https://github.com/RahimPasha/VisualCommit.git", "VisualCommit")]
    [InlineData("https://example.com/group/project/", "project")]
    [InlineData("git@github.com:owner/tool.git", "tool")]
    [InlineData("file:///D:/repos/large-history-v1", "large-history-v1")]
    [InlineData(@"C:\repos\sample.git", "sample")]
    [InlineData("", "")]
    public void The_clone_folder_name_comes_from_the_last_part_of_the_url(string url, string name)
    {
        Assert.Equal(name, WelcomeViewModel.NameFromUrl(url));
    }

    [Fact]
    public void The_clone_folder_name_follows_the_url_until_it_is_typed_into()
    {
        var viewModel = Create(new MemorySession());
        var welcome = viewModel.ActiveTab.Welcome;

        welcome.CloneUrl = "https://example.com/first.git";
        Assert.Equal("first", welcome.CloneName);

        welcome.CloneName = "mine";
        welcome.CloneUrl = "https://example.com/second.git";
        Assert.Equal("mine", welcome.CloneName);
    }

    private static MainWindowViewModel Create(ISessionStore session)
    {
        var settings = new MainWindowViewModelTests.FakeSettings();
        var tabs = new TabServices(new NoRepositories(), new NoFolders(), settings, session, new DateDisplay(TimeZoneInfo.Utc));
        return new MainWindowViewModel(settings, new MainWindowViewModelTests.FakeThemes(), _ => Task.FromResult(GitFound), NullAppLog.Instance, tabs, session);
    }

    private sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotARepositoryException(path);

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoFolders : VisualCommit.App.Services.IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
