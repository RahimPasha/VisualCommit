using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Hosting.GitLab;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace VisualCommit.LiveHostingTests;

/// <summary>Runs <see cref="GitLabLiveTests"/>' methods in 15.5's order (stage 1; stage 2's Check_3 takes its place after Check_2).</summary>
public sealed class GitLabLiveOrder : ITestCaseOrderer
{
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(GitLabLiveTests.Check_1_the_sandbox_and_its_leftovers),
        nameof(GitLabLiveTests.Check_1b_probes),
        nameof(GitLabLiveTests.Check_2_the_providers_and_the_session),
    ];

    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase => LiveSteps.Order(testCases, Methods);
}

/// <summary>
/// The live command against gitlab.com, stage 1 (docs/phase-7b-design.md 15.5; D346, D349, D364,
/// D371, D373, D375): the sandbox and its leftovers, the probes beside the fake, and the app's provider
/// and session signing in, listing, cloning, pushing and creating a draft merge request. Every
/// request passes <see cref="GitLabReach"/>; every note goes through <see cref="GitLabLiveRun"/>.
/// </summary>
[TestCaseOrderer(typeof(GitLabLiveOrder))]
public sealed class GitLabLiveTests(GitLabLiveRun run) : IClassFixture<GitLabLiveRun>
{
    private const HostingProvider Provider = HostingProvider.GitLab;

    private const string MissingBranch = LiveLeftovers.Prefix + "no-such-branch";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    private static LiveGitLabSandbox Sandbox => (LiveGitLabSandbox)LiveSettings.Require(Provider);

    /// <summary>
    /// Check_1 (15.5): the variables gone; <c>GET user</c> (200, the namespace's first segment as its
    /// login); the sandbox (200, private, D326, its default branch); then the leftovers (D324): each
    /// opened merge request from a <c>visualcommit-live/</c> branch of the sandbox closed, then deleted
    /// (a 403 noted "closed, kept", g2 G1.6), and each such branch deleted (a 404: gone, g2 G1.7).
    /// </summary>
    [Fact]
    public async Task Check_1_the_sandbox_and_its_leftovers()
    {
        var sandbox = Sandbox;
        var store = new LiveRequestLog.Store();
        var reach = LiveReach.GitLab();
        var step = "1, the variables";
        var problems = new List<string>();
        try
        {
            LiveSteps.AssertVariablesGone();
            run.Note($"Check_1. The sandbox {sandbox}; the token is {LiveSettings.KindOf(Provider, sandbox.Token)}.");
            using var api = new GitLabLiveApi(sandbox, reach, store, run.Screener!, run.Withheld);

            step = "1, the identity";
            var user = await api.UserAsync(LiveCredential.Token, Cancelled);
            run.Check(user.Status == 200, () => api.Describe(user, "GET user") + ": 401 means the token is wrong or expired.");
            run.Note($"GET user: {user}; its members: {(user.Json is { ValueKind: JsonValueKind.Object } json ? string.Join(", ", json.EnumerateObject().Select(member => member.Name)) : user.Shape)}.");
            run.Check(string.Equals(user.Member("username"), sandbox.Login, StringComparison.OrdinalIgnoreCase), () => $"The token's user is not {sandbox.Login}, the namespace's first segment.");

            step = "1, the sandbox";
            await ReadSandboxAsync(api);

            step = "2, the leftovers";
            var opened = await api.OpenedMergeRequestsAsync(Cancelled);
            var leftovers = opened.Where(LiveProviderLeftovers.IsLeftover).ToList();
            run.Note($"Opened merge requests: {Show(opened.Count)}, {Show(leftovers.Count)} of them from {LiveLeftovers.Prefix} branches of the sandbox.");
            foreach (var request in leftovers)
            {
                await CloseAndDeleteAsync(api, request.Iid, problems, CancellationToken.None);
            }

            var branches = await api.BranchesAsync(LiveLeftovers.Prefix, Cancelled);
            foreach (var branch in branches)
            {
                await DeleteBranchAsync(api, branch, problems, CancellationToken.None);
            }

            run.Note($"Leftovers of a stopped run: {Show(leftovers.Count)} merge requests, {Show(branches.Count)} branches.");
        }
        catch (Exception ex)
        {
            run.Note($"Check_1 failed in step {step}: {ex.GetType().Name}: {ex.Message}");
            LiveSteps.Finish(run, store, reach, problems, run.FailureOf(ex, step));
            return;
        }

        LiveSteps.Finish(run, store, reach, problems, null);
    }

    /// <summary>
    /// Check_1's probes (15.7), in a method of their own as Azure DevOps' are: <c>user</c>;
    /// <c>personal_access_tokens/self</c>, whose 403 <c>insufficient_granular_scope</c> is expected and
    /// noted (r6 2.2); a made-up <c>glpat-</c> value on <c>GET user</c> (D373), which must get 401; the
    /// projects with <c>permissions</c>; the merge requests; <c>ratelimit-*</c> across a 304. Each
    /// beside the fake's answer; every request Bearer.
    /// </summary>
    [Fact]
    public async Task Check_1b_probes()
    {
        var sandbox = Sandbox;
        var store = new LiveRequestLog.Store();
        var reach = LiveReach.GitLab();
        var stamp = LiveSteps.Stamp();
        var step = "1b, the probes";
        var problems = new List<string>();
        try
        {
            using var api = new GitLabLiveApi(sandbox, reach, store, run.Screener!, run.Withheld);
            run.Note("Check_1b. GitLab's probes.");
            await using var fake = await StartFakeAsync(sandbox, LiveLeftovers.Prefix + stamp);
            var user = await ProbeAsync(api, fake, "GET user", a => a.UserAsync(LiveCredential.Token, Cancelled));
            if (user is not { Status: 200 })
            {
                problems.Add($"GET user answered {user?.ToString() ?? "nothing"}, not 200 (15.7's premise).");
            }

            var self = await ProbeAsync(api, fake, "GET personal_access_tokens/self (403 insufficient_granular_scope expected, r6 2.2)", a => a.TokenSelfAsync(Cancelled));
            run.Note($"personal_access_tokens/self: {(self is { Status: 403 } && self.Text.Contains("insufficient_granular_scope", StringComparison.Ordinal) ? "403 insufficient_granular_scope, as expected" : "another answer: " + (self?.ToString() ?? "none"))}.");
            var madeUp = LiveCredential.MadeUp(LiveMadeUp.GitLabShaped(sandbox.Token), "a made-up glpat- value of 26 characters");
            var refused = await ProbeAsync(api, fake, $"GET user with {madeUp}", a => a.UserAsync(madeUp, Cancelled));
            if (refused is not { Status: 401 })
            {
                problems.Add($"GET user with {madeUp} answered {refused?.ToString() ?? "nothing"}, not 401 (D373).");
            }

            var projects = await ProbeAsync(api, fake, "GET projects with permissions (one row)", a => a.ProjectsAsync(Cancelled));
            run.Note($"The projects' first row {(projects?.Json is { ValueKind: JsonValueKind.Array } rows && rows.GetArrayLength() > 0 && rows[0].TryGetProperty("permissions", out _) ? "has" : "has no")} permissions.");
            await ProbeAsync(api, fake, "GET the sandbox's opened merge requests", a => a.MergeRequestsAnswerAsync(Cancelled));
            if (user?.Header("ETag") is { } etag)
            {
                var again = await ProbeAsync(api, fake, "GET user with If-None-Match", a => a.UserAsync(LiveCredential.Token, Cancelled, a == api ? etag : "\"none\""));
                run.Note($"ratelimit across the 304: before {Rate(user)}; after {(again is null ? "no answer" : Rate(again))}.");
            }
            else
            {
                run.Note("GET user sent no ETag: no 304 to probe.");
            }

            if (store.Snapshot().Any(request => request.Form is not (LiveCredentialForm.Bearer or LiveCredentialForm.None)))
            {
                problems.Add("A request to GitLab was not Bearer (15.7's premise).");
            }
        }
        catch (Exception ex)
        {
            run.Note($"Check_1b failed in step {step}: {ex.GetType().Name}: {ex.Message}");
            LiveSteps.Finish(run, store, reach, problems, run.FailureOf(ex, step));
            return;
        }

        LiveSteps.Finish(run, store, reach, problems, null);
    }

    /// <summary>
    /// Check_2 (15.5): the app with GitLab on signs in, lists the sandbox, clones it, loads its merge
    /// requests, pushes D325's commit and creates a draft; the create is probed (<c>draft</c>,
    /// <c>references</c>, a duplicate, a missing branch, nothing ahead) and everything cleaned up.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_2_the_providers_and_the_session()
    {
        var sandbox = Sandbox;
        var stamp = LiveSteps.Stamp();
        var branch = LiveLeftovers.Prefix + stamp;
        var title = "VisualCommit live test " + stamp;
        var appRequests = new LiveRequestLog.Store();
        var ownRequests = new LiveRequestLog.Store();
        var reach = LiveReach.GitLab();
        using var api = new GitLabLiveApi(sandbox, reach, ownRequests, run.Screener!, run.Withheld);
        var hosting = HostingOptions.Off with
        {
            GitLab = GitLabEndpoints.GitLabCom,
            CreateGitLabHandler = () => new LiveRequestLog(HostingOptions.DefaultHandler(GitLabEndpoints.GitLabCom.ApiBase)(), appRequests, reach),
        };
        using var app = new LiveApp(run, sandbox, hosting);
        var step = "1, the start";
        var problems = new List<string>();
        ExceptionDispatchInfo? failure = null;
        try
        {
            run.Note($"Check_2. The branch {branch}.");
            var project = await ReadSandboxAsync(api);
            await app.StartAsync(appRequests);

            step = "2, the sign-in";
            var account = await app.SignInAsync(null, appRequests, problems);
            run.Check(string.Equals(account.Login, sandbox.Login, StringComparison.OrdinalIgnoreCase), () => $"The account's login is {account.Login}, not {sandbox.Login}.");

            step = "3, the list";
            var row = await app.ListAsync(sandbox.FullName);

            step = "4, the clone";
            var opened = await app.CloneAsync(row.CloneUrl.AbsoluteUri);

            step = "5, the load";
            await app.LoadAsync(opened);

            step = "6, the push";
            await app.PushAsync(opened, branch, stamp, title);

            step = "7, the create";
            var request = await app.CreateAsync(opened, title, branch, project.DefaultBranch);

            step = "8, the probes";
            api.Limit = LiveSteps.ProbeLimit;
            await ProbeCreateAsync(api, request.Number, branch, project.DefaultBranch, title, problems);
        }
        catch (Exception ex)
        {
            failure = run.FailureOf(ex, step);
            run.Note($"Check_2 failed in step {step}: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            api.Limit = LiveServiceApi.CallLimit;
            await CleanUpAsync(api, branch, problems);
            app.Close();
        }

        app.Tripwires(problems);
        LiveSteps.Finish(run, appRequests, reach, problems, failure, ownRequests, signInStarted: app.SignInStarted);
    }

    /// <summary>The sandbox project, once per run: 200, private (D326), its default branch.</summary>
    private async Task<LiveRepository> ReadSandboxAsync(GitLabLiveApi api)
    {
        if (run.Repository is { } known)
        {
            return known;
        }

        var answer = await api.ProjectAsync(Cancelled);
        run.Check(answer.Status == 200, () => api.Describe(answer, "the sandbox project") + ".");
        var visibility = answer.Member("visibility") ?? "not given";
        var branch = answer.Member("default_branch") ?? string.Empty;
        run.Note($"The sandbox project: {answer}; visibility {visibility}, default branch {branch}, permissions {(answer.Member("permissions") is null ? "absent" : "present")}.");
        run.Check(string.Equals(visibility, "private", StringComparison.Ordinal), () => $"The sandbox is {visibility}: D326 wants it private.");
        run.Check(branch.Length > 0, () => "The sandbox has no default branch.");
        var repository = new LiveRepository(api.Sandbox.FullName, true, visibility, branch, true);
        run.Repository = repository;
        return repository;
    }

    private async Task<LiveGitLabFake?> StartFakeAsync(LiveGitLabSandbox sandbox, string branch)
    {
        try
        {
            return await LiveGitLabFake.StartAsync(sandbox.Project, branch, Cancelled);
        }
        catch (Exception ex) when (!Cancelled.IsCancellationRequested)
        {
            run.Note($"The fake of GitLab did not start, so no probe has its answer beside the service's: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private async Task<LiveServiceAnswer?> ProbeAsync(GitLabLiveApi api, LiveGitLabFake? fake, string what, Func<GitLabLiveApi, Task<LiveServiceAnswer>> probe)
    {
        LiveServiceAnswer answer;
        try
        {
            answer = await probe(api);
        }
        catch (LiveServiceException ex)
        {
            run.Note($"GitLab's answer to {what}: none: {ex.Message}");
            return null;
        }

        var beside = await LiveSteps.FakeBesideAsync(answer, fake is null ? null : () => probe(fake.Api), [api.Sandbox.Namespace, api.Sandbox.Project], fake?.Names ?? []);
        run.Note($"GitLab's answer to {what}: {answer}; {answer.HeadersForNotes}; the fake: {beside}. {answer.BodyForNotes}");
        return answer;
    }

    private static string Rate(LiveServiceAnswer answer) =>
        string.Join(", ", answer.Headers.Where(header => header.Key.StartsWith("ratelimit", StringComparison.OrdinalIgnoreCase)).Select(header => $"{header.Key.ToLowerInvariant()} {header.Value}").DefaultIfEmpty("no ratelimit header"))
        + $" ({answer})";

    /// <summary>Check_2's probes of the create (15.7): <c>draft</c> and <c>references</c>; a duplicate (409), a missing branch (400 naming <c>source_branch</c>), nothing ahead.</summary>
    private async Task ProbeCreateAsync(GitLabLiveApi api, int iid, string branch, string target, string title, List<string> problems)
    {
        var read = await api.MergeRequestAsync(iid, Cancelled);
        run.Note($"The created !{Show(iid)}: {read}; draft {read.Member("draft") ?? "none"}, title starts Draft: {(read.Member("title")?.StartsWith("Draft: ", StringComparison.Ordinal) == true ? "yes" : "no")}, references {read.Member("references") ?? "none"}.");
        if (read.Member("draft") != "true")
        {
            problems.Add($"The created !{Show(iid)}'s draft is {read.Member("draft") ?? "absent"}, not true, though its title starts Draft: (15.7's premise).");
        }

        var duplicate = await api.CreateAsync(branch, target, GitLabProvider.TitleFor(title, true), Cancelled);
        run.Note($"The duplicate create: {duplicate}; 409 expected. {duplicate.BodyForNotes}");
        if (!LiveProviderPremises.IsGitLabDuplicate(duplicate))
        {
            problems.Add($"The duplicate create answered {duplicate}, not 409.");
        }

        var missing = await api.CreateAsync(MissingBranch, target, title, Cancelled);
        run.Note($"The create from a missing branch: {missing}; 400 naming source_branch expected. {missing.BodyForNotes}");
        if (!LiveProviderPremises.IsGitLabMissingBranch(missing))
        {
            problems.Add($"The create from a missing branch answered {missing}, not 400 naming source_branch (7.3).");
        }

        var even = await api.CreateAsync(target, target, title, Cancelled);
        run.Note($"A create with nothing ahead (the default branch into itself): {even}. {even.BodyForNotes}");
    }

    /// <summary>15.5 step 9: the run's merge requests closed and deleted, then its branch deleted.</summary>
    private async Task CleanUpAsync(GitLabLiveApi api, string branch, List<string> problems)
    {
        try
        {
            foreach (var request in (await api.OpenedMergeRequestsAsync(CancellationToken.None)).Where(request => request.SourceBranch == branch && !request.FromFork))
            {
                await CloseAndDeleteAsync(api, request.Iid, problems, CancellationToken.None);
            }

            if ((await api.BranchesAsync(LiveLeftovers.Prefix, CancellationToken.None)).Contains(branch))
            {
                await DeleteBranchAsync(api, branch, problems, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            problems.Add($"The clean-up failed: {ex.GetType().Name}: {api.Screen(ex.Message)}");
        }
    }

    private async Task CloseAndDeleteAsync(GitLabLiveApi api, int iid, List<string> problems, CancellationToken ct)
    {
        var closed = await api.CloseAsync(iid, ct);
        var deleted = await api.DeleteMergeRequestAsync(iid, ct);
        run.Note($"Merge request !{Show(iid)}: closed {closed}, deleted {deleted}{(deleted.Status == 403 ? " (closed, kept, g2 G1.6)" : string.Empty)}.");
        if (closed.Status != 200)
        {
            problems.Add(api.Describe(closed, $"closing !{Show(iid)}"));
        }

        if (deleted.Status is not (204 or 403))
        {
            problems.Add(api.Describe(deleted, $"deleting !{Show(iid)}"));
        }
    }

    private async Task DeleteBranchAsync(GitLabLiveApi api, string branch, List<string> problems, CancellationToken ct)
    {
        var deleted = await api.DeleteBranchAsync(branch, ct);
        run.Note($"Branch {branch} deleted: {deleted}{(deleted.Status == 404 ? " (gone already, g2 G1.7)" : string.Empty)}.");
        if (deleted.Status is not (204 or 404))
        {
            problems.Add(api.Describe(deleted, $"deleting {branch}"));
        }
    }

    private static string Show(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The app as both providers' Check_2 and Azure DevOps' Check_4 drive it (15.5): started headless
/// with the given hosting options, signed in through the session (1.9), the sandbox listed, cloned
/// through the clone form and the app's helper with 7a 14.4 step 4's checks, its requests loaded,
/// D325's commit pushed with "Push {branch}…", a draft created through the repository's hosting;
/// then the tripwires. Every message it throws or notes goes through the run's screen.
/// </summary>
internal sealed class LiveApp : IDisposable
{
    private static readonly TimeSpan NetworkStep = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CloneLimit = TimeSpan.FromSeconds(120);

    private readonly LiveRun _run;
    private readonly LiveHostSandbox _sandbox;
    private readonly HostingOptions _hosting;
    private readonly TempDirectory _data = new("live-data");
    private readonly TempDirectory _clones = new("live-clone");
    private readonly TempDirectory _home = new("live-home");
    private readonly GitRunner _git = new(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The live command needs git."));
    private ShellDriver? _app;
    private string? _clone;
    private bool _closed;

    public LiveApp(LiveRun run, LiveHostSandbox sandbox, HostingOptions hosting)
    {
        _run = run;
        _sandbox = sandbox;
        _hosting = hosting;
    }

    /// <summary>When the sign-in began: no request may come before it (15.5 step 10).</summary>
    public DateTimeOffset? SignInStarted { get; private set; }

    private HostingProvider Provider => _sandbox.Provider;

    private ShellDriver App => _app ?? throw new InvalidOperationException("The app has not started.");

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    private Dictionary<string, string?> Session => new(GitIsolation.SessionGitEnvironment(_data.Path));

    /// <summary>Step 1: the app with the provider on and no request sent.</summary>
    public async Task StartAsync(LiveRequestLog.Store requests)
    {
        _app = ShellDriver.StartWithTabs(_data.Path, [null], hosting: _hosting);
        await _app.WaitUntilReadyAsync();
        var session = _app.Session.Services.Hosting;
        _run.Check(session.IsOn(Provider), () => $"{HostingProviders.DisplayName(Provider)} is off in the app.");
        var line = $" [INF] {HostingProviders.DisplayName(Provider)}: {session.ProviderOf(Provider)!.Host}.";
        await _app.WaitForAsync(() => _app.LogLines().Any(logged => logged.EndsWith(line, StringComparison.Ordinal)), $"the log's \"{line.Trim()}\"");
        _run.Check(requests.Count == 0, () => "The app sent a request before the sign-in: " + string.Join("; ", requests.Snapshot().Select(request => request.Describe())));
    }

    /// <summary>Step 2: the sign-in through the session; only its reads leave, each 200 and each in the log's store; <c>accounts.json</c> as 1.8.</summary>
    public async Task<HostingAccount> SignInAsync(string? organization, LiveRequestLog.Store requests, List<string> problems)
    {
        SignInStarted = DateTimeOffset.UtcNow;
        try
        {
            await App.SignInAsync(Provider, _sandbox.Token, organization, Cancelled);
        }
        catch (InvalidOperationException ex)
        {
            throw new XunitException(_run.Screen(ex.Message));
        }

        var signIn = requests.Snapshot();
        _run.Note($"The sign-in: {string.Join("; ", signIn.Select(request => request.Describe()))}.");
        _run.Check(signIn.Count > 0, () => "The sign-in's requests bypassed LiveRequestLog, so the guard did not judge them (15.5 step 2).");
        if (signIn.Any(request => request.Status != 200))
        {
            problems.Add("A sign-in read did not answer 200: " + string.Join("; ", signIn.Where(request => request.Status != 200).Select(request => request.Describe())));
        }

        var account = App.Session.Services.Hosting.AccountOf(Provider)!.Account;
        NoteAccountsFile(problems);
        return account;
    }

    /// <summary>Step 3: the session lists the sandbox; its row.</summary>
    public async Task<RepositoryInfo> ListAsync(string fullName)
    {
        var listed = await App.Session.Services.Hosting.ListRepositoriesAsync(Provider, Cancelled).WaitAsync(NetworkStep, Cancelled);
        if (listed.Value is not { } list)
        {
            throw new XunitException(_run.Screen($"The list failed: {listed.Failure?.Kind}: {listed.Failure?.Message} (request {listed.Failure?.RequestId ?? "none"})."));
        }

        _run.Note($"The session's list: {list.Repositories.Count.ToString(CultureInfo.InvariantCulture)} rows, more exist: {(list.MoreExist ? "yes" : "no")}.");
        return list.Repositories.FirstOrDefault(row => string.Equals(row.Repository.FullName, fullName, StringComparison.OrdinalIgnoreCase))
            ?? throw new XunitException(_run.Screen($"The list does not hold {fullName}."));
    }

    /// <summary>Step 4: the clone through the clone form's URL box and the app's helper (D255), with 7a 14.4 step 4's checks; the clone's authors learned by the screen.</summary>
    public async Task<RepositoryViewModel> CloneAsync(string url)
    {
        var tab = App.Session.Shell.ActiveTab;
        App.Click(App.FindByAutomationId("CloneRepoButton"));
        await App.WaitForAsync(() => App.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
        App.ReplaceText("CloneUrlBox", url);
        App.ReplaceText("CloneParentBox", _clones.Path);
        var clone = Path.Combine(_clones.Path, App.Find<Avalonia.Controls.TextBox>("CloneNameBox").Text ?? "clone");
        var mark = App.LogMark();
        App.Click(App.FindByAutomationId("CloneStartButton"));
        await WaitWatchingAsync(() => tab.Repository is not null || (!tab.Welcome.IsCloning && tab.Welcome.HasError), "the clone to open its repository", CloneLimit);
        if (tab.Repository is null)
        {
            throw new XunitException(_run.Screen("The clone failed: " + tab.Welcome.ErrorText));
        }

        var opened = await App.WaitForRepositoryAsync(CloneLimit);
        _clone = clone;
        _run.Check(
            App.LogLinesSince(mark).Any(line => line.Contains(": Clone: git's credential get for ", StringComparison.Ordinal) && line.EndsWith(" answered from the account.", StringComparison.Ordinal)),
            () => "No line of the log since the click on Clone says git's credential get was answered from the account.");
        _run.Check(string.Equals(url, (await GitOkAsync(clone, Session, "config", "--get", "remote.origin.url")).StandardOutput.Trim(), StringComparison.Ordinal), () => "The clone's remote.origin.url is not the URL typed.");
        _run.Check((await GitAsync(clone, Session, "config", "--get-regexp", "^credential")).ExitCode == 1, () => "The clone's configuration holds credential keys.");
        _run.Check((await GitAsync(clone, Session, "config", "--get-all", "credential.helper")).ExitCode == 1, () => "Git finds a credential helper, so Git Credential Manager could start.");
        _run.Screener?.LearnFromGit((await GitOkAsync(clone, Session, "log", "--all", "--format=%ae%n%ce")).StandardOutput);
        _run.Note("The clone: through the clone form's URL box and the app's helper, with no dialog; no credential key or helper in its configuration.");
        return opened;
    }

    /// <summary>Step 5: the requests reach <c>Loaded</c>.</summary>
    public async Task LoadAsync(RepositoryViewModel opened)
    {
        await App.WaitForAsync(() => opened.HostingState.PullRequests is PullRequestsState.Loaded or PullRequestsState.Failed or PullRequestsState.OtherOrganization, "the sandbox's requests", NetworkStep);
        _run.Check(opened.HostingState.PullRequests == PullRequestsState.Loaded, () => $"The requests are {opened.HostingState.PullRequests}: {opened.HostingState.Failure?.Kind}: {opened.HostingState.Failure?.Message}.");
        _run.Note($"Open requests the app listed: {(opened.HostingState.List?.PullRequests.Count ?? 0).ToString(CultureInfo.InvariantCulture)}.");
    }

    /// <summary>Step 6: D325's commit on <paramref name="branch"/>, pushed with the branch menu's "Push {branch}…".</summary>
    public async Task PushAsync(RepositoryViewModel opened, string branch, string stamp, string title)
    {
        var clone = _clone ?? throw new InvalidOperationException("No clone.");
        await App.WaitForWatchersQuietAsync();
        var identity = Identity();
        await GitOkAsync(clone, identity, "switch", "-c", branch);
        var file = Path.Combine(clone, "visualcommit-live", stamp + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, $"VisualCommit live test {stamp}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await GitOkAsync(clone, identity, "add", "--", $"visualcommit-live/{stamp}.txt");
        await GitOkAsync(clone, identity, "commit", "-m", title);
        await App.WaitForAsync(() => HasRow(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live"), "the branch's folder under Local branches");
        await App.WaitForWatchersQuietAsync();
        if (App.RefRowModel(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live") is { IsOpen: false })
        {
            App.Click(await App.RefRowAsync(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live"));
        }

        await App.WaitForAsync(() => HasRow(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live", stamp), "the branch's row");
        App.RightClick(await App.RefRowAsync(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live", stamp));
        await App.WaitForAsync(() => App.Session.Shell.Menu.IsOpen, "the branch's menu");
        var mark = App.Session.Operations.Snapshot().Select(record => record.Sequence).DefaultIfEmpty(0).Max();
        App.ClickMenuItem(OperationTexts.MenuTexts.PushWithDialog(branch));
        await App.WaitForDialogAsync<PushDialogViewModel>();
        App.Click(App.FindByAutomationId("ConfirmButton"));
        OperationRecord? pushed = null;
        await WaitWatchingAsync(() => (pushed = App.Session.Operations.Snapshot().FirstOrDefault(record => record.Sequence > mark)) is not null, "the push to end", NetworkStep);
        _run.Check(pushed!.Outcome == OperationOutcome.Succeeded, () => $"The push ended {pushed.Outcome}: {pushed.Message}");
        _run.Note($"The push of {branch}: {pushed.Title}, {pushed.Outcome}.");
    }

    /// <summary>Step 7: a draft through the repository's hosting (<c>Hosting.CreatePullRequestAsync</c>); <c>ByRef</c> holds it.</summary>
    public async Task<PullRequest> CreateAsync(RepositoryViewModel opened, string title, string branch, string baseBranch)
    {
        var target = opened.HostingState.Repository ?? throw new XunitException("The repository has no hosted repository.");
        var created = await opened.Hosting.CreatePullRequestAsync(target, new NewPullRequest(title, LiveSteps.Description, branch, null, baseBranch, Draft: true), Cancelled).WaitAsync(NetworkStep, Cancelled);
        if (created.Value is not { } pull)
        {
            throw new XunitException(_run.Screen($"The create failed: {created.Failure?.Kind}: {created.Failure?.Refusal}: {created.Failure?.Message} (request {created.Failure?.RequestId ?? "none"})."));
        }

        _run.Note($"Created {HostingProviders.DisplayName(Provider)}'s !{pull.Number.ToString(CultureInfo.InvariantCulture)} from {branch} into {baseBranch}, draft {(pull.IsDraft ? "yes" : "no")}.");
        await App.WaitForAsync(() => opened.HostingState.ByRef.TryGetValue(LiveLeftovers.HeadsPrefix + branch, out var listed) && listed.Number == pull.Number, $"!{pull.Number} in ByRef", NetworkStep);
        return pull;
    }

    /// <summary>Closes the app once (before the tripwires read its git calls).</summary>
    public void Close()
    {
        if (!_closed)
        {
            _closed = true;
            _app?.Dispose();
        }
    }

    /// <summary>Step 10: the token in no file of the data folder, the clone or the home, nor in the app's git calls; the learned addresses in no <c>accounts.json</c> or log.</summary>
    public void Tripwires(List<string> problems)
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            TokenTripwire.AssertAbsent(_sandbox.Token, [_data.Path, _clones.Path, _home.Path], _app.Session.GitCalls);
            foreach (var call in _app.Session.GitCalls.Snapshot())
            {
                if (LiveTokenForms.FoundIn(call.StandardInput, _sandbox.Token) is { } form)
                {
                    throw new TokenTripwireException($"The token was found in the standard input of the app's git call {call.Sequence.ToString(CultureInfo.InvariantCulture)} ({form}).");
                }
            }

            _run.Note("The token's tripwire: not in the data folder, the clone, the identity's home or the app's git calls.");
        }
        catch (TokenTripwireException ex)
        {
            problems.Add(_run.Screen(ex.Message));
        }

        problems.AddRange(_run.DataFolderProblems(_data.Path));
    }

    public void Dispose()
    {
        Close();
        _home.Dispose();
        _clones.Dispose();
        _data.Dispose();
    }

    /// <summary>1.8's record in <c>accounts.json</c>: the provider's id, <c>id</c> a number (0 for Azure DevOps), <c>userId</c> a string for Azure DevOps alone, <c>avatarUrl</c> null.</summary>
    private void NoteAccountsFile(List<string> problems)
    {
        var path = Path.Combine(_data.Path, "accounts.json");
        if (!File.Exists(path))
        {
            problems.Add("No accounts.json after the sign-in (1.8).");
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var record = Records(document.RootElement).FirstOrDefault(item => item.TryGetProperty("provider", out var provider) && provider.GetString() == HostingProviders.Id(Provider));
        if (record.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"accounts.json holds no record of provider {HostingProviders.Id(Provider)} (1.8).");
            return;
        }

        var azure = Provider == HostingProvider.AzureDevOps;
        var id = record.TryGetProperty("id", out var number) && number.ValueKind == JsonValueKind.Number ? number.GetInt64() : (long?)null;
        var userId = record.TryGetProperty("userId", out var user) ? user.ValueKind : JsonValueKind.Undefined;
        var avatar = record.TryGetProperty("avatarUrl", out var url) ? url.ValueKind : JsonValueKind.Undefined;
        var holds = id is not null && (!azure || id == 0) && (azure ? userId == JsonValueKind.String : userId is JsonValueKind.Undefined or JsonValueKind.Null) && avatar is JsonValueKind.Null or JsonValueKind.Undefined;
        _run.Note($"accounts.json: a record of provider {HostingProviders.Id(Provider)}, id {(id is null ? "not a number" : azure ? (id == 0 ? "0" : "not 0") : "a number")}, userId {userId}, avatarUrl {avatar}: {(holds ? "as 1.8" : "not as 1.8")}.");
        if (!holds)
        {
            problems.Add("accounts.json's record is not as 1.8 says.");
        }
    }

    private static IEnumerable<JsonElement> Records(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("provider", out _))
            {
                yield return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var found in Records(property.Value))
                {
                    yield return found;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var found in Records(item))
                {
                    yield return found;
                }
            }
        }
    }

    private async Task WaitWatchingAsync(Func<bool> condition, string what, TimeSpan timeout)
    {
        await App.WaitForAsync(() => Interruption() is not null || condition(), what, timeout);
        switch (Interruption())
        {
            case PromptDialogViewModel prompt:
                prompt.Cancel();
                throw new XunitException("Git asked for credentials: the app's helper did not answer (D255).");
            case ErrorDialogViewModel error:
                var text = $"{error.Title}: {error.Summary}";
                error.Cancel();
                throw new XunitException(_run.Screen("The app showed an error: " + text));
        }
    }

    private DialogViewModel? Interruption() =>
        App.Session.Services.DialogHost.Open.FirstOrDefault(dialog => dialog is PromptDialogViewModel or ErrorDialogViewModel);

    private bool HasRow(params string[] path)
    {
        try
        {
            App.RefRowModel(path);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>D325's identity, dated now, in the identity's own home.</summary>
    private Dictionary<string, string?> Identity()
    {
        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'+00:00'", CultureInfo.InvariantCulture);
        return new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(_home.Path))
        {
            ["GIT_AUTHOR_NAME"] = LiveSteps.IdentityName,
            ["GIT_AUTHOR_EMAIL"] = LiveScreen.KeptAddress,
            ["GIT_COMMITTER_NAME"] = LiveSteps.IdentityName,
            ["GIT_COMMITTER_EMAIL"] = LiveScreen.KeptAddress,
            ["GIT_AUTHOR_DATE"] = now,
            ["GIT_COMMITTER_DATE"] = now,
        };
    }

    private Task<GitResult> GitAsync(string folder, IReadOnlyDictionary<string, string?> environment, params string[] arguments) =>
        _git.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = environment }, Cancelled);

    private async Task<GitResult> GitOkAsync(string folder, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var result = await GitAsync(folder, environment, arguments);
        return result.Succeeded ? result : throw new XunitException(_run.Screen($"git {string.Join(' ', arguments)} failed ({result.ExitCode.ToString(CultureInfo.InvariantCulture)}): {result.StandardError.Trim()}"));
    }
}
