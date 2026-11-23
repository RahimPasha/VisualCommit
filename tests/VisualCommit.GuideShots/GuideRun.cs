using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Hosting;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.GitLab;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;

namespace VisualCommit.GuideShots;

/// <summary>
/// How a shot starts the app (<see cref="GuideCamera.ShootAsync"/>): what the session file and the
/// workspaces hold, a state made in the copy before the start, the search for git, GitHub, and the
/// window's size.
/// </summary>
public sealed record GuideStart
{
    /// <summary>One tab on the weather station.</summary>
    public static GuideStart WeatherStation { get; } = new() { Tabs = scenario => [new TabState(scenario.WeatherStation)] };

    /// <summary>One empty tab, the welcome page, with the two demo repositories in its recent list.</summary>
    public static GuideStart Welcome { get; } = new() { Recent = DemoRecent };

    /// <summary>One tab on the weather station in a wider window, for a diff or a page in the graph's place, whose lines are long.</summary>
    public static GuideStart WeatherStationWide { get; } = WeatherStation with { Width = 1440, Height = 800 };

    /// <summary>The weather station with GitHub served by the fake and Alex signed in.</summary>
    public static GuideStart WeatherStationOnGitHub { get; } = WeatherStation with { GitHub = true };

    /// <summary>The session's tabs; none: one empty tab.</summary>
    public Func<GuideScenario, IReadOnlyList<TabState>>? Tabs { get; init; }

    /// <summary>The recent list of the welcome page.</summary>
    public Func<GuideScenario, IReadOnlyList<RecentRepository>>? Recent { get; init; }

    /// <summary>The workspaces file's contents.</summary>
    public Func<GuideScenario, WorkspaceList>? Workspaces { get; init; }

    /// <summary>What is done in the copy before the app starts, such as a merge stopped on a conflict.</summary>
    public Func<GuideScenario, Task>? Prepare { get; init; }

    /// <summary>The search for git: a fake's answers for a first-run shot; null for the machine's git.</summary>
    public Func<IGitSetupProbe>? Probe { get; init; }

    /// <summary>Where the prompt broker looks for its helper; null for where it is.</summary>
    public string? AskPassHelper { get; init; }

    /// <summary>Whether GitHub is served by the fake (<see cref="GuideRun.GitHub"/>).</summary>
    public bool GitHub { get; init; }

    /// <summary>With <see cref="GitHub"/>: whether Alex is signed in at the start.</summary>
    public bool SignedIn { get; init; } = true;

    public int Width { get; init; } = 1100;

    public int Height { get; init; } = 700;

    /// <summary>The weather station and the recipe box, opened on the demo's last day.</summary>
    public static IReadOnlyList<RecentRepository> DemoRecent(GuideScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return
        [
            new RecentRepository(scenario.WeatherStation, GuideScenario.WeatherStationName, new DateTimeOffset(2025, 12, 19, 16, 0, 0, TimeSpan.Zero)),
            new RecentRepository(scenario.RecipeBox, GuideScenario.RecipeBoxName, new DateTimeOffset(2025, 12, 18, 9, 0, 0, TimeSpan.Zero)),
        ];
    }
}

/// <summary>One start of the app for a shot: the demo's copy, the driver and, when served, the fake of GitHub.</summary>
public sealed class GuideRun : IAsyncDisposable
{
    /// <summary>The demo's workspace for the dashboard, by a fixed id.</summary>
    public const string WorkspaceId = "a3f1c2d4e5b64789a0b1c2d3e4f5a6b7";

    public const string WorkspaceName = "Home projects";

    /// <summary>The version of git every picture shows: none newer than the git that runs the shots, none older than the app's floor.</summary>
    public const string ShownGitVersion = "2.43.0";

    private readonly bool _opensRepository;

    private GuideRun(GuideScenario scenario, ShellDriver app, FakeGitHub? gitHub, bool opensRepository)
    {
        Scenario = scenario;
        App = app;
        GitHub = gitHub;
        _opensRepository = opensRepository;
    }

    public GuideScenario Scenario { get; }

    public ShellDriver App { get; }

    /// <summary>The fake of GitHub, in process, when the start asked for it.</summary>
    public FakeGitHub? GitHub { get; }

    /// <summary>The demo's workspace: the weather station and the recipe box.</summary>
    public static WorkspaceList DemoWorkspace(GuideScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new WorkspaceList
        {
            Workspaces = [new Workspace(WorkspaceId, WorkspaceName, [new WorkspaceRepository(scenario.WeatherStation), new WorkspaceRepository(scenario.RecipeBox)])],
        };
    }

    internal static async Task<GuideRun> StartAsync(GuideScenario scenario, GuideStart start)
    {
        if (start.Prepare is not null)
        {
            await start.Prepare(scenario);
        }

        var data = scenario.DataDirectory;
        var tabs = start.Tabs?.Invoke(scenario) ?? [];
        if (start.Tabs is not null || start.Recent is not null)
        {
            ShellDriver.WriteSession(data, new SessionState { Tabs = [.. tabs], Recent = [.. start.Recent?.Invoke(scenario) ?? []] });
        }

        if (start.Workspaces is not null)
        {
            var list = start.Workspaces(scenario);
            new JsonWorkspaceStore(new AppPaths(data).WorkspacesFile).Update(_ => list);
        }

        FakeGitHub? gitHub = null;
        HostingOptions? hosting = null;
        string? token = null;
        if (start.GitHub)
        {
            (gitHub, token) = await ServeGitHubAsync(scenario);
            // GitLab and Azure DevOps on at their services' addresses, so that the Accounts section
            // lists them, with their handlers left out: every request to them is refused, and no
            // shot signs in to them.
            hosting = new HostingOptions(GitHubEndpoints.GitHubCom, () => new ApiPrefixHandler(gitHub.CreateHandler()))
            {
                AzureDevOps = AzureDevOpsEndpoints.Service,
                GitLab = GitLabEndpoints.GitLabCom,
            };
        }

        ShellDriver app;
        try
        {
            app = ShellDriver.Start(data, start.Width, start.Height, hosting: hosting, gitProbe: start.Probe?.Invoke() ?? MachineGitShownAsUsual(), askPassHelper: start.AskPassHelper);
        }
        catch
        {
            if (gitHub is not null)
            {
                await gitHub.DisposeAsync();
            }

            throw;
        }

        var run = new GuideRun(scenario, app, gitHub, tabs.Count > 0 && tabs[0].RepositoryPath is not null && tabs[0].WorkspaceId is null);
        try
        {
            if (token is not null && start.SignedIn)
            {
                await app.SignInAsync(token);
            }
        }
        catch
        {
            await run.DisposeAsync();
            throw;
        }

        return run;
    }

    /// <summary>
    /// The machine's git, reported at its usual path, Git for Windows' or <c>/usr/bin/git</c>, and
    /// as <see cref="ShownGitVersion"/>, so that the status bar reads the same on every machine.
    /// </summary>
    public static IGitSetupProbe MachineGitShownAsUsual() =>
        OperatingSystem.IsWindows()
            ? new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(FakeGitSetupProbe.WindowsGit, GitSource.Path, ShownGitVersion))
            : new FakeGitSetupProbe(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Ubuntu), FakeGitAnswer.RealAt(FakeGitSetupProbe.UnixGit, GitSource.Path, ShownGitVersion));

    /// <summary>
    /// Waits until the active tab shows what it opened with: a repository's graph with its
    /// watchers quiet, a dashboard's first read, or the start-up's end; then rests the pointer.
    /// </summary>
    public async Task WaitUntilShownAsync()
    {
        await App.WaitUntilReadyAsync();
        var tab = App.Session.Shell.ActiveTab;
        if (tab.Dashboard is not null)
        {
            await App.WaitForDashboardReadAsync();
        }
        else if (_opensRepository)
        {
            await App.WaitForRepositoryAsync();
            await App.WaitForWatchersQuietAsync();
        }

        App.Settle();
        App.MoveToNeutral();
    }

    public async ValueTask DisposeAsync()
    {
        App.Dispose();
        if (GitHub is not null)
        {
            await GitHub.DisposeAsync();
        }
    }

    /// <summary>
    /// The fake of GitHub in process on the demo's bare repository (D451): Alex
    /// (<see cref="GuideScenario.Owner"/>) and Sam (<see cref="GuideScenario.SecondLogin"/>), the
    /// weather station's repository, Alex's token, and two open pull requests.
    /// </summary>
    private static async Task<(FakeGitHub GitHub, string Token)> ServeGitHubAsync(GuideScenario scenario)
    {
        var fake = FakeGitHub.InProcess(Path.Combine(scenario.Remotes, "github"));
        try
        {
            fake.AddUser(GuideScenario.Owner, 4101, GuideScenario.Alex.Name);
            fake.AddUser(GuideScenario.SecondLogin, 4102, GuideScenario.Sam.Name);
            var token = fake.AddToken(GuideScenario.Owner);
            (await fake.AddRepositoryAsync(GuideScenario.Owner, GuideScenario.WeatherStationName, sourceRepository: scenario.WeatherStationBare, description: "Reads the garden's sensors and shows the weather.")).UpdatedAt = new DateTimeOffset(2025, 12, 19, 16, 0, 0, TimeSpan.Zero);
            fake.AddPullRequest(GuideScenario.Owner, GuideScenario.WeatherStationName, "Predict rain from the pressure", GuideScenario.Owner, "feature/forecast", "main", number: 12);
            fake.AddPullRequest(GuideScenario.Owner, GuideScenario.WeatherStationName, "Convert wind speeds", GuideScenario.SecondLogin, "feature/units", "main", number: 9).State = "closed";
            fake.LinkEmail(GuideScenario.Alex.Email, GuideScenario.Owner);
            fake.LinkEmail(GuideScenario.Sam.Email, GuideScenario.SecondLogin);
            return (fake, token);
        }
        catch
        {
            await fake.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Serves github.com's API through the in-process fake: the app asks <c>https://api.github.com/…</c>,
    /// which the fake serves under <c>/api/v3/</c> only, as a GitHub Enterprise server does; so the
    /// prefix is added to each request to that host.
    /// </summary>
    private sealed class ApiPrefixHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is { } uri && uri.Host == GitHubEndpoints.GitHubCom.ApiBase.Host && !uri.AbsolutePath.StartsWith("/api/v3/", StringComparison.Ordinal))
            {
                request.RequestUri = new UriBuilder(uri) { Path = "/api/v3" + uri.AbsolutePath }.Uri;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
