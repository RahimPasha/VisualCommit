// What is where in phase 7b's real-window pass (docs/test-reports/phase-7b.md: "Conventions of every
// part", "The real window" and the R steps in the Notes of A2, A5, A6, B1, B3, B7, P1, P10, N1, N2 and
// N6; docs/phase-7b-design.md 14, with 7a's design 13 as the pattern; ruling 29, D363):
// - Phase7bRealWindowPass.cs (this file): the run (Phase7bRun: one folder of pictures and one run.txt
//   for the whole pass), the order of its methods (Phase7bOrder, 14.2's: three accounts, GitLab's two,
//   Azure DevOps' two), launching the real app on the scenario "hosting7b" served by the three fakes
//   (one empty tab, or one tab on web-app or mobile-app), the waits on the fakes' records and on D322's
//   line, comparing a capture with the scripted picture of the same name, the menus, accounts.json, the
//   clean end every launch makes, and the two launches each provider makes (SignInBrowseAndCloneAsync,
//   RequestsAndCreateAsync), shared by GitLab's and Azure DevOps' methods as their twins share theirs.
// - Phase7bRealWindowPass.Accounts.cs: the three accounts' launch (14.4 A).
// - Phase7bRealWindowPass.GitLab.cs and .AzureDevOps.cs: each provider's "Sign-in, browse, clone" and
//   "Requests and create" (14.4).
// - RealApp.Phase7b.cs (the three fakes' environment, the Accounts section's blocks and the real wheel
//   over it, the sign-in per provider), with 7a's RealApp.Phase7a.cs used as it is (the masked token,
//   the dialog's "Sign in", the repository browser's and the left panel's rows) and the earlier phases'
//   RealApp files.
// - The expectations of the scripted walk-through (VisualTests.Phase7b/Phase7bExpectations*.cs) are
//   compiled into this project too (see the .csproj), so both harnesses use the same constants, texts,
//   picture names and the one prepared state (PrepareRequestsAsync, design 2.7).
//
// Conventions for the part files (7a design 13.2's, with 14.2's):
// - Five methods, one launch each, at 1100×700, dark. Declare the copy and the served fakes first, then
//   the app, so the app is stopped first when a step fails: `using var copy = await
//   Scenarios.Hosting7bAsync();`, `await using var served = await Scenarios.ServeHosting7bAsync(copy,
//   ownsCopy: true, …);`, the prepare its twins call (PrepareRequestsAsync for "Requests and create",
//   none for the others), then `using var app = Launch(…)`. The twins one launch compares share one
//   prepared state.
// - The three providers are on in every launch, as in every twin (RealApp.Hosting7bEnvironment). Each
//   sign-in types the scenario's token into the real dialog (D320's form), and for Azure DevOps the
//   organization as part A gives it, or finds it prefilled from the tab's remote (ruling 16). The
//   keychain is the memory store (VISUALCOMMIT_KEYCHAIN=memory); nothing is seeded, nothing restarts.
// - After an input, wait for a named condition (a log line, a UI Automation element or text, a record
//   of a fake), each with one of the pass's limits; never for a fixed time. A picture of a signed-in
//   repository waits for D322's line alone (the new providers write no avatars line, D349).
// - Pictures: Compare(app, served, the part's constant) with the scripted picture of the same name, after
//   checking that no name on screen holds a token; the pointer at the neutral point unless a menu is
//   open (keepPointer). At most 3 % differ (D33, D58). The 7b walk-through's pictures must exist: run
//   `dotnet test --project tests/VisualCommit.VisualTests.Phase7b` first.
// - No token is ever passed to an Assert method or put in a message: texts that could hold one are
//   tested with string.Contains and a failure names the place. A text read from the app, git or a fake
//   goes into a message only through Checked; the fakes' records only through Recorded and
//   RecordedGitHub (whose paths, queries and bodies are checked); the log's lines only as a count
//   (CountLinesSince). Never click "Open in browser", "Copy link", any "Copy …", a token link, "Open
//   file", "Open in editor" or "Show in folder": no 7b step opens the browser (ruling 29); never change a
//   desktop setting (D224).
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using VisualCommit.VisualTests.Phase7b;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of phase 7b's pass, shared by its tests: one folder of screenshots and one run.txt.</summary>
public sealed class Phase7bRun : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: Phase7bExpectations.Phase);

    /// <summary>Whether the display has been noted in run.txt yet (once, with the first launch).</summary>
    internal bool DisplayNoted { get; set; }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// Runs phase 7b's real-window methods in design 14.2's order: the three accounts, then GitLab's two,
/// then Azure DevOps' two (two per provider and one for three accounts, ruling 29, in place of D319's
/// one per part). A method the list does not name runs after them, by name.
/// </summary>
public sealed class Phase7bOrder : ITestCaseOrderer
{
    /// <summary>The methods in their order.</summary>
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(Phase7bRealWindowPass.Check_A_three_accounts),
        nameof(Phase7bRealWindowPass.Check_GitLab_sign_in_browse_and_clone),
        nameof(Phase7bRealWindowPass.Check_GitLab_requests_and_create),
        nameof(Phase7bRealWindowPass.Check_AzureDevOps_sign_in_browse_and_clone),
        nameof(Phase7bRealWindowPass.Check_AzureDevOps_requests_and_create),
    ];

    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        return [.. testCases.OrderBy(test => Rank(test.TestMethodName)).ThenBy(test => test.TestMethodName, StringComparer.Ordinal)];
    }

    private static int Rank(string? method) => method is not null && Methods.Contains(method) ? Methods.ToList().IndexOf(method) : int.MaxValue;
}

/// <summary>
/// The real-window pass of phase 7b: the steps of docs/test-reports/phase-7b.md marked "R" (parts A,
/// B, P and N), repeated in the real app on the Windows desktop with the real mouse and keyboard,
/// against the fakes of Azure DevOps, GitLab and GitHub on 127.0.0.1 (design 14). Screenshots go to
/// artifacts/visual/phase-7b/real-window/ and are compared with the scripted walk-through's of the same
/// name (D33, D58), which must have run first. The launches are in Phase7bRealWindowPass.Accounts.cs,
/// .GitLab.cs and .AzureDevOps.cs, the two each provider makes in this file.
/// </summary>
[TestCaseOrderer(typeof(Phase7bOrder))]
public partial class Phase7bRealWindowPass(Phase7bRun fixture) : IClassFixture<Phase7bRun>
{
    /// <summary>How long the app may take to open a scenario: its history read and drawn, its status shown (7a's, phase 6's).</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long an operation may take to end (its log line), 7a's.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a change of the window that follows an input may take to show: the scripted run's default wait.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a network step may take: a sign-in, a list, a clone, a create.</summary>
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How often <see cref="WaitForFake"/> and <see cref="WaitForGitHub"/> read a fake's records.</summary>
    private static readonly TimeSpan FakePollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The repository browser's provider switch, one id per provider (D362): none shows with one account signed in (B3).</summary>
    private static readonly string[] SwitchIds = ["CloneFromGitHubProviderGitHub", "CloneFromGitHubProviderGitLab", "CloneFromGitHubProviderAzureDevOps"];

    private RealWindowRun Run => fixture.Run;

    // ---- Launches ----

    /// <summary>
    /// Starts the real app on a new data folder with one tab, empty (<paramref name="repository"/>
    /// null) or on <paramref name="served"/>'s <c>web-app</c> or <c>mobile-app</c>, with the three fakes
    /// on (<see cref="RealApp.Hosting7bEnvironment"/>) at 1100×700, and waits for each provider's start
    /// line <c>{DisplayName}: 127.0.0.1:&lt;port&gt;.</c> (design 9.2, part A's <c>StartLines</c>, GitHub's
    /// as 7a's), then for the welcome page (git found, <c>CloneRepoButton</c> shown) or for the repository
    /// to have opened (<see cref="Opened"/>). Notes in run.txt each fake that could not have its port
    /// (<see cref="NotePorts"/>). Disposes the app when that fails.
    /// </summary>
    private RealApp Launch(ServedHosting7b served, string? repository)
    {
        ArgumentNullException.ThrowIfNull(served);
        NotePorts(served);
        var data = Run.NewDataDirectory();
        var tab = repository is null ? new TabState(null) : new TabState(FolderOf(served, repository));
        var app = Sized(RealApp.Launch(data, new SessionState { Tabs = [tab], ActiveTab = 0 }, RealApp.Hosting7bEnvironment(served)));
        try
        {
            foreach (var line in PartA.StartLines.Take(3))
            {
                app.WaitForLogLine(AppLogFile.Containing(WithPorts(line, served)), LoadTimeout);
            }

            if (repository is not null)
            {
                return Opened(app, repository, loadsBefore: 0, readsBefore: 0);
            }

            app.WaitForGitFound(LoadTimeout);
            app.WaitFor(() => app.ShownElements("CloneRepoButton").Count == 1, "the welcome page's Clone repository button", LoadTimeout);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The working tree of the scenario's clone <paramref name="repository"/> (<c>mobile-app</c>, <c>web-app</c>, <c>manifest</c>).</summary>
    private static string FolderOf(ServedHosting7b served, string repository) => repository switch
    {
        MobileApp => served.MobileApp.Path,
        WebApp => served.WebApp.Path,
        Manifest => served.Manifest.Path,
        _ => throw new ArgumentException($"The scenario hosting7b has no clone {repository}.", nameof(repository)),
    };

    /// <summary>Sizes the window to 1100×700 and notes the display once. Disposes the app when that fails. Copied from 7a's (Phase7aRealWindowPass.cs), as phase 6's.</summary>
    private RealApp Sized(RealApp app)
    {
        try
        {
            app.SetClientSize(DefaultSize.Width, DefaultSize.Height);
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Waits until <paramref name="repository"/> has opened: its history loaded and its status shown,
    /// the log lines the app writes for both, after the <paramref name="loadsBefore"/> and
    /// <paramref name="readsBefore"/> the log held before. Disposes the app when that fails. Copied from
    /// 7a's (Phase7aRealWindowPass.cs), as phase 6's.
    /// </summary>
    private static RealApp Opened(RealApp app, string repository, int loadsBefore, int readsBefore)
    {
        try
        {
            app.WaitForLogLine(Loaded(repository), LoadTimeout, loadsBefore + 1);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead(repository), LoadTimeout, readsBefore + 1);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The line a repository logs when its graph has loaded: <c>&lt;repo&gt;: loaded N commits in M ms</c>.</summary>
    private static Regex Loaded(string repository) => AppLogFile.Containing($"] {repository}: loaded ");

    /// <summary>
    /// Notes in run.txt each fake that could not have its port (47397 to 47399, D363): its URLs then show
    /// another port (D265), which <see cref="WithPorts(string, ServedHosting7b)"/> gives the texts.
    /// </summary>
    private void NotePorts(ServedHosting7b served)
    {
        if (served.AzureDevOps.PortWasTaken)
        {
            Run.Note(string.Create(CultureInfo.InvariantCulture, $"Port {AzureDevOpsPort} was taken: the fake of Azure DevOps ran on {served.AzureDevOps.Port} (D265)."));
        }

        if (served.GitLab.PortWasTaken)
        {
            Run.Note(string.Create(CultureInfo.InvariantCulture, $"Port {GitLabPort} was taken: the fake of GitLab ran on {served.GitLab.Port} (D265)."));
        }

        if (served.GitHub.PortWasTaken)
        {
            Run.Note(string.Create(CultureInfo.InvariantCulture, $"Port {GitHubPort} was taken: the fake of GitHub ran on {served.GitHub.Port} (D265)."));
        }
    }

    // ---- The scenario's facts per provider ----

    /// <summary>The scenario's token of <paramref name="provider"/>: Ada's of Fernhill-Labs, ada's fine-grained one on GitLab, octo's on GitHub.</summary>
    private static string TokenOf(ServedHosting7b served, HostingProvider provider) => provider switch
    {
        HostingProvider.AzureDevOps => served.AzureDevOpsToken,
        HostingProvider.GitLab => served.GitLabToken,
        _ => served.GitHubToken,
    };

    /// <summary>The login a block shows once signed in, in the server's spelling: <c>octo</c>, <c>ada</c>, <c>Fernhill-Labs</c> (part A).</summary>
    private static string LoginOf(HostingProvider provider) => provider switch
    {
        HostingProvider.AzureDevOps => PartA.Organization,
        HostingProvider.GitLab => AdaLogin,
        _ => OctoLogin,
    };

    /// <summary>The sign-in dialog's title (part A's 10.4): <c>Add GitHub account</c>, <c>Add GitLab account</c>, <c>Add Azure DevOps account</c>.</summary>
    private static string DialogTitleOf(HostingProvider provider) => provider switch
    {
        HostingProvider.AzureDevOps => PartA.AzureDevOpsDialogTitle,
        HostingProvider.GitLab => PartA.GitLabDialogTitle,
        _ => PartA.GitHubDialogTitle,
    };

    /// <summary>The fake of <paramref name="provider"/>, Azure DevOps' or GitLab's (GitHub's is 7a's <see cref="FakeGitHub"/>).</summary>
    private static FakeHostingService FakeOf(ServedHosting7b served, HostingProvider provider) => provider switch
    {
        HostingProvider.AzureDevOps => (FakeHostingService)served.AzureDevOps,
        HostingProvider.GitLab => served.GitLab,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "GitHub's fake is a FakeGitHub."),
    };

    /// <summary>The credential's form each request of the provider's API carries (part A's facts): GitLab's <c>Bearer</c>, Azure DevOps' Basic with an empty user.</summary>
    private static FakeAuthorizationForm FormOf(HostingProvider provider) =>
        provider == HostingProvider.GitLab ? FakeAuthorizationForm.Bearer : FakeAuthorizationForm.BasicEmptyUser;

    /// <summary>
    /// The login a fake records for the scenario's signed-in user: GitLab's <c>ada</c>; Azure DevOps'
    /// the organization and Ada's made-up id (WP-FA, as the twins' <c>AdaAzureLogin</c>).
    /// </summary>
    private static string RequesterOf(ServedHosting7b served, HostingProvider provider) =>
        provider == HostingProvider.GitLab ? AdaLogin : $"{Organization}/{AdaAzureId(served)}";

    /// <summary>Ada's made-up id in Fernhill-Labs, in lower case, as accounts.json's <c>userId</c> holds it.</summary>
    private static string AdaAzureId(ServedHosting7b served) => served.AzureDevOps.User(Organization, AdaName).Id.ToString("D");

    // ---- The fakes' records and the log ----

    /// <summary>Each fake's <c>Requests.Count</c> before a step (the twins' marks); <c>default</c> is the launch.</summary>
    private readonly record struct Marks(int AzureDevOps, int GitLab, int GitHub)
    {
        public int Of(HostingProvider provider) => provider switch
        {
            HostingProvider.AzureDevOps => AzureDevOps,
            HostingProvider.GitLab => GitLab,
            _ => GitHub,
        };
    }

    private static Marks Mark(ServedHosting7b served) => new(served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count);

    /// <summary>
    /// The records of <paramref name="fake"/> (Azure DevOps' or GitLab's) after its first
    /// <paramref name="mark"/>, each checked first (<see cref="AssertNoToken"/>): an assertion over them
    /// may then print them, as xUnit prints a collection when it fails, without printing a token.
    /// </summary>
    private static List<FakeServiceRequest> Recorded(ServedHosting7b served, FakeHostingService fake, int mark = 0)
    {
        List<FakeServiceRequest> records = [.. fake.Requests.Skip(mark)];
        foreach (var record in records)
        {
            var place = $"The fake of {fake.DisplayName}'s record of {record.Method} {record.Route ?? "(no route)"}";
            AssertNoToken(served, record.Path, $"{place}: its path");
            AssertNoToken(served, record.Query, $"{place}: its query");
            AssertNoToken(served, record.Body, $"{place}: its body");
        }

        return records;
    }

    /// <summary>GitHub's fake's records after its first <paramref name="mark"/>, each checked first, as <see cref="Recorded"/>.</summary>
    private static List<FakeGitHubRequest> RecordedGitHub(ServedHosting7b served, int mark = 0)
    {
        List<FakeGitHubRequest> records = [.. served.GitHub.Requests.Skip(mark)];
        foreach (var record in records)
        {
            var place = $"The fake of GitHub's record of {record.Method} {record.Route ?? "(no route)"}";
            AssertNoToken(served, record.Path, $"{place}: its path");
            AssertNoToken(served, record.Query, $"{place}: its query");
            AssertNoToken(served, record.Body, $"{place}: its body");
        }

        return records;
    }

    /// <summary>The three fakes' records since <paramref name="mark"/>, for a failure's message (method, path, route, status; checked first).</summary>
    private static string DescribeSince(ServedHosting7b served, Marks mark) =>
        string.Join(
            " | ",
            Recorded(served, served.AzureDevOps, mark.AzureDevOps).Select(request => $"Azure DevOps {request.Method} {request.Path} {request.Route} {request.Status}")
                .Concat(Recorded(served, served.GitLab, mark.GitLab).Select(request => $"GitLab {request.Method} {request.Path} {request.Route} {request.Status}"))
                .Concat(RecordedGitHub(served, mark.GitHub).Select(request => $"GitHub {request.Method} {request.Path} {request.Route} {request.Status}")));

    /// <summary>"Requests none": no record in any of the three fakes since <paramref name="mark"/>; fails naming <paramref name="what"/> and listing them.</summary>
    private static void AssertNoRequests(ServedHosting7b served, Marks mark, string what)
    {
        var none = Recorded(served, served.AzureDevOps, mark.AzureDevOps).Count == 0
            && Recorded(served, served.GitLab, mark.GitLab).Count == 0
            && RecordedGitHub(served, mark.GitHub).Count == 0;
        Assert.True(none, $"{what}; the fakes recorded: {DescribeSince(served, mark)}.");
    }

    /// <summary>No record since <paramref name="mark"/> in the two fakes other than <paramref name="provider"/>'s.</summary>
    private static void AssertNoRequestsBut(ServedHosting7b served, HostingProvider provider, Marks mark, string what)
    {
        var none = (provider == HostingProvider.AzureDevOps || Recorded(served, served.AzureDevOps, mark.AzureDevOps).Count == 0)
            && (provider == HostingProvider.GitLab || Recorded(served, served.GitLab, mark.GitLab).Count == 0)
            && (provider == HostingProvider.GitHub || RecordedGitHub(served, mark.GitHub).Count == 0);
        Assert.True(none, $"{what}; the fakes recorded: {DescribeSince(served, mark)}.");
    }

    /// <summary>
    /// Waits until <paramref name="fake"/> has recorded, after its first <paramref name="mark"/>
    /// requests, a request that <paramref name="match"/> accepts, and returns it; on a time-out the
    /// message lists what it recorded since the mark (<see cref="Recorded"/>). 7a's form for GitHub's
    /// (Phase7aRealWindowPass.cs): the fake records a request before its answer goes out
    /// (<c>FakeHostingService.ServeAsync</c>), so for a step the app shows only after the answer this
    /// is a plain read of a record that is there already. Reads the records every 100 ms, at most
    /// <paramref name="timeout"/>.
    /// </summary>
    private static FakeServiceRequest WaitForFake(ServedHosting7b served, FakeHostingService fake, int mark, Func<FakeServiceRequest, bool> match, string what, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(fake);
        ArgumentNullException.ThrowIfNull(match);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (Recorded(served, fake, mark).FirstOrDefault(match) is { } found)
            {
                return found;
            }

            if (clock.Elapsed >= timeout)
            {
                throw new TimeoutException(
                    $"Timed out after {timeout.TotalSeconds:0} s waiting for the fake of {fake.DisplayName}'s record of {what}. Since the mark it recorded: "
                    + string.Join(" | ", Recorded(served, fake, mark).Select(request => $"{request.Method} {request.Path} {request.Route} {request.Status}")));
            }

            Thread.Sleep(FakePollInterval);
        }
    }

    /// <summary>As <see cref="WaitForFake"/>, for GitHub's fake (7a's records, <see cref="FakeGitHubRequest"/>).</summary>
    private static FakeGitHubRequest WaitForGitHub(ServedHosting7b served, int mark, Func<FakeGitHubRequest, bool> match, string what, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(match);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            if (RecordedGitHub(served, mark).FirstOrDefault(match) is { } found)
            {
                return found;
            }

            if (clock.Elapsed >= timeout)
            {
                throw new TimeoutException(
                    $"Timed out after {timeout.TotalSeconds:0} s waiting for the fake of GitHub's record of {what}. Since the mark it recorded: "
                    + string.Join(" | ", RecordedGitHub(served, mark).Select(request => $"{request.Method} {request.Path} {request.Route} {request.Status}")));
            }

            Thread.Sleep(FakePollInterval);
        }
    }

    /// <summary>
    /// Part A's check requests of <paramref name="provider"/>'s sign-in since <paramref name="mark"/>,
    /// in order, each 200, and none of the other two fakes' (10.7, 14.4 A): GitHub's one <c>GET
    /// api/v3/user</c> with its token; GitLab's one <c>GET /api/v4/user</c>, <c>Bearer</c>, ada;
    /// Azure DevOps' connectionData of <paramref name="organization"/> as typed or prefilled, then the
    /// core resource area, each Basic with an empty user, the first Ada's.
    /// </summary>
    private static void AssertSignInRequests(ServedHosting7b served, HostingProvider provider, Marks mark, string? organization)
    {
        switch (provider)
        {
            case HostingProvider.GitHub:
                WaitForGitHub(served, mark.GitHub, request => request.Route == "user", "GET api/v3/user", NetworkTimeout);
                Assert.Equal(
                    [("GET", "/api/v3/user", "user", true, OctoLogin, 200)],
                    RecordedGitHub(served, mark.GitHub).Select(request => (request.Method, request.Path, request.Route, request.TokenPresent, request.Login, request.Status)));
                break;
            case HostingProvider.GitLab:
                WaitForFake(served, served.GitLab, mark.GitLab, request => request.Route == PartA.GitLabSignInRoutes[^1], "GET /api/v4/user", NetworkTimeout);
                Assert.Equal(
                    [("GET", "/api/v4/user", "user", FakeAuthorizationForm.Bearer, AdaLogin, 200)],
                    Recorded(served, served.GitLab, mark.GitLab).Select(request => (request.Method, request.Path, request.Route, request.Authorization, request.Login, request.Status)));
                break;
            default:
                ArgumentException.ThrowIfNullOrEmpty(organization);
                var routes = PartA.AzureDevOpsSignInRoutes(organization);
                WaitForFake(served, served.AzureDevOps, mark.AzureDevOps, request => request.Route == routes[^1], "the core resource area", NetworkTimeout);
                var records = Recorded(served, served.AzureDevOps, mark.AzureDevOps);
                Assert.Equal(
                    [
                        ("GET", $"/{organization}/_apis/connectionData", routes[0], FakeAuthorizationForm.BasicEmptyUser, 200),
                        ("GET", $"/{organization}/_apis/resourceAreas/{PartA.CoreAreaId}", routes[1], FakeAuthorizationForm.BasicEmptyUser, 200),
                    ],
                    records.Select(request => (request.Method, request.Path, request.Route ?? string.Empty, request.Authorization, request.Status)));
                Assert.Equal(RequesterOf(served, provider), records[0].Login);
                break;
        }

        AssertNoRequestsBut(served, provider, mark, $"{provider}'s sign-in asked another fake");
    }

    /// <summary>
    /// <paramref name="provider"/>'s fake recorded since <paramref name="mark"/> exactly
    /// <paramref name="routes"/>, in order, each 200 with the provider's credential form, each sent as
    /// the scenario's user (Azure DevOps' <c>areas</c>, which reads no credential's user, left out, as
    /// the twins leave it); the other two fakes none.
    /// </summary>
    private static void AssertRequestsAlone(ServedHosting7b served, HostingProvider provider, Marks mark, IReadOnlyList<string> routes, string what)
    {
        var records = Recorded(served, FakeOf(served, provider), mark.Of(provider));
        Assert.Equal(
            routes.Select(route => (route, 200, FormOf(provider))),
            records.Select(request => (request.Route ?? request.Path, request.Status, request.Authorization)));
        var requester = RequesterOf(served, provider);
        foreach (var request in records.Where(request => request.Route != PartP.AzureDevOpsAreasRoute))
        {
            Assert.True(request.Login == requester, $"{request.Method} {request.Route} {request.Status} was not sent as the scenario's user ({what}).");
        }

        AssertNoRequestsBut(served, provider, mark, what);
    }

    /// <summary>
    /// D322: waits for <paramref name="repository"/>'s <c>listed {n} open pull requests of {full name}</c>
    /// (<see cref="AppLogFile.PullRequestsListed"/>) since <paramref name="logMark"/>, written on the UI
    /// thread once the list is applied: alone for a new provider, which writes no avatars line (D349),
    /// the state of a picture of a signed-in repository (design 14.3).
    /// </summary>
    private static void WaitForListed(RealApp app, int logMark, string repository)
    {
        var listed = AppLogFile.PullRequestsListed(repository);
        WaitTelling(
            app,
            () => app.CountLogLinesSince(listed, logMark) > 0,
            $"{repository}'s requests listed since the mark (D322)",
            () => $"Since the mark the log has {app.CountLogLinesSince(listed, logMark)} \"listed\" lines of {repository}.",
            NetworkTimeout);
    }

    /// <summary>Waits for <paramref name="condition"/> as <see cref="RealApp.WaitFor"/> does, at most <paramref name="timeout"/> (<see cref="StepTimeout"/> when null); on a timeout the message adds what <paramref name="state"/> reads then. As 7a's and phase 6's.</summary>
    private static void WaitTelling(RealApp app, Func<bool> condition, string what, Func<string> state, TimeSpan? timeout = null)
    {
        try
        {
            app.WaitFor(condition, what, timeout ?? StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} {state()}", ex);
        }
    }

    /// <summary>
    /// How many lines of the log since <paramref name="logMark"/> <paramref name="match"/> accepts: a
    /// check of the log fails with the count and a message naming the line it looked for, never with
    /// the lines themselves (7a's).
    /// </summary>
    private static int CountLinesSince(RealApp app, int logMark, Func<string, bool> match) => app.LogLinesSince(logMark).Count(match);

    // ---- Tokens ----

    /// <summary>The scenario's four tokens with their constants' names, for a failure's message (design 2.6).</summary>
    private static IReadOnlyList<(string Name, string Token)> TokensOf(ServedHosting7b served) =>
    [
        (nameof(AzureDevOpsToken), served.AzureDevOpsToken),
        (nameof(GitLabToken), served.GitLabToken),
        (nameof(GitLabLegacyToken), served.GitLabLegacyToken),
        (nameof(GitHubToken), served.GitHubToken),
    ];

    /// <summary>Fails, naming <paramref name="place"/> and the token's constant but never the token, when <paramref name="text"/> holds one of the scenario's tokens.</summary>
    private static void AssertNoToken(ServedHosting7b served, string? text, string place)
    {
        foreach (var (name, token) in TokensOf(served))
        {
            Assert.False(text?.Contains(token, StringComparison.Ordinal) ?? false, $"{place} holds {name}.");
        }
    }

    /// <summary>
    /// <paramref name="text"/>, read from the app, git or the log, for a failure's message, after
    /// <see cref="AssertNoToken"/> has checked it: a message that reports a failure never prints a token,
    /// and a token in such a text fails by its place instead (7a's).
    /// </summary>
    private static string Checked(ServedHosting7b served, string text, string place)
    {
        AssertNoToken(served, text, place);
        return text;
    }

    /// <summary>
    /// No element of the window is named with a token (the names UI Automation gives every element,
    /// the text elements' texts among them): the twins' "no token on screen" (7a's). The value of the
    /// masked box is <see cref="RealApp.SecretBoxValue"/>'s, read where a token was typed.
    /// </summary>
    private static void AssertNoTokenOnScreen(RealApp app, ServedHosting7b served)
    {
        foreach (var element in app.Window.FindAllDescendants())
        {
            string? name;
            string? id;
            try
            {
                name = element.Properties.Name.ValueOrDefault;
                id = element.Properties.AutomationId.ValueOrDefault;
            }
            catch (Exception ex) when (ex is FlaUI.Core.Exceptions.ElementNotAvailableException or System.Runtime.InteropServices.COMException)
            {
                // Gone since the walk listed it (a tooltip, a closing popup): it shows nothing.
                continue;
            }

            AssertNoToken(served, name, $"The name of the element {id}");
        }
    }

    // ---- Pictures ----

    /// <summary>
    /// Rests the pointer at the neutral point unless <paramref name="keepPointer"/> is true (a capture
    /// with a menu open), asserts that no element of the window is named with a token
    /// (<see cref="AssertNoTokenOnScreen"/>), then captures the client area, saves it as
    /// <c>&lt;name&gt;.png</c> and asserts that it matches the scripted picture of the same name within
    /// <see cref="RealWindowRun.MaxDifference"/> (D33, D58); run.txt gets a line with the difference and
    /// <c>&lt;name&gt;-difference.png</c> shows it. Copied from 7a's (Phase7aRealWindowPass.cs).
    /// </summary>
    private void Compare(RealApp app, ServedHosting7b served, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!keepPointer)
        {
            app.MoveToNeutral();
        }

        // After the move, so that no tooltip opens over the pointer's last place during the scan.
        AssertNoTokenOnScreen(app, served);
        using var capture = app.CaptureClient();
        Run.Save(capture, name);
        Run.AssertMatchesScripted(capture, name, name);
    }

    // ---- Menus (phase 3's conventions; copied from 7a's, Phase7aRealWindowPass.cs, as phase 6's) ----

    /// <summary>The labels of a menu as the report writes it: its items without the header, the separators and the marks after a label.</summary>
    private static List<string> ItemLabels(IEnumerable<string> lines) =>
        [.. lines.Skip(1).Where(line => line != "—").Select(line => MenuMark().Replace(line, string.Empty))];

    /// <summary>The disabled items of a menu as the report writes it, with their reasons (each item's tooltip).</summary>
    private static List<(string Label, string Reason)> DisabledItems(IEnumerable<string> lines) =>
        [.. lines.Skip(1)
            .Select(line => DisabledMark().Match(line))
            .Where(match => match.Success)
            .Select(match => (match.Groups["label"].Value, match.Groups["reason"].Value))];

    /// <summary>
    /// Asserts the open menu as the report writes it (<paramref name="lines"/>, header first): its
    /// items' labels in order, each disabled item with its reason as its help text, every other item
    /// enabled; the height the report gives (±2); and that the menu lies at least 4 px inside the
    /// window. Copied from 7a's.
    /// </summary>
    private static void AssertMenu(RealApp app, IReadOnlyList<string> lines, double height)
    {
        var menu = app.BoundsOf(app.WaitForMenu());
        Assert.Equal(ItemLabels(lines), app.MenuItemNames());
        var disabled = DisabledItems(lines);
        foreach (var item in app.FindAll("ContextMenuItem"))
        {
            var reason = disabled.Where(entry => entry.Label == item.Name).Select(entry => entry.Reason).FirstOrDefault();
            if (reason is null)
            {
                Assert.True(item.IsEnabled, $"The menu item \"{item.Name}\" is disabled.");
            }
            else
            {
                Assert.False(item.IsEnabled, $"The menu item \"{item.Name}\" is enabled; the report says it is disabled with \"{reason}\".");
                Assert.Equal(reason, RealApp.HelpTextOf(item));
            }
        }

        Assert.True(
            Math.Abs(menu.Height - height) <= WindowLayout.MenuHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Height} high; the report says {height} (±{WindowLayout.MenuHeightTolerance})."));
        var client = app.ClientSize;
        var inset = (float)WindowLayout.MenuEdgeInset - 0.5f;
        Assert.True(
            menu.Left >= inset && menu.Top >= inset && menu.Right <= client.Width - inset && menu.Bottom <= client.Height - inset,
            $"The menu at {menu} is not 4 px inside the {client.Width}x{client.Height} window.");
    }

    /// <summary>Esc closes the open menu; waits until no menu is in the window. Copied from 7a's.</summary>
    private static void CloseMenuWithEscape(RealApp app)
    {
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", RealApp.DefaultWaitTimeout);
    }

    // ---- accounts.json (design 1.8, 9.3) ----

    /// <summary>One record of <c>accounts.json</c> as the file holds it; <c>HasUserId</c> whether it has the member at all.</summary>
    private sealed record SavedAccount(string? Provider, string? Host, string? Login, bool HasUserId, string? UserId, bool UseForGit, string? TokenState);

    /// <summary><c>accounts.json</c>'s version, its records, and whether the file's text holds one of the scenario's tokens.</summary>
    private sealed record SavedAccounts(int Version, IReadOnlyList<SavedAccount> Accounts, bool HoldsAToken);

    /// <summary>
    /// <c>accounts.json</c> in <paramref name="data"/>, or null while it is missing, being written or
    /// not of the expected shape. The file is written off the UI thread (7a's 7.2), so a check polls it.
    /// </summary>
    private static SavedAccounts? ReadSavedAccounts(string data, ServedHosting7b served)
    {
        try
        {
            string text;
            using (var stream = new FileStream(new AppPaths(data).AccountsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            if (root.GetProperty("accounts") is not { ValueKind: JsonValueKind.Array } accounts)
            {
                return null;
            }

            List<SavedAccount> records = [];
            foreach (var record in accounts.EnumerateArray())
            {
                var hasUserId = record.TryGetProperty("userId", out var userId);
                records.Add(new SavedAccount(
                    record.GetProperty("provider").GetString(),
                    record.GetProperty("host").GetString(),
                    record.GetProperty("login").GetString(),
                    hasUserId,
                    hasUserId ? userId.GetString() : null,
                    record.GetProperty("useForGit").GetBoolean(),
                    record.GetProperty("tokenState").GetString()));
            }

            return new SavedAccounts(
                root.GetProperty("version").GetInt32(),
                records,
                TokensOf(served).Any(entry => text.Contains(entry.Token, StringComparison.Ordinal)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A record for a failure's message: its fields, which hold no token once <see cref="SavedAccounts.HoldsAToken"/> is false.</summary>
    private static string Describe(SavedAccount account) =>
        $"{account.Provider} {account.Host} {account.Login} userId {(account.HasUserId ? account.UserId ?? "null" : "none")} useForGit {account.UseForGit} {account.TokenState}";

    // ---- The end of every launch ----

    /// <summary>
    /// What every launch ends with (design 14.3, the report's "The real window"): the app closed with
    /// exit code 0 (closed here unless the method closed it already); then no token of the scenario in
    /// any file of the data folder (the app's log among them), of the copy's <c>mobile-app/</c>,
    /// <c>manifest/</c> and <c>web-app/</c> (as every twin searches them) and of
    /// <paramref name="folders"/> (<see cref="TokenTripwire"/>; the real process's git calls are not
    /// recorded, so no call log); each fake's <c>Errors</c> empty; no request a fake refused for its
    /// headers or its shape (answered 400 or 415); and no <c>avatars/</c> folder and no "showing GitHub
    /// avatars" line unless GitHub signed in (its fake answered <c>user</c> 200; D349, D367).
    /// </summary>
    private static void AssertCleanEnd(RealApp app, ServedHosting7b served, params string[] folders)
    {
        Assert.Equal(0, app.ExitCode ?? app.Close());
        string[] searched = [app.DataDirectory, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path, .. folders];
        foreach (var (_, token) in TokensOf(served))
        {
            TokenTripwire.AssertAbsent(token, searched, calls: null);
        }

        foreach (var (fake, errors) in new[] { ("Azure DevOps", served.AzureDevOps.Errors), ("GitLab", served.GitLab.Errors), ("GitHub", served.GitHub.Errors) })
        {
            foreach (var error in errors)
            {
                AssertNoToken(served, error, $"An error of the fake of {fake}");
            }

            Assert.True(errors.Count == 0, $"The fake of {fake} recorded errors: {string.Join(" | ", errors)}");
        }

        Assert.DoesNotContain(Recorded(served, served.AzureDevOps), request => request.Status is 400 or 415);
        Assert.DoesNotContain(Recorded(served, served.GitLab), request => request.Status is 400 or 415);
        var gitHub = RecordedGitHub(served);
        Assert.DoesNotContain(gitHub, request => request.Status is 400 or 415);
        if (!gitHub.Any(request => request.Route == "user" && request.Status == 200))
        {
            var avatars = new AppPaths(app.DataDirectory).AvatarsDirectory;
            Assert.False(Directory.Exists(avatars), $"{avatars} exists, though GitHub never signed in (D349, D367).");
            var shown = CountLinesSince(app, 0, line => line.Contains("showing GitHub avatars", StringComparison.Ordinal));
            Assert.True(shown == 0, $"{shown} lines of the log say \"showing GitHub avatars\", though GitHub never signed in (D349).");
        }
    }

    // ---- The two launches each provider makes (14.4), shared by .GitLab.cs and .AzureDevOps.cs ----

    /// <summary>
    /// "Sign-in, browse, clone" (14.4) for GitLab or Azure DevOps on one empty tab, no preparation:
    /// <list type="number">
    /// <item>A5's or A6's R step: <c>OpenSignInDialog</c> from the Accounts section; for Azure DevOps the
    /// organization box focused and <c>TypeOrganization(Fernhill-Labs)</c>; a real click on
    /// <c>SignInToken</c>, the token typed and masked; compared with <c>a05b-gitlab-token-masked</c> or
    /// <c>a06b-azure-token-masked</c>. <c>ConfirmSignIn</c>: part A's check requests alone; the block
    /// names the login; Esc.</item>
    /// <item>B1's and B3's R steps: a real click on <c>CloneRepoButton</c>; the form's button reads
    /// <c>From GitLab…</c> or <c>From Azure DevOps…</c> (D354); a real click on it; the browser lists
    /// part B's rows, the filter focused, no switch; compared with <c>b03a-gitlab-listed</c> or
    /// <c>b03b-azure-listed</c>.</item>
    /// <item>B7's R step: part B's row, "Choose": the URL through <c>WithPorts</c> (Azure DevOps' with its
    /// <c>Fernhill-Labs@</c> and <c>%20</c>, ruling 15; GitLab's nested path); the parent folder typed;
    /// "Clone": the clone opens within 60 s through <c>visualcommit-askpass.exe</c> run by Git for
    /// Windows' <c>sh</c>, with no credentials dialog, a 200 git request as <c>x-access-token</c> and
    /// none answered 200 as another user (git before 2.46 first sends the URL's organization without a
    /// password, a 401, r7 H4b); the clone's tab's D322 line, so that the close cuts no request of it.</item>
    /// <item>run.txt names the helper and whether its path holds a space; after the close, in the clone,
    /// <c>remote.origin.url</c> is step 3's URL and <c>git config --get-regexp ^credential</c> exits 1;
    /// <c>AssertCleanEnd</c> with the clones' folder.</item>
    /// </list>
    /// </summary>
    private async Task SignInBrowseAndCloneAsync(HostingProvider provider)
    {
        var azure = provider == HostingProvider.AzureDevOps;
        using var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, ownsCopy: true, ct: TestContext.Current.CancellationToken);
        using var clones = new TempDirectory("rw7b-clone");
        using var app = Launch(served, repository: null);
        var fake = FakeOf(served, provider);
        var token = TokenOf(served, provider);
        var url = WithPorts(PartB.CloneUrlOf(provider), served);
        var folder = PartB.FolderOf(provider);
        var clone = Path.Combine(clones.Path, folder);

        // The launch asks nothing (D257, D350).
        AssertNoRequests(served, default, "The launch asked a fake (D257, D350)");

        // Step 1 (A5 or A6): the dialog over the Accounts section; Azure DevOps' organization typed; the
        // token typed on the real keyboard and masked.
        var dialog = app.OpenSignInDialog(provider, StepTimeout);
        Assert.Equal(DialogTitleOf(provider), dialog.Name);
        Assert.True(app.IsSettingsShown, "The settings screen is gone behind the sign-in dialog (D104).");
        var opened = azure ? PartA.FocusOpenedEmpty : PartA.FocusOpened;
        Assert.True(app.HasFocusOnScreen(opened), $"The dialog opened with the keyboard focus on {app.FocusedAutomationId() ?? "nothing"}, not on {opened} (10.4).");
        if (azure)
        {
            app.TypeOrganization(PartA.Organization);
        }

        app.ClickWithMouse(app.FindIn(dialog, "SignInToken"));
        app.WaitFor(() => app.HasFocusOnScreen("SignInToken"), "the keyboard focus in the token box after a click on it", StepTimeout);
        app.TypeToken(token);
        foreach (var text in app.Texts())
        {
            AssertNoToken(served, text, "A text element of the window");
        }

        AssertNoRequests(served, default, "Typing the token asked a fake");
        Compare(app, served, azure ? PartA.A06AzureTokenMasked : PartA.A05GitLabTokenMasked);

        // Step 1, its end: "Sign in" makes part A's check requests alone; the block names the login; Esc
        // closes the screen and the sign-in stays (D256).
        var mark = Mark(served);
        app.ConfirmSignIn(token, NetworkTimeout);
        AssertSignInRequests(served, provider, mark, azure ? PartA.Organization : null);
        app.WaitForAccountLogin(provider, LoginOf(provider), StepTimeout);
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitUntilGone(RealApp.SettingsDialogId, StepTimeout);

        // Step 2 (B1, B3): the clone form's button names the one signed-in provider (D354); a click on it
        // lists the provider's repositories, the filter focused, no switch, nothing selected.
        mark = Mark(served);
        app.ClickWithMouse(app.Find("CloneRepoButton"));
        app.WaitFor(
            () => app.HasFocusOnScreen("CloneUrlBox") && app.ShownElements("CloneFromGitHubButton").Count == 1,
            "the clone form with the focus in its URL box and its account's button",
            StepTimeout);
        var browse = app.ShownElements("CloneFromGitHubButton")[0];
        Assert.Equal((PartB.ButtonOf(provider), true), (browse.Name, browse.IsEnabled));
        app.ClickWithMouse(browse);
        app.MoveToNeutral();
        app.WaitFor(() => app.ShownElements(RealApp.CloneFromGitHubDialogId).Count == 1, $"the repository browser \"{PartB.TitleOf(provider)}\"", NetworkTimeout);
        var listRoute = azure ? PartB.AzureDevOpsListRoute : PartB.GitLabListRoute;
        WaitForFake(served, fake, mark.Of(provider), request => request.Route == listRoute, $"the list {listRoute}", NetworkTimeout);
        IReadOnlyList<string> rows = [.. PartB.RowsOf(provider).Select(row => row.FullName)];
        WaitTelling(
            app,
            () => app.CloneBrowserRowNames().SequenceEqual(rows) && app.HasFocusOnScreen("CloneFromGitHubFilter"),
            $"the browser's rows {string.Join(", ", rows)} with the filter focused",
            () => $"It shows {string.Join(", ", app.CloneBrowserRowNames())}; the focus is on {app.FocusedAutomationId() ?? "nothing"}.",
            NetworkTimeout);
        var browser = app.ShownElements(RealApp.CloneFromGitHubDialogId)[0];
        Assert.Equal(PartB.TitleOf(provider), browser.Name);
        Assert.Equal(string.Empty, app.BoxText("CloneFromGitHubFilter"));
        var choose = app.FindIn(browser, "ConfirmButton");
        Assert.Equal((PartB.Choose, false), (choose.Name, choose.IsEnabled));
        foreach (var id in SwitchIds)
        {
            Assert.True(app.ShownElements(id).Count == 0, $"The browser shows {id} with one account signed in (B3).");
        }

        AssertRequestsAlone(served, provider, mark, [listRoute], "Opening the browser");
        Compare(app, served, PartB.ListedPictureOf(provider));

        // Step 3 (B7): a click on part B's row, then "Choose": the URL as the API gives it and the folder's name.
        var chosen = PartB.ChosenOf(provider);
        app.ClickWithMouse(app.CloneBrowserRow(chosen));
        app.WaitFor(() => RealApp.IsSelected(app.CloneBrowserRow(chosen)), $"{chosen} to be selected", StepTimeout);
        choose = app.FindIn(app.ShownElements(RealApp.CloneFromGitHubDialogId)[0], "ConfirmButton");
        app.WaitFor(() => choose.IsEnabled, "Choose to be enabled with a row selected", StepTimeout);
        app.ClickWithMouse(choose);
        app.MoveToNeutral();
        app.WaitUntilGone(RealApp.CloneFromGitHubDialogId, StepTimeout);
        WaitTelling(
            app,
            () => app.BoxText("CloneUrlBox") == url && app.BoxText("CloneNameBox") == folder,
            $"the form to hold {url} and {folder}",
            () => $"It holds \"{Checked(served, app.BoxText("CloneUrlBox"), "The form's URL box")}\" and \"{Checked(served, app.BoxText("CloneNameBox"), "The form's name box")}\".");

        // Step 3, the clone: the parent folder typed; "Clone"; the clone opens in the tab within the
        // network's limit, with no credentials dialog; the helper answered git from the account as
        // x-access-token.
        app.ReplaceText("CloneParentBox", clones.Path);
        WaitTelling(
            app,
            () => app.BoxText("CloneParentBox") == clones.Path,
            "the parent folder's box to hold the clones' folder",
            () => $"It holds \"{Checked(served, app.BoxText("CloneParentBox"), "The form's parent box")}\".");
        var gitRequests = fake.GitRequests.Count;
        var otherGitRequests = (azure ? served.GitLab : (FakeHostingService)served.AzureDevOps).GitRequests.Count;
        var gitHubGitRequests = served.GitHub.GitRequests.Count;
        var logMark = app.LogMark();
        app.ClickWithMouse(app.Find("CloneStartButton"));
        app.MoveToNeutral();
        try
        {
            app.WaitForLogLine(Loaded(folder), NetworkTimeout);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead(folder), NetworkTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} The credentials dialog {(app.ShownElements("CredentialsDialog").Count == 0 ? "does not show" : "shows")}; the clone's error reads \"{Checked(served, string.Join(" | ", app.NamesOnScreen("CloneError")), "The clone's error")}\".", ex);
        }

        Assert.Empty(app.ShownElements("CredentialsDialog"));
        var answered = WithPorts(azure ? PartB.AzureDevOpsAnswered : PartB.GitLabAnswered, served);
        Assert.True(
            CountLinesSince(app, logMark, line => line.EndsWith(answered, StringComparison.Ordinal)) > 0,
            $"No line of the log since the click on Clone ends with \"{answered}\".");
        var asks = CountLinesSince(app, logMark, line => line.Contains($"{folder}: Clone: git asks (", StringComparison.Ordinal));
        Assert.True(asks == 0, $"{asks} lines of the log since the click on Clone hold \"{folder}: Clone: git asks (\".");
        var gitPath = azure ? PartB.AzureDevOpsGitPath : PartB.GitLabGitPath;
        var git = fake.GitRequests.Skip(gitRequests).Where(request => request.Path.StartsWith(gitPath, StringComparison.Ordinal)).ToList();
        foreach (var request in git)
        {
            AssertNoToken(served, request.Path, "The path of a git request to the fake");
            AssertNoToken(served, request.Query, "The query of a git request to the fake");
        }

        Assert.Contains(git, request => request is { GivenUserName: "x-access-token", Status: 200 });
        Assert.DoesNotContain(git, request => request.Status == 200 && request.GivenUserName is not null && request.GivenUserName != "x-access-token");
        if (!azure)
        {
            // GitLab's URL names no user: git sends none but the helper's (part B's B7).
            Assert.DoesNotContain(git, request => request.GivenUserName is not null && request.GivenUserName != "x-access-token");
        }

        Assert.Equal(otherGitRequests, (azure ? served.GitLab : (FakeHostingService)served.AzureDevOps).GitRequests.Count);
        Assert.Equal(gitHubGitRequests, served.GitHub.GitRequests.Count);

        // The clone's tab lists its requests before the close, so that the close cuts no request of it
        // (B7's note: the twin waits for D322's line too).
        WaitForListed(app, logMark, folder);

        // Step 4: run.txt names the helper the clone went through and whether its path holds a space.
        Assert.True(File.Exists(RealApp.HelperPath), $"The askpass helper {RealApp.HelperPath} does not exist.");
        Run.Note(string.Create(
            CultureInfo.InvariantCulture,
            $"{provider}: the clone of {chosen} went through {RealApp.HelperPath}; its path {(RealApp.HelperPath.Contains(' ', StringComparison.Ordinal) ? "holds a space" : "holds no space")}."));

        // Step 4, after the close: plain git in the clone: the URL as chosen, no credential key.
        Assert.Equal(0, app.Close());
        using var home = new TempDirectory("rw7b-clone-home");
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The pass needs git."));
        async Task<GitResult> GitAsync(params string[] arguments) =>
            await runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = clone, Environment = TempRepo.IsolatedEnvironment(home.Path) }, TestContext.Current.CancellationToken);

        var origin = (await GitAsync("config", "--get", "remote.origin.url")).StandardOutput.Trim();
        AssertNoToken(served, origin, "The clone's remote.origin.url");
        Assert.Equal(url, origin);
        var credential = await GitAsync("config", "--get-regexp", "^credential");
        AssertNoToken(served, credential.StandardOutput, "The clone's credential keys");
        Assert.Equal((1, string.Empty), (credential.ExitCode, credential.StandardOutput.Trim()));
        AssertCleanEnd(app, served, clones.Path);
    }

    /// <summary>
    /// "Requests and create" (14.4) for GitLab or Azure DevOps on one tab on <c>web-app</c> or
    /// <c>mobile-app</c>, prepared by <see cref="Phase7bExpectations.PrepareRequestsAsync"/> as part P's R
    /// methods and part N's checks are (the one prepared state, design 2.7):
    /// <list type="number">
    /// <item>P1's R step: <c>SignInThroughSettings</c> (Azure DevOps' organization prefilled from the
    /// tab's remote); D322's line; the sign-in's and the list's requests in order, GitHub's fake none
    /// (web-app's upstream, D257); the section's rows, the draft marked; compared with
    /// <c>p01a-gitlab-rows</c> or <c>p01c-azure-rows</c>, the badge in it.</item>
    /// <item>P10's R step: a real right-click on the section's header: its menu as part P gives it,
    /// compared with <c>p10a-gitlab-section-menu</c> or <c>p10c-azure-section-menu</c> keeping the
    /// pointer; Esc; a real right-click on <c>!21</c> or <c>!11</c>: its menu's labels; Esc; nothing
    /// asked.</item>
    /// <item>N1's and N2's R steps: a real click on Local branches › topic, a real right-click on its
    /// no-request, part N's menu item; the form with part N's head, base and the focused title;
    /// compared with <c>n02a-gitlab-form</c> or <c>n02b-azure-form</c>.</item>
    /// <item>N6's R step: "Create": the operation's end and two D322 lines since the mark; one create
    /// request as part N names it, 201, its body exactly part N's (GitLab's <c>project:</c> read the only
    /// request before it), no receive-pack (nothing pushed); the status as part N's,
    /// <c>OperationOfferButton</c> "Open in browser" never clicked, the new row first; compared with
    /// <c>n06a-gitlab-created</c> or <c>n06b-azure-created</c>; <c>AssertCleanEnd</c>.</item>
    /// </list>
    /// </summary>
    private async Task RequestsAndCreateAsync(HostingProvider provider)
    {
        var shown = PartN.Of(provider);
        var repository = PartP.RepositoryOf(provider);
        using var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, ownsCopy: true, ct: TestContext.Current.CancellationToken);
        await PrepareRequestsAsync(copy, provider);
        using var app = Launch(served, repository);
        var fake = FakeOf(served, provider);

        // Step 1 (P1): the launch asks nothing (D257); signed in through Settings; D322's line; the
        // sign-in's and the list's requests, route keys in order; the rows, the draft marked.
        AssertNoRequests(served, default, "The launch asked a fake (D257)");
        var logMark = app.LogMark();
        app.SignInThroughSettings(provider, TokenOf(served, provider), organization: null, LoginOf(provider), NetworkTimeout);
        WaitForListed(app, logMark, repository);
        var listedBefore = PartN.ListedOf(provider, PartP.RowsOf(provider).Count);
        var otherLists = CountLinesSince(app, logMark, line => AppLogFile.PullRequestsListed(repository).IsMatch(line) && !line.EndsWith(listedBefore, StringComparison.Ordinal));
        Assert.True(otherLists == 0, $"{otherLists} \"listed\" lines of {repository} since the sign-in do not end with \"{listedBefore}\".");
        var routes = PartP.SignInRoutesOf(provider);
        WaitForFake(served, fake, 0, request => request.Route == routes[^1], routes[^1], NetworkTimeout);
        AssertRequestsAlone(served, provider, default, routes, "The sign-in and the list");
        if (provider == HostingProvider.AzureDevOps)
        {
            // The organization as the tab's remote names it (ruling 16): the prefill, in its case.
            Assert.Equal(PartP.AzureDevOpsConnectionPath, Recorded(served, fake)[0].Path);
        }

        Assert.True(RecordedGitHub(served).Count == 0, "GitHub's fake was asked, though no GitHub account is signed in (web-app's upstream, D257).");
        IReadOnlyList<string> rows = [.. PartP.RowsOf(provider).Select(row => row.Name)];
        WaitTelling(
            app,
            () => app.PullRequestRowNames().SequenceEqual(rows),
            $"the section's rows {string.Join(", ", rows)}",
            () => $"It shows {string.Join(" | ", app.PullRequestRowNames())}.");
        Assert.Equal(
            PartP.RowsOf(provider).Select(row => (row.Name, row.Draft)),
            PartP.RowsOf(provider).Select(row => (row.Name, RealApp.ShowsDraftMark(app.PullRequestRow(row.Name)))));
        Compare(app, served, PartP.RowsPictureOf(provider));

        // Step 2 (P10): a real right-click at the centre of the section's header: its menu; the row's
        // menu's labels; asked for, they ask nothing.
        var mark = Mark(served);
        app.RightClickWithMouse(app.RefRow(PartP.TitleOf(provider)));
        AssertMenu(app, PartP.SectionMenu(provider), PartP.SectionMenuHeight);
        Compare(app, served, PartP.SectionMenuPictureOf(provider), keepPointer: true);
        CloseMenuWithEscape(app);
        var menuRow = PartP.MenuRowOf(provider);
        app.RightClickWithMouse(app.PullRequestRow(menuRow.Name));
        app.WaitForMenu();
        Assert.Equal(ItemLabels(PartP.RowMenuOf(provider)), app.MenuItemNames());
        CloseMenuWithEscape(app);
        AssertNoRequests(served, mark, $"Asking for the section's menu and {menuRow.Name}'s asked a fake");

        // Step 3 (N1, N2): a real click on Local branches › topic, a real right-click on its no-request,
        // then part N's item; the pointer leaves the form's controls at once.
        string[] folderPath = [.. shown.BranchRowPath.Take(shown.BranchRowPath.Count - 1)];
        string[] rowPath = [.. shown.BranchRowPath];
        app.ClickWithMouse(app.RefRow(folderPath));
        app.WaitFor(() => app.HasRefRow(rowPath), string.Join(" › ", rowPath), StepTimeout);
        mark = Mark(served);
        app.RightClickWithMouse(app.RefRow(rowPath));
        app.ClickMenuItem(shown.MenuItem);
        app.MoveToNeutral();

        // The form: From topic/no-request Into origin/main, the focused title holding its default.
        app.WaitFor(() => app.ShownElements(RealApp.CreatePullRequestDialogId).Count == 1, $"the form \"{shown.FormTitle}\"", StepTimeout);
        var form = app.ShownElements(RealApp.CreatePullRequestDialogId)[0];
        Assert.Equal(shown.FormTitle, form.Name);
        app.WaitForText("PullRequestHead", shown.Head, StepTimeout);
        app.WaitForText("PullRequestBase", shown.Base, NetworkTimeout);

        // The form's own box: the left panel's request rows name their titles PullRequestTitle too (7a's).
        string TitleBoxText() => app.FindIn(form, "PullRequestTitle").AsTextBox().Text;
        WaitTelling(
            app,
            () => RealApp.HasKeyboardFocus(app.FindIn(form, "PullRequestTitle")) && TitleBoxText() == shown.Title,
            $"the focused title box to hold \"{shown.Title}\"",
            () => $"It holds \"{Checked(served, TitleBoxText(), "The title box")}\"; the focus is on {app.FocusedAutomationId() ?? "nothing"}.");
        Assert.Empty(app.ShownElements("PullRequestHeadNote"));
        var create = app.FindIn(form, "ConfirmButton");
        Assert.Equal((PartN.Create, true), (create.Name, create.IsEnabled));
        AssertNoRequests(served, mark, "Opening the form asked a fake");
        Compare(app, served, shown.FormPicture);

        // Step 4 (N6): "Create". The operation's end, then two "listed" lines since the mark (7a's
        // ruling 51), each of the new count.
        mark = Mark(served);
        var gitRequests = fake.GitRequests.Count;
        logMark = app.LogMark();
        app.ClickWithMouse(create);
        app.MoveToNeutral();
        var ended = AppLogFile.OperationEnded(repository, shown.OperationTitle, OperationOutcome.Succeeded);
        var listed = AppLogFile.PullRequestsListed(repository);
        WaitTelling(
            app,
            () => app.CountLogLinesSince(ended, logMark) > 0,
            $"\"{shown.OperationTitle}\" to succeed",
            () => $"Since the mark the log says: {Checked(served, string.Join(" | ", app.LogLinesSince(logMark).Where(line => line.Contains(shown.OperationTitle, StringComparison.Ordinal))), "A line of the log about the operation")}; the form reads \"{Checked(served, string.Join(" | ", app.NamesOnScreen("PullRequestError")), "The form's error")}\".",
            OperationTimeout);
        WaitTelling(
            app,
            () => app.CountLogLinesSince(listed, logMark) >= 2,
            $"two \"listed\" lines of {repository} since the create (7a's ruling 51)",
            () => $"Since the mark the log has {app.CountLogLinesSince(listed, logMark)}.",
            NetworkTimeout);
        app.WaitUntilGone(RealApp.CreatePullRequestDialogId, StepTimeout);
        var listedAfter = PartN.ListedOf(provider, shown.CountAfterCreate);
        otherLists = CountLinesSince(app, logMark, line => listed.IsMatch(line) && !line.EndsWith(listedAfter, StringComparison.Ordinal));
        Assert.True(otherLists == 0, $"{otherLists} \"listed\" lines of {repository} since the create do not end with \"{listedAfter}\".");

        // The create's request: one, part N's route, 201, the provider's credential; GitLab's project:
        // read the only request before it; its body exactly part N's members.
        WaitForFake(served, fake, mark.Of(provider), request => request.Route == shown.CreateRoute, shown.CreateRoute, NetworkTimeout);
        var since = Recorded(served, fake, mark.Of(provider));
        var post = Assert.Single(since, request => request.Route == shown.CreateRoute);
        Assert.Equal(("POST", shown.CreateStatus, FormOf(provider)), (post.Method, post.Status, post.Authorization));
        Assert.True(post.Login == RequesterOf(served, provider), $"The create was not sent as the scenario's user ({provider}).");
        var before = provider == HostingProvider.GitLab ? new[] { PartN.GitLabSourceRoute } : Array.Empty<string>();
        Assert.Equal(before, since.Take(since.IndexOf(post)).Select(request => request.Route ?? request.Path));
        List<(string Member, string Json)> body;
        using (var json = JsonDocument.Parse(post.Body ?? "{}"))
        {
            body = [.. json.RootElement.EnumerateObject().Select(member => (member.Name, member.Value.GetRawText()))];
        }

        Assert.Equal(shown.Body.OrderBy(member => member.Member, StringComparer.Ordinal), body.OrderBy(member => member.Member, StringComparer.Ordinal));
        Assert.DoesNotContain(body, member => shown.AbsentMembers.Contains(member.Member));
        AssertNoRequestsBut(served, provider, mark, "The create asked another fake");

        // A receive-pack as x-access-token only if the branch was pushed first (it was not, part N's open point 35).
        var pushes = fake.GitRequests.Skip(gitRequests).Where(request => request.Path.EndsWith("git-receive-pack", StringComparison.Ordinal)).ToList();
        foreach (var request in pushes)
        {
            AssertNoToken(served, request.Path, "The path of a push to the fake");
            AssertNoToken(served, request.Query, "The query of a push to the fake");
        }

        if (shown.PushedFirst)
        {
            Assert.Contains(pushes, request => request is { GivenUserName: "x-access-token", Status: 200 });
        }
        else
        {
            Assert.Empty(pushes);
        }

        // The status bar's text and its offer, never clicked (ruling 29); the new row first in the section.
        WaitTelling(
            app,
            () => app.OperationStatusText() == shown.StatusText && app.NamesOnScreen("OperationOfferButton").SequenceEqual([PartN.OpenInBrowser]),
            $"the status bar \"{shown.StatusText}\" with \"{PartN.OpenInBrowser}\"",
            () => $"It reads \"{Checked(served, app.OperationStatusText(), "The status bar's text")}\" with \"{string.Join(" | ", app.NamesOnScreen("OperationOfferButton"))}\".");
        WaitTelling(
            app,
            () => app.PullRequestRowNames() is [var first, ..] && first == shown.NewRowName,
            $"\"{shown.NewRowName}\" first in the section",
            () => $"It shows {string.Join(" | ", app.PullRequestRowNames())}.");
        Compare(app, served, shown.CreatedPicture);
        AssertCleanEnd(app, served);
    }

    [GeneratedRegex(@" \((danger|disabled: "".*""|disabled)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex MenuMark();

    [GeneratedRegex(@"^(?<label>.*) \(disabled: ""(?<reason>.*)""\)$", RegexOptions.CultureInvariant)]
    private static partial Regex DisabledMark();
}
