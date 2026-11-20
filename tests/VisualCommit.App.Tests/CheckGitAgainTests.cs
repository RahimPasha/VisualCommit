using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// "Check again" in the window (docs/phase-8a-design.md 1.2, 5.3; D423, ruling 5), with no session:
/// <c>Looking for Git...</c> while it searches; then the status bar, the helper looked for again and
/// the screen; once git is found, the merge tools' lookup forgotten, the tab whose restore failed
/// reopened, the empty tabs' errors from the missing git dropped and the merge tools asked again.
/// While the helper is missing git works, so only the helper is looked for. A search that throws
/// shows the screen's "not working" state without a path.
/// </summary>
public class CheckGitAgainTests
{
    private static readonly GitDetection Missing = new(GitAvailability.NotFound, null, null, null);
    private static readonly GitDetection Found = new(GitAvailability.Available, @"C:\Program Files\Git\cmd\git.exe", new GitVersion(2, 47, 1), null);

    [AvaloniaFact]
    public async Task Check_again_finds_git_reopens_the_failed_tab_drops_the_missing_gits_errors_and_asks_the_tools_again()
    {
        using var lab = new Lab(Missing, Found);
        var api = lab.Provider.Add("api");
        await lab.StartAsync(api.WorkingDirectory, null, null);
        var (restored, empty, other) = (lab.Window.Tabs[0], lab.Window.Tabs[1], lab.Window.Tabs[2]);
        empty.Welcome.ShowError(GitAccess.GitNotFound, fromMissingGit: true);
        other.Welcome.ShowError("Not a repository.");

        Assert.Equal(api.WorkingDirectory, restored.FailedPath);
        Assert.Equal(RepoTabViewModel.EmptyTitle, restored.Title);
        Assert.Equal(GitSetupState.Missing, lab.Window.GitSetup.State);
        Assert.True(restored.ShowsGitSetup);
        Assert.False(restored.ShowsWelcome);
        Assert.Equal("Git not found", lab.Window.GitStatusText);
        await DashboardTestSupport.WaitUntilAsync(() => lab.Order.Contains("tools"), "the start's merge-tool lookup");

        // The reopening held: the merge tools are not asked again until it has ended.
        lab.GitFound = true;
        var reopening = new TaskCompletionSource();
        lab.Provider.Gate = reopening;
        var refused = lab.OpensOf(api.WorkingDirectory);
        var checking = lab.Window.CheckGitAgainCommand.ExecuteAsync(null);
        await DashboardTestSupport.WaitUntilAsync(() => lab.OpensOf(api.WorkingDirectory) > refused, "the failed tab's reopening to start");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(["tools", "forget"], lab.Order);
        reopening.SetResult();
        await checking;

        Assert.Equal(2, lab.Searches);
        Assert.Same(Found, lab.Window.Git);
        Assert.Equal("Git 2.47.1", lab.Window.GitStatusText);
        Assert.Equal(@"C:\Program Files\Git\cmd\git.exe, found on the PATH", lab.Window.GitStatusToolTip);
        Assert.Equal(GitSetupState.Found, lab.Window.GitSetup.State);
        Assert.NotNull(restored.Repository);
        Assert.Equal("api", restored.Title);
        Assert.Null(restored.FailedPath);
        Assert.Null(restored.Welcome.ErrorText);
        Assert.Null(empty.Welcome.ErrorText);
        Assert.True(empty.ShowsWelcome);
        Assert.Equal("Not a repository.", other.Welcome.ErrorText);
        Assert.Empty(lab.Services.Recent.Items);

        // The start's lookup, then the lookup forgotten, then the merge tools asked once more, after the reopening.
        Assert.Equal(["tools", "forget", "tools"], lab.Order);
    }

    [AvaloniaFact]
    public async Task While_it_searches_the_status_bar_and_the_button_say_so()
    {
        var held = new TaskCompletionSource<GitDetection>();
        using var lab = new Lab(Missing);
        await lab.StartAsync([null]);
        lab.Next = held.Task;

        var checking = lab.Window.CheckGitAgainCommand.ExecuteAsync(null);

        Assert.Equal("Looking for Git...", lab.Window.GitStatusText);
        Assert.Null(lab.Window.GitStatusToolTip);
        Assert.True(lab.Window.CheckGitAgainCommand.IsRunning);
        Assert.False(lab.Window.CheckGitAgainCommand.CanExecute(null));
        Assert.Equal("Checking…", lab.Window.GitSetup.CheckAgainText);

        held.SetResult(Missing);
        await checking;

        Assert.Equal("Git not found", lab.Window.GitStatusText);
        Assert.Equal("Check again", lab.Window.GitSetup.CheckAgainText);
        Assert.Equal(GitSetupState.Missing, lab.Window.GitSetup.State);
    }

    [AvaloniaFact]
    public async Task Check_again_that_finds_no_git_reopens_nothing_and_asks_no_tools()
    {
        using var lab = new Lab(Missing);
        var api = lab.Provider.Add("api");
        await lab.StartAsync(api.WorkingDirectory);
        await DashboardTestSupport.WaitUntilAsync(() => lab.Order.Contains("tools"), "the start's merge-tool lookup");

        await lab.Window.CheckGitAgainCommand.ExecuteAsync(null);

        Assert.Equal(2, lab.Searches);
        Assert.Equal(api.WorkingDirectory, lab.Window.Tabs[0].FailedPath);
        Assert.Equal(["tools"], lab.Order);
        Assert.True(lab.Window.Tabs[0].ShowsGitSetup);
    }

    [AvaloniaFact]
    public async Task Check_again_that_finds_git_reads_the_active_dashboard_again_and_one_that_does_not_reads_nothing()
    {
        using var lab = new Lab(Missing, Missing, Found);
        var web = lab.Provider.Add("web");
        lab.Workspaces.Update(_ => new WorkspaceList { Workspaces = [new Workspace("web", "Web", [new WorkspaceRepository(web.WorkingDirectory)])] });
        await lab.StartWithAsync([new TabState(null, "web")]);
        Assert.True(lab.Window.ActiveTab.IsDashboard);
        await DashboardTestSupport.WaitUntilAsync(() => lab.OpensOf(web.WorkingDirectory) > 0, "the dashboard's first read");
        await DashboardTestSupport.SettleAsync();
        var read = lab.OpensOf(web.WorkingDirectory);

        await lab.Window.CheckGitAgainCommand.ExecuteAsync(null);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(read, lab.OpensOf(web.WorkingDirectory));

        lab.GitFound = true;
        await lab.Window.CheckGitAgainCommand.ExecuteAsync(null);
        await DashboardTestSupport.WaitUntilAsync(() => lab.OpensOf(web.WorkingDirectory) > read, "the dashboard's read after git was found");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(read + 1, lab.OpensOf(web.WorkingDirectory));
        Assert.Same(Found, lab.Window.Git);
    }

    [AvaloniaFact]
    public async Task While_the_helper_is_missing_check_again_looks_for_the_helper_only_and_keeps_git()
    {
        using var lab = new Lab(Found);
        lab.Prompts.Missing = @"C:\Users\demo\AppData\Local\Programs\VisualCommit\visualcommit-askpass.exe";
        await lab.StartAsync([null]);

        Assert.Equal(GitSetupState.HelperMissing, lab.Window.GitSetup.State);
        Assert.Equal("Git 2.47.1 (helper missing)", lab.Window.GitStatusText);
        Assert.Equal(1, lab.Prompts.Checks);

        lab.Prompts.Missing = null;
        await lab.Window.CheckGitAgainCommand.ExecuteAsync(null);

        Assert.Equal(1, lab.Searches);
        Assert.Equal(2, lab.Prompts.Checks);
        Assert.Same(Found, lab.Window.Git);
        Assert.Equal(GitSetupState.Found, lab.Window.GitSetup.State);
        Assert.Equal("Git 2.47.1", lab.Window.GitStatusText);
        Assert.False(lab.Window.Tabs[0].ShowsGitSetup);
    }

    [AvaloniaFact]
    public async Task A_found_git_with_its_helper_there_asks_nothing_about_the_helper()
    {
        using var lab = new Lab(Found);
        await lab.StartAsync([null]);

        Assert.Equal(0, lab.Prompts.Checks);
        Assert.Equal(GitSetupState.Found, lab.Window.GitSetup.State);
    }

    [AvaloniaFact]
    public async Task A_search_that_throws_shows_not_working_without_a_path_its_message_as_gits_words()
    {
        using var lab = new Lab(Missing);
        lab.Next = Task.FromException<GitDetection>(new InvalidOperationException("boom"));
        await lab.StartAsync([null]);

        Assert.Null(lab.Window.Git);
        Assert.Equal("Git is not working", lab.Window.GitStatusText);
        Assert.Equal(GitSetupState.NotWorking, lab.Window.GitSetup.State);
        Assert.Equal("boom", lab.Window.GitSetup.GitSaid);
        Assert.False(lab.Window.GitSetup.ShowsPath);
    }

    [AvaloniaFact]
    public async Task Set_up_git_shows_the_screen_again_in_the_first_tab_that_shows_it_or_a_new_tab()
    {
        using var lab = new Lab(Found);
        lab.Prompts.Missing = "C:/missing/visualcommit-askpass.exe";
        await lab.StartAsync([null]);
        lab.Window.GitSetup.ContinueCommand.Execute(null);
        Assert.False(lab.Window.Tabs[0].ShowsGitSetup);

        lab.Window.ShowGitSetupCommand.Execute(null);

        Assert.True(lab.Window.Tabs[0].ShowsGitSetup);
        Assert.Same(lab.Window.Tabs[0], lab.Window.ActiveTab);
        Assert.Single(lab.Window.Tabs);
    }

    /// <summary>The window over fakes: a search that answers from a queue, a provider that refuses while git is missing and records every open, a broker whose helper a test hides, the tools and git's settings recording their order.</summary>
    private sealed class Lab : IDisposable
    {
        private readonly Queue<GitDetection> _answers;
        private readonly ConcurrentQueue<string> _opens = new();
        private int _searches;

        public Lab(params GitDetection[] answers)
        {
            _answers = new Queue<GitDetection>(answers);
            Provider = new FakeProvider();
            Session = new DashboardTestSupport.MemorySession();
            var tools = new ToolCatalogues(
                ToolCatalogues.Fixed.FindEditors,
                _ =>
                {
                    Order.Add("tools");
                    return Task.FromResult(ToolCatalogues.FixedMergeTools);
                },
                ToolCatalogues.Fixed.PathExtensions);
            Services = new TabServices(
                new GatedProvider(Provider, () => GitFound, _opens.Enqueue),
                new FakeFolderPicker(),
                Settings,
                Session,
                new DateDisplay(TimeZoneInfo.Utc),
                Log,
                prompts: Prompts,
                gitSettings: new ForgettingConfiguration(Order),
                tools: tools,
                workspaces: Workspaces);
        }

        public FakeProvider Provider { get; }

        public InMemoryWorkspaceStore Workspaces { get; } = new();

        public DashboardTestSupport.MemorySession Session { get; }

        public MainWindowViewModelTests.FakeSettings Settings { get; } = new();

        public RecordingLog Log { get; } = new();

        public HelperBroker Prompts { get; } = new();

        public TabServices Services { get; }

        public List<string> Order { get; } = [];

        public bool GitFound { get; set; }

        /// <summary>The next search's answer, in place of the queue's.</summary>
        public Task<GitDetection>? Next { get; set; }

        public int Searches => _searches;

        public MainWindowViewModel Window { get; private set; } = null!;

        /// <summary>Writes the session's tabs (a folder, or null for an empty tab), makes the window and runs its start.</summary>
        public Task StartAsync(params string?[] tabs) => StartWithAsync([.. tabs.Select(path => new TabState(path))]);

        /// <summary>Writes the session's tabs, the first active, makes the window and runs its start.</summary>
        public async Task StartWithAsync(IReadOnlyList<TabState> tabs)
        {
            Session.Update(state => state with { Tabs = [.. tabs], ActiveTab = 0 });
            Window = new MainWindowViewModel(Settings, new MainWindowViewModelTests.FakeThemes(), SearchAsync, Log, Services, Session);
            await Window.InitializeAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>How many opens the provider was asked for at <paramref name="path"/>, refused ones among them.</summary>
        public int OpensOf(string path) => _opens.Count(open => string.Equals(open, path, StringComparison.OrdinalIgnoreCase));

        public void Dispose()
        {
            Window?.CloseAll();
            Provider.Dispose();
        }

        private Task<GitDetection> SearchAsync(CancellationToken cancellationToken)
        {
            _searches++;
            if (Next is { } next)
            {
                Next = null;
                return next;
            }

            return Task.FromResult(_answers.Count > 1 ? _answers.Dequeue() : _answers.Peek());
        }
    }

    /// <summary>Opens through the fake provider while git is found; refuses as the app's provider does while it is missing. Tells <paramref name="opening"/> of every open asked for.</summary>
    private sealed class GatedProvider(FakeProvider inner, Func<bool> gitFound, Action<string> opening) : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
        {
            opening(path);
            return gitFound() ? inner.OpenAsync(path, cancellationToken) : Task.FromException<IGitRepository>(new GitUnavailableException(GitAccess.GitNotFound));
        }

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    /// <summary>A broker whose helper the test hides or shows, counting each time it is looked for again.</summary>
    private sealed class HelperBroker : IGitPromptBroker
    {
        private int _checks;

        public string? Missing { get; set; }

        public int Checks => Volatile.Read(ref _checks);

        public string? MissingHelperPath => Missing;

        public PromptScope Begin(string title, string repositoryName, PromptMode mode, string? operationId = null, PromptGroup? group = null, IReadOnlyList<CredentialBinding>? bindings = null) =>
            NullGitPromptBroker.Instance.Begin(title, repositoryName, mode, operationId, group, bindings);

        public string? CheckHelper()
        {
            Interlocked.Increment(ref _checks);
            return Missing;
        }
    }

    /// <summary>Git's settings that record when the merge tools' lookup is forgotten; nothing else is asked of them here.</summary>
    private sealed class ForgettingConfiguration(List<string> order) : IGitConfiguration
    {
        public string GlobalFile => string.Empty;

        public string XdgFile => string.Empty;

        public void ForgetToolLookup() => order.Add("forget");

        public Task<GitConfigRead> ReadAsync(string? workingTree, CancellationToken cancellationToken = default) => Task.FromResult(new GitConfigRead([], false));

        public Task<IReadOnlyList<GitConfigValue>> ReadIncludedAsync(GitConfigValue directive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GitConfigValue>>([]);

        public Task<GitIdentityRead> ReadIdentityAsync(string workingTree, CancellationToken cancellationToken = default) => Task.FromResult(new GitIdentityRead(null, null, null));

        public string? Variable(string name) => null;

        public Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MergeToolHelp> ReadToolHelpAsync(CancellationToken cancellationToken = default) => Task.FromResult(MergeToolHelp.FailedLookup);

        public void Dispose()
        {
        }
    }
}
