using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace VisualCommit.LiveHostingTests;

/// <summary>Runs <see cref="AzureDevOpsLiveTests"/>' methods in 15.5's order (stage 1; stage 2's Check_3 takes its place after Check_2b_probes).</summary>
public sealed class AzureDevOpsLiveOrder : ITestCaseOrderer
{
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(AzureDevOpsLiveTests.Check_1_the_sandbox_and_its_leftovers),
        nameof(AzureDevOpsLiveTests.Check_1b_probes),
        nameof(AzureDevOpsLiveTests.Check_2_the_providers_and_the_session),
        nameof(AzureDevOpsLiveTests.Check_2b_probes),
        nameof(AzureDevOpsLiveTests.Check_4_a_clone_through_the_organization_host),
    ];

    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase => LiveSteps.Order(testCases, Methods);
}

/// <summary>
/// The live command against Azure DevOps, stage 1 (docs/phase-7b-design.md 15.5; D346, D352, D364,
/// D371 to D375): the sandbox and its leftovers, the probes beside the fake, the app's provider and
/// session signing in, listing, cloning, pushing and creating a draft, and a clone through the
/// organization's own host. Every request it or the app sends passes <see cref="AzureDevOpsReach"/>;
/// every note goes through <see cref="AzureDevOpsLiveRun"/>'s token search and screen. Every test asks
/// <see cref="LiveSettings.Require(HostingProvider)"/> first and fails without the variables.
/// </summary>
[TestCaseOrderer(typeof(AzureDevOpsLiveOrder))]
public sealed class AzureDevOpsLiveTests(AzureDevOpsLiveRun run) : IClassFixture<AzureDevOpsLiveRun>
{
    private const HostingProvider Provider = HostingProvider.AzureDevOps;

    /// <summary>A branch the sandbox never has, for the create from a missing branch.</summary>
    private const string MissingBranch = LiveLeftovers.Prefix + "no-such-branch";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    private static LiveAzureSandbox Sandbox => (LiveAzureSandbox)LiveSettings.Require(Provider);

    /// <summary>
    /// Check_1 (15.5): the variables gone; the identity read; the sandbox (200, its project private,
    /// D326, its default branch); then the leftovers (D324): active pull requests from the sandbox's
    /// own <c>visualcommit-live/</c> branches abandoned (they cannot be deleted), then one refs POST
    /// deleting those branches. The probes are Check_1b's (15.5's sums).
    /// </summary>
    [Fact]
    public async Task Check_1_the_sandbox_and_its_leftovers()
    {
        var sandbox = Sandbox;
        var store = new LiveRequestLog.Store();
        var reach = LiveReach.AzureDevOps(sandbox.Organization, sandbox.Project, sandbox.Repository);
        var step = "1, the variables";
        var problems = new List<string>();
        try
        {
            LiveSteps.AssertVariablesGone();
            run.Note($"Check_1. The sandbox {sandbox}; the token is {LiveSettings.KindOf(Provider, sandbox.Token)}.");
            using var api = new AzureDevOpsLiveApi(sandbox, reach, store, run.Screener!, run.Withheld);

            step = "1, the identity";
            await IdentityAsync(api, problems);

            step = "1, the sandbox";
            await ReadSandboxAsync(api);

            step = "2, the leftovers";
            var active = await api.ActivePullRequestsAsync(Cancelled);
            var leftovers = active.Where(LiveProviderLeftovers.IsLeftover).ToList();
            run.Note($"Active pull requests: {Show(active.Count)}, {Show(leftovers.Count)} of them from {LiveLeftovers.Prefix} branches of the sandbox and no fork.");
            foreach (var pull in leftovers)
            {
                var abandoned = await api.AbandonAsync(pull.Id, Cancelled);
                run.Note($"Leftover pull request !{Show(pull.Id)} abandoned: {abandoned}.");
                if (abandoned.Status != 200)
                {
                    problems.Add(api.Describe(abandoned, $"abandoning !{Show(pull.Id)}"));
                }
            }

            var refs = await api.RefsAsync("heads/" + LiveLeftovers.Prefix, Cancelled);
            var branches = LiveProviderLeftovers.Branches(refs);
            if (branches.Count > 0)
            {
                var deleted = await api.DeleteRefsAsync(branches, Cancelled);
                run.Note($"Leftover branches {string.Join(", ", branches.Select(reference => reference.Name))} deleted in one refs POST: {deleted}.");
                if (deleted.Status != 200)
                {
                    problems.Add(api.Describe(deleted, "deleting the leftover branches"));
                }
            }

            run.Note($"Leftovers of a stopped run: {Show(leftovers.Count)} pull requests abandoned, {Show(branches.Count)} branches deleted; {Show(refs.Count - branches.Count)} other refs not touched.");
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
    /// Check_1's probes (15.7), in a method of their own for 15.5's sums: connectionData with and
    /// without Suppress and at <c>7.1</c>; the core area anonymously and with the token; D373's
    /// made-up values to connectionData and to the sandbox's <c>info/refs</c> (sent as git sends it),
    /// each with and without Suppress; D374's made-up organization; the sandbox project's repository
    /// list. Each beside the fake's answer; a broken premise fails the test after its note. Its first
    /// call, connectionData with the token, is the run's identity read when Check_1 did not make it.
    /// </summary>
    [Fact]
    public async Task Check_1b_probes()
    {
        var sandbox = Sandbox;
        var store = new LiveRequestLog.Store();
        var reach = LiveReach.AzureDevOps(sandbox.Organization, sandbox.Project, sandbox.Repository);
        var step = "1b, the probes";
        var problems = new List<string>();
        try
        {
            using var api = new AzureDevOpsLiveApi(sandbox, reach, store, run.Screener!, run.Withheld);
            run.Note($"Check_1b. D374's made-up organization of this run: {reach.MadeUpOrganization}.");
            await using var fake = await StartFakeAsync(sandbox, null, null);
            await ProbeIdentityAsync(api, fake, problems);
            var agent = LiveSteps.AgentOf(await LiveSteps.GitVersionAsync());
            run.Note($"The info/refs probes are sent as git sends them: User-Agent {agent}, Accept */*, Pragma no-cache, Git-Protocol version=2 (g1 G0.7).");
            await ProbeMadeUpAsync(api, fake, agent, problems);
            await ProbeMadeUpOrganizationAsync(run, api, fake?.Api, fake?.Names ?? [], reach.MadeUpOrganization, problems);
            var list = await ProbeAsync(run, api, fake?.Api, fake?.Names ?? [], "the sandbox project's repositories (ListProject's list)", a => a.RepositoriesAsync(Cancelled));
            var rows = list?.Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [];
            run.Note($"The project's list: {Show(rows.Count)} rows, the sandbox's row {(rows.Any(row => row.TryGetProperty("name", out var name) && string.Equals(name.GetString(), sandbox.Repository, StringComparison.OrdinalIgnoreCase)) ? "among them" : "missing")}.");
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
    /// Check_2 (15.5): the app with Azure DevOps on (its handler the log over the default one, behind
    /// the guard) signs in through the session, lists the sandbox, clones it through the clone form and
    /// the app's helper, loads its pull requests, pushes D325's commit with "Push {branch}…" and creates
    /// a draft through the repository's hosting; then the tripwires. Its steps 8 and 9, the probes of
    /// the create and the clean-up, are Check_2b's (15.5's sums): Check_2 hands its branches to the run
    /// before the push and its pull request once created, and cleans up nothing itself.
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
        var reach = LiveReach.AzureDevOps(sandbox.Organization, sandbox.Project, sandbox.Repository);
        using var api = new AzureDevOpsLiveApi(sandbox, reach, ownRequests, run.Screener!, run.Withheld);
        using var app = new LiveApp(run, sandbox, AppOptions(sandbox, appRequests, reach));
        var step = "1, the identity";
        var problems = new List<string>();
        ExceptionDispatchInfo? failure = null;
        try
        {
            await IdentityAsync(api, problems);
            run.Note($"Check_2. The branch {branch}.");
            var repository = await ReadSandboxAsync(api);
            run.Made = new LiveMade(branch, branch + LiveSteps.EvenSuffix, repository.DefaultBranch, title);
            step = "1, the start";
            await app.StartAsync(appRequests);

            step = "2, the sign-in";
            var account = await app.SignInAsync(sandbox.Organization, appRequests, problems);
            run.Check(string.Equals(account.Login, sandbox.Organization, StringComparison.OrdinalIgnoreCase), () => $"The account's login is {account.Login}, not the sandbox's organization.");
            run.Note($"The login {(string.Equals(account.Login, sandbox.Organization, StringComparison.Ordinal) ? "spells" : "does not spell")} the organization as the variable does.");
            run.Check(LiveProviderPremises.IsMemberId(account.UserId), () => "The account's UserId is not a member's GUID (g1 G0.3).");

            step = "3, the list";
            var row = await app.ListAsync(sandbox.FullName);
            run.Check(string.Equals(row.CloneUrl.UserInfo, sandbox.Organization, StringComparison.OrdinalIgnoreCase), () => $"The listed CloneUrl's user is {row.CloneUrl.UserInfo}, not the organization (ruling 15).");

            step = "4, the clone";
            var opened = await app.CloneAsync(row.CloneUrl.AbsoluteUri);

            step = "5, the load";
            await app.LoadAsync(opened);

            step = "6, the push";
            var head = await app.PushAsync(opened, branch, stamp, title);

            step = "7, the create";
            var pull = await app.CreateAsync(opened, title, branch, repository.DefaultBranch, appRequests, "/pullrequests", problems);
            run.Made = run.Made with { Number = pull.Number, UserId = account.UserId, Head = head };
        }
        catch (Exception ex)
        {
            failure = run.FailureOf(ex, step);
            run.Note($"Check_2 failed in step {step}: {ex.GetType().Name}: {ex.Message}; Check_2b_probes cleans up what it made.");
        }
        finally
        {
            app.Close();
        }

        app.Tripwires(problems);
        LiveSteps.Finish(run, appRequests, reach, problems, failure, ownRequests, signInStarted: app.SignInStarted);
    }

    /// <summary>
    /// Check_2's steps 8 and 9 (15.5, 15.7), in a method of their own for 15.5's sums: the create's
    /// probes beside the fake's own draft on its namesake of the run's branch (its status,
    /// <c>mergeStatus</c>, <c>isDraft</c>, <c>createdBy</c> and head; when the merge ref appears; a
    /// duplicate, a missing branch; the abandon and the merge ref's removal; an empty title from the
    /// branch, which then has no active pull request; nothing ahead, from a branch made at the default
    /// branch's tip), then, in <c>finally</c>, the clean-up of what Check_2 handed over, whether Check_2
    /// passed or not. A stopped run's leftovers are Check_1's (D324).
    /// </summary>
    [Fact]
    public async Task Check_2b_probes()
    {
        var sandbox = Sandbox;
        var store = new LiveRequestLog.Store();
        var reach = LiveReach.AzureDevOps(sandbox.Organization, sandbox.Project, sandbox.Repository);
        using var api = new AzureDevOpsLiveApi(sandbox, reach, store, run.Screener!, run.Withheld);
        var made = run.Made;
        run.Made = null;
        var step = "2b, the identity";
        var problems = new List<string>();
        ExceptionDispatchInfo? failure = null;
        try
        {
            await IdentityAsync(api, problems);
            run.Check(made is not null, () => "Check_2 handed over nothing: it did not run in this process, so there is nothing to probe or clean up.");
            step = "8, the probes";
            if (made!.Number is not { } number)
            {
                run.Note("Check_2b. Check_2 created no pull request, so there is nothing to probe; its branches are cleaned up.");
            }
            else
            {
                run.Note($"Check_2b. The probes of !{Show(number)} from {made.Branch}.");
                await using var fake = await StartFakeAsync(sandbox, made.Branch, made.Even);
                await ProbeCreateAsync(api, fake, made, number, problems);
            }
        }
        catch (Exception ex)
        {
            failure = run.FailureOf(ex, step);
            run.Note($"Check_2b failed in step {step}: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (made is not null)
            {
                await CleanUpAsync(api, [made.Branch, made.Even], problems);
            }
        }

        LiveSteps.Finish(run, store, reach, problems, failure);
    }

    /// <summary>
    /// Check_4 (15.5, D363): after the identity read, started and signed in as Check_2, the app clones
    /// <c>https://&lt;organization&gt;.visualstudio.com/&lt;project&gt;/_git/&lt;repository&gt;</c> with no
    /// credentials dialog, then lists from <c>dev.azure.com</c>: the only test of
    /// <c>OrganizationHost</c> against the service.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_4_a_clone_through_the_organization_host()
    {
        var sandbox = Sandbox;
        var appRequests = new LiveRequestLog.Store();
        var ownRequests = new LiveRequestLog.Store();
        var reach = LiveReach.AzureDevOps(sandbox.Organization, sandbox.Project, sandbox.Repository);
        using var api = new AzureDevOpsLiveApi(sandbox, reach, ownRequests, run.Screener!, run.Withheld);
        using var app = new LiveApp(run, sandbox, AppOptions(sandbox, appRequests, reach));
        var step = "1, the identity";
        var problems = new List<string>();
        ExceptionDispatchInfo? failure = null;
        try
        {
            await IdentityAsync(api, problems);
            run.Note("Check_4. A clone through the organization's host.");
            step = "1, the start";
            await app.StartAsync(appRequests);
            step = "2, the sign-in";
            await app.SignInAsync(sandbox.Organization, appRequests, problems);
            step = "3, the clone";
            var url = $"https://{sandbox.Organization}.visualstudio.com/{Uri.EscapeDataString(sandbox.Project)}/_git/{Uri.EscapeDataString(sandbox.Repository)}";
            var opened = await app.CloneAsync(url);
            run.Note("The clone through the organization's host needed no credentials dialog; git's credential get was answered from the account.");
            step = "4, the list";
            await app.ListAsync(sandbox.FullName);
            await app.LoadAsync(opened);
        }
        catch (Exception ex)
        {
            failure = run.FailureOf(ex, step);
            run.Note($"Check_4 failed in step {step}: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            app.Close();
        }

        app.Tripwires(problems);
        LiveSteps.Finish(run, appRequests, reach, problems, failure, ownRequests, signInStarted: app.SignInStarted);
    }

    /// <summary>15.5 step 1's options: Azure DevOps alone, <c>ListProject</c> the sandbox's project, its handler the log over the default one behind the guard.</summary>
    internal static HostingOptions AppOptions(LiveAzureSandbox sandbox, LiveRequestLog.Store requests, LiveReach reach) =>
        HostingOptions.Off with
        {
            AzureDevOps = AzureDevOpsEndpoints.Service with { ListProject = sandbox.Project },
            CreateAzureDevOpsHandler = () => new LiveRequestLog(HostingOptions.DefaultHandler(AzureDevOpsEndpoints.Service.WebBase)(), requests, reach),
        };

    // ---- The sandbox and the identity --------------------------------------------------------

    /// <summary>
    /// The identity read (connectionData with the token), each method's first call and once per run
    /// (15.3), so that the screen knows the user's id, the host's and the addresses before any note:
    /// 200, a member's id, its member names noted. Nothing when the run made it already.
    /// </summary>
    private Task IdentityAsync(AzureDevOpsLiveApi api, List<string> problems) => IdentityOnceAsync(run, api, problems);

    /// <summary><see cref="IdentityAsync"/> for <paramref name="run"/>; internal for <see cref="LiveSafetyTests"/>.</summary>
    internal static async Task IdentityOnceAsync(LiveRun run, AzureDevOpsLiveApi api, List<string> problems)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(problems);
        if (run.IdentityRead)
        {
            return;
        }

        var answer = await api.ConnectionDataAsync(LiveCredential.Token, Cancelled);
        if (answer is not { Status: 200, Json: { ValueKind: JsonValueKind.Object } json } || !json.TryGetProperty("authenticatedUser", out var user))
        {
            throw new LiveServiceException(api.Describe(answer, "connectionData") + ": 401 or 203 means the token is wrong, expired or for another organization.");
        }

        run.IdentityRead = true;
        run.Note($"The identity (connectionData): {answer}; authenticatedUser's members: {string.Join(", ", user.EnumerateObject().Select(member => member.Name))}.");
        var id = user.TryGetProperty("id", out var value) ? value.GetString() : null;
        if (!LiveProviderPremises.IsMemberId(id))
        {
            problems.Add("connectionData's authenticatedUser.id is not a member's GUID (g1 G0.3).");
        }
    }

    /// <summary>The sandbox repository, once per run: 200, its project private (D326), its default branch; <c>remoteUrl</c>'s user the organization.</summary>
    private async Task<LiveRepository> ReadSandboxAsync(AzureDevOpsLiveApi api)
    {
        if (run.Repository is { } known)
        {
            return known;
        }

        var answer = await api.RepositoryAsync(Cancelled);
        if (answer is not { Status: 200, Json: { ValueKind: JsonValueKind.Object } json })
        {
            throw new LiveServiceException(api.Describe(answer, "the sandbox repository") + ".");
        }

        var visibility = json.TryGetProperty("project", out var project) && project.TryGetProperty("visibility", out var seen) ? seen.GetString() ?? "not given" : "not given";
        var branch = json.TryGetProperty("defaultBranch", out var head) ? head.GetString() ?? string.Empty : string.Empty;
        var remote = json.TryGetProperty("remoteUrl", out var url) && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps ? parsed : null;
        run.Note($"The sandbox repository: {answer}; project visibility {visibility}, default branch {branch}; remoteUrl's user {(remote is null ? "unreadable" : string.Equals(remote.UserInfo, api.Sandbox.Organization, StringComparison.Ordinal) ? "the organization, spelled as the variable does" : string.Equals(remote.UserInfo, api.Sandbox.Organization, StringComparison.OrdinalIgnoreCase) ? "the organization, spelled otherwise" : "not the organization")}.");
        run.Check(remote is not null && string.Equals(remote.UserInfo, api.Sandbox.Organization, StringComparison.OrdinalIgnoreCase), () => "The sandbox's remoteUrl's user is not the organization (15.7's premise).");
        run.Check(string.Equals(visibility, "private", StringComparison.OrdinalIgnoreCase), () => $"The sandbox's project is {visibility}: D326 wants it private, so that the clone proves the token reached git.");
        run.Check(branch.StartsWith(LiveLeftovers.HeadsPrefix, StringComparison.Ordinal), () => "The sandbox has no default branch.");
        var repository = new LiveRepository(api.Sandbox.FullName, true, visibility, branch[LiveLeftovers.HeadsPrefix.Length..], true);
        run.Repository = repository;
        return repository;
    }

    // ---- The probes (15.7) -------------------------------------------------------------------

    private async Task<LiveAzureFake?> StartFakeAsync(LiveAzureSandbox sandbox, string? branch, string? even)
    {
        try
        {
            return await LiveAzureFake.StartAsync(sandbox.Project, sandbox.Repository, branch, even, Cancelled);
        }
        catch (Exception ex) when (!Cancelled.IsCancellationRequested)
        {
            run.Note($"The fake of Azure DevOps did not start, so no probe has its answer beside the service's: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>One probe: the service's answer, its headers and body, beside the fake's; null without an answer.</summary>
    private Task<LiveServiceAnswer?> ProbeAsync(AzureDevOpsLiveApi api, LiveAzureFake? fake, string what, Func<AzureDevOpsLiveApi, Task<LiveServiceAnswer>> probe) =>
        ProbeAsync(run, api, fake?.Api, fake?.Names ?? [], what, probe);

    /// <summary>One probe of <paramref name="api"/> beside <paramref name="fake"/>'s (null: not started), noted in <paramref name="run"/>; internal for <see cref="LiveSafetyTests"/>.</summary>
    internal static async Task<LiveServiceAnswer?> ProbeAsync(LiveRun run, AzureDevOpsLiveApi api, AzureDevOpsLiveApi? fake, IReadOnlyList<string> fakeNames, string what, Func<AzureDevOpsLiveApi, Task<LiveServiceAnswer>> probe)
    {
        LiveServiceAnswer answer;
        try
        {
            answer = await probe(api);
        }
        catch (LiveServiceException ex)
        {
            run.Note($"Azure DevOps' answer to {what}: none: {ex.Message}");
            return null;
        }

        var beside = await LiveSteps.FakeBesideAsync(answer, fake is null ? null : () => probe(fake), [api.Sandbox.Organization, api.Sandbox.Project, api.Sandbox.Repository, api.MadeUpOrganization], fakeNames);
        run.Note($"Azure DevOps' answer to {what}: {answer}; {answer.HeadersForNotes}; the fake: {beside}. {answer.BodyForNotes}");
        return answer;
    }

    private async Task ProbeIdentityAsync(AzureDevOpsLiveApi api, LiveAzureFake? fake, List<string> problems)
    {
        var identity = await ProbeAsync(api, fake, "connectionData with the token and Suppress", a => a.ConnectionDataAsync(LiveCredential.Token, Cancelled));
        if (identity?.Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty("authenticatedUser", out var user))
        {
            run.IdentityRead |= identity.Status == 200;
            run.Note($"The identity's member names: {string.Join(", ", user.EnumerateObject().Select(member => member.Name))}.");
        }

        await ProbeAsync(api, fake, "connectionData with the token, without Suppress", a => a.ConnectionDataAsync(LiveCredential.Token, Cancelled, suppress: false));
        await ProbeAsync(api, fake, $"connectionData with the token at {AzureDevOpsApi.Version}", a => a.ConnectionDataAsync(LiveCredential.Token, Cancelled, version: AzureDevOpsApi.Version));
        await ProbeAsync(api, fake, "the core area, as A1's spelling read sends it ({o}/_apis/resourceAreas/…), without a credential", a => a.CoreAreaAsync(LiveCredential.None, Cancelled));
        var area = await ProbeAsync(api, fake, "the core area, as A1's spelling read sends it, with the token", a => a.CoreAreaAsync(LiveCredential.Token, Cancelled));
        var location = area?.Json is { ValueKind: JsonValueKind.Object } areaJson && areaJson.TryGetProperty("locationUrl", out var url) && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps
            ? parsed.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;
        run.Note($"The core area's locationUrl {(location is null ? "is unreadable" : string.Equals(location, api.Sandbox.Organization, StringComparison.Ordinal) ? "names the organization as the variable spells it" : string.Equals(location, api.Sandbox.Organization, StringComparison.OrdinalIgnoreCase) ? "names the organization, spelled otherwise" : "names another first segment")}.");
        if (location is null || !string.Equals(location, api.Sandbox.Organization, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("The core area's locationUrl's first segment is not the organization (15.7's premise, D360).");
        }
    }

    /// <summary>D373's made-up values, a short plain one and an 84-character one in Azure DevOps' form, each with and without Suppress, to connectionData and the sandbox's info/refs.</summary>
    private async Task ProbeMadeUpAsync(AzureDevOpsLiveApi api, LiveAzureFake? fake, string agent, List<string> problems)
    {
        var shaped = LiveMadeUp.AzureShaped(api.Sandbox.Token);
        foreach (var value in new[] { LiveMadeUp.ShortValue, LiveCredential.MadeUp(shaped, "a made-up value of 84 characters in Azure DevOps' form") })
        {
            foreach (var suppress in new[] { true, false })
            {
                var with = suppress ? "with Suppress" : "without Suppress";
                foreach (var (target, probe) in new (string, Func<AzureDevOpsLiveApi, Task<LiveServiceAnswer>>)[]
                {
                    ("connectionData", a => a.ConnectionDataAsync(value, Cancelled, suppress)),
                    ("the sandbox's info/refs", a => a.InfoRefsAsync(value, suppress, agent, Cancelled)),
                })
                {
                    var answer = await ProbeAsync(api, fake, $"{target} with {value} {with}", probe);
                    if (answer is not null && !LiveProviderPremises.MadeUpRefused(answer))
                    {
                        problems.Add($"{target} with {value} {with} earned {answer}, a 2xx ruling 16 would take (D373).");
                    }
                }
            }
        }
    }

    /// <summary>D374's probe: the made-up organization's connectionData without a credential, then, only after its 404, with the token, which must answer 404 too.</summary>
    internal static async Task ProbeMadeUpOrganizationAsync(LiveRun run, AzureDevOpsLiveApi api, AzureDevOpsLiveApi? fake, IReadOnlyList<string> fakeNames, string organization, List<string> problems)
    {
        var anonymous = await ProbeAsync(run, api, fake, fakeNames, $"connectionData of the made-up organization {organization} without a credential", a => a.ConnectionDataAsync(LiveCredential.None, Cancelled, organization: a == api ? organization : a.MadeUpOrganization));
        if (anonymous?.Status != 404)
        {
            run.Note($"D374's read with the token: skipped, since the read without a credential answered {anonymous?.ToString() ?? "nothing"}, not 404; nothing was sent with the token.");
            return;
        }

        var token = await ProbeAsync(run, api, fake, fakeNames, $"connectionData of the made-up organization {organization} with the token", a => a.ConnectionDataAsync(LiveCredential.Token, Cancelled, organization: a == api ? organization : a.MadeUpOrganization));
        if (token is not null && token.Status != 404)
        {
            problems.Add($"D374's read with the token answered {token}, not 404 as the read without one did (A1's NotFound, 6.4).");
        }
    }

    /// <summary>
    /// Check_2b's probes of the create (15.7), each beside the fake's own draft from its namesake of the
    /// run's branch: the read back (status, <c>mergeStatus</c>, <c>isDraft</c>, <c>createdBy</c>, the
    /// head); when the merge ref appears (polled within <see cref="LiveSteps.PollBudget"/>); a duplicate
    /// and a missing branch while the draft is active; the abandon and the merge ref's removal (polled
    /// likewise); an empty title from the branch, which then has no active pull request, so that a
    /// duplicate's refusal cannot stand in for it; nothing ahead, from <see cref="LiveMade.Even"/> made
    /// at the default branch's tip by one refs POST whose update took. The refs delete is the clean-up's.
    /// </summary>
    private async Task ProbeCreateAsync(AzureDevOpsLiveApi api, LiveAzureFake? fake, LiveMade made, int id, List<string> problems)
    {
        var fakeId = await FakeDraftAsync(fake, made);
        var side = fakeId is null ? null : fake;
        string Target(AzureDevOpsLiveApi a) => a == api ? made.Target : LiveAzureFake.DefaultBranch;
        int Id(AzureDevOpsLiveApi a) => a == api ? id : fakeId.GetValueOrDefault();

        var read = await ProbeAsync(api, side, $"the read of the created !{Show(id)}", a => a.PullRequestAsync(Id(a), Cancelled));
        string? Text(string name) => read?.Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty(name, out var value) ? (value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()) : null;
        string? Inner(string name, string member) => read?.Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty(name, out var outer) && outer.ValueKind == JsonValueKind.Object && outer.TryGetProperty(member, out var value) ? value.GetString() : null;
        var createdBy = Inner("createdBy", "id");
        var head = Inner("lastMergeSourceCommit", "commitId");
        run.Note($"The created !{Show(id)}: status {Text("status") ?? "none"}, mergeStatus {Text("mergeStatus") ?? "none"}, isDraft {Text("isDraft") ?? "none"}, createdBy.id {(string.Equals(createdBy, made.UserId, StringComparison.OrdinalIgnoreCase) ? "the account's UserId" : "another id")}, the head {head ?? "none"} ({(string.Equals(head, made.Head, StringComparison.OrdinalIgnoreCase) ? "the pushed commit" : "not the pushed commit")}).");
        if (!string.Equals(createdBy, made.UserId, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("The create's createdBy.id is not the account's UserId (15.7's premise).");
        }

        var merge = $"pull/{Show(id)}/merge";
        var appeared = await LiveSteps.PollAsync(api, () => api.RefsAsync(merge, Cancelled), refs => refs.Count > 0);
        run.Note($"The merge ref refs/{merge} {(appeared.Done ? "appeared" : "had not appeared")} by read {Show(appeared.Reads)}, {appeared.Took.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s after the read back{(appeared.Failure is { } failed ? $" ({failed})" : string.Empty)}; the fake's: {await FakeMergeRefAsync(side, fakeId)}.");

        var duplicate = await ProbeAsync(api, side, "the duplicate create (409 GitPullRequestExistsException expected)", a => a.CreateAsync(made.Branch, Target(a), made.Title, true, Cancelled));
        if (duplicate is not null && !LiveProviderPremises.IsAzureDuplicate(duplicate))
        {
            problems.Add($"The duplicate create answered {duplicate} with typeKey {duplicate.TypeKey ?? "none"}, not 409 GitPullRequestExistsException.");
        }

        var missing = await ProbeAsync(api, side, "the create from a missing branch (400 GitPullRequestCannotBeActivated expected)", a => a.CreateAsync(MissingBranch, Target(a), made.Title, true, Cancelled));
        if (missing is not null && !LiveProviderPremises.IsAzureMissingBranch(missing))
        {
            problems.Add($"The create from a missing branch answered {missing} with typeKey {missing.TypeKey ?? "none"}, not 400 GitPullRequestCannotBeActivated.");
        }

        var abandoned = await ProbeAsync(api, side, $"the abandon of !{Show(id)}", a => a.AbandonAsync(Id(a), Cancelled));
        if (abandoned is not { Status: 200 })
        {
            problems.Add(abandoned is null ? $"The abandon of !{Show(id)} had no answer." : api.Describe(abandoned, $"abandoning !{Show(id)}"));
            run.Note("The merge ref's removal and the empty title: skipped, since the abandon did not take; the clean-up abandons the draft.");
        }
        else
        {
            var gone = await LiveSteps.PollAsync(api, () => api.RefsAsync(merge, Cancelled), refs => refs.Count == 0);
            run.Note($"After the abandon, the merge ref {(gone.Done ? "was gone" : "was still there")} by read {Show(gone.Reads)}, {gone.Took.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s after the abandon{(gone.Failure is { } lost ? $" ({lost})" : string.Empty)}; the fake's: {await FakeMergeRefAsync(side, fakeId)}.");
            await ProbeAsync(api, side, "the create with an empty title, from the branch without an active pull request", a => a.CreateAsync(made.Branch, Target(a), string.Empty, true, Cancelled));
        }

        var tip = (await api.RefsAsync("heads/" + made.Target, Cancelled)).FirstOrDefault(reference => reference.Name == LiveLeftovers.HeadsPrefix + made.Target);
        if (tip.ObjectId is not { Length: > 0 })
        {
            run.Note($"Nothing ahead: skipped, since the refs read named no tip of {made.Target}.");
            return;
        }

        var even = await api.MakeBranchAsync(made.Even, tip.ObjectId, Cancelled);
        if (!AzureDevOpsLiveApi.RefUpdated(even))
        {
            run.Note($"Nothing ahead: skipped, since the refs POST making {made.Even} at {made.Target}'s tip did not take: {even}. {even.BodyForNotes}");
            return;
        }

        run.Note($"{made.Even} made at {made.Target}'s tip by one refs POST: {even}, its update took.");
        await ProbeAsync(api, side, $"the create with nothing ahead of {made.Target}, from {made.Even}", a => a.CreateAsync(made.Even, Target(a), made.Title, true, Cancelled));
    }

    /// <summary>The fake's own draft from its namesake of the run's branch, for the probes beside: its id, or null (noted) when the fake did not start or did not create it.</summary>
    private async Task<int?> FakeDraftAsync(LiveAzureFake? fake, LiveMade made)
    {
        if (fake is null)
        {
            return null;
        }

        try
        {
            var created = await fake.Api.CreateAsync(made.Branch, LiveAzureFake.DefaultBranch, made.Title, true, Cancelled);
            if (created is { Status: 200 or 201, Json: { ValueKind: JsonValueKind.Object } json } && json.TryGetProperty("pullRequestId", out var id) && id.TryGetInt32(out var number))
            {
                run.Note($"The fake's own draft from its {made.Branch}: {created.Status.ToString(CultureInfo.InvariantCulture)}.");
                return number;
            }

            run.Note($"The fake did not create its own draft ({created}), so the probes go to the service alone. {created.BodyForNotes}");
        }
        catch (LiveServiceException ex)
        {
            run.Note($"The fake did not create its own draft ({ex.Message}), so the probes go to the service alone.");
        }

        return null;
    }

    /// <summary>Whether the fake's merge ref of its draft is there (one read), or why not asked.</summary>
    private static async Task<string> FakeMergeRefAsync(LiveAzureFake? fake, int? id)
    {
        if (fake is null || id is not { } number)
        {
            return "not asked";
        }

        try
        {
            return (await fake.Api.RefsAsync($"pull/{Show(number)}/merge", Cancelled)).Count > 0 ? "there" : "not there";
        }
        catch (LiveServiceException ex)
        {
            return "no answer: " + ex.Message;
        }
    }

    /// <summary>15.5 step 9, Check_2b's <c>finally</c>: the run's pull requests abandoned (by their source branch, never a fork's), then one refs POST deleting the run's branches.</summary>
    private async Task CleanUpAsync(AzureDevOpsLiveApi api, IReadOnlyList<string> branches, List<string> problems)
    {
        try
        {
            var active = await api.ActivePullRequestsAsync(CancellationToken.None);
            foreach (var pull in active.Where(pull => !pull.FromFork && branches.Any(branch => pull.SourceRef == LiveLeftovers.HeadsPrefix + branch)))
            {
                var abandoned = await api.AbandonAsync(pull.Id, CancellationToken.None);
                run.Note($"Clean-up: !{Show(pull.Id)} abandoned: {abandoned}.");
                if (abandoned.Status != 200)
                {
                    problems.Add(api.Describe(abandoned, $"abandoning !{Show(pull.Id)}"));
                }
            }

            var refs = (await api.RefsAsync("heads/" + LiveLeftovers.Prefix, CancellationToken.None))
                .Where(reference => branches.Any(branch => reference.Name == LiveLeftovers.HeadsPrefix + branch)).ToList();
            if (refs.Count > 0)
            {
                var deleted = await api.DeleteRefsAsync(refs, CancellationToken.None);
                run.Note($"Clean-up: the refs POST deleting {Show(refs.Count)} branches: {deleted}. {deleted.BodyForNotes}");
                if (deleted.Status != 200)
                {
                    problems.Add(api.Describe(deleted, "deleting the run's branches"));
                }
            }
        }
        catch (Exception ex)
        {
            problems.Add($"The clean-up failed: {ex.GetType().Name}: {api.Screen(ex.Message)}");
        }
    }

    private static string Show(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>What the new providers' clean-up may remove (D324, 15.5): only <c>refs/heads/visualcommit-live/</c> branches of the sandbox and their requests, never a fork's.</summary>
internal static class LiveProviderLeftovers
{
    /// <summary>An active Azure DevOps pull request from a <c>visualcommit-live/</c> branch of the sandbox, not from a fork.</summary>
    public static bool IsLeftover(LiveAzurePullRequest pull) =>
        pull.SourceRef.StartsWith(LiveLeftovers.HeadsPrefix + LiveLeftovers.Prefix, StringComparison.Ordinal) && pull.SourceRef.Length > LiveLeftovers.HeadsPrefix.Length + LiveLeftovers.Prefix.Length && !pull.FromFork;

    /// <summary>An opened GitLab merge request from a <c>visualcommit-live/</c> branch of the sandbox itself.</summary>
    public static bool IsLeftover(LiveMergeRequest request) =>
        request.SourceBranch.StartsWith(LiveLeftovers.Prefix, StringComparison.Ordinal) && request.SourceBranch.Length > LiveLeftovers.Prefix.Length && !request.FromFork;

    /// <summary>The refs under <c>refs/heads/visualcommit-live/</c>, whatever else the filter returned.</summary>
    public static List<(string Name, string ObjectId)> Branches(IEnumerable<(string Name, string ObjectId)> refs) =>
        [.. refs.Where(reference => reference.Name.StartsWith(LiveLeftovers.HeadsPrefix + LiveLeftovers.Prefix, StringComparison.Ordinal) && reference.Name.Length > LiveLeftovers.HeadsPrefix.Length + LiveLeftovers.Prefix.Length)];
}

/// <summary>Steps both new providers' live tests share (15.5): the order, the stamp, the variables' check, the fake beside, the end of a test.</summary>
internal static class LiveSteps
{
    public const string Description = "Opened and closed by VisualCommit's live command (tests/VisualCommit.LiveHostingTests). Nothing to review.";

    /// <summary>The identity of the command's commit (D325).</summary>
    public const string IdentityName = "VisualCommit live test";

    /// <summary>The suffix of the branch with nothing ahead, made at the default branch's tip for Check_2b's probe (15.7).</summary>
    public const string EvenSuffix = "-even";

    /// <summary>How long one poll of a merge ref may take in all, its reads included (15.5's sums count it once).</summary>
    public static readonly TimeSpan PollBudget = TimeSpan.FromSeconds(30);

    /// <summary>The pause between a poll's reads.</summary>
    public static readonly TimeSpan PollPause = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Reads until <paramref name="done"/> holds or <paramref name="budget"/> (<see cref="PollBudget"/>)
    /// is spent, each read held to what is left of it and <paramref name="pause"/> (<see cref="PollPause"/>)
    /// between reads: whether it came, after how many reads and how long, and why a read had no answer.
    /// </summary>
    public static async Task<(bool Done, int Reads, TimeSpan Took, string? Failure)> PollAsync<T>(LiveServiceApi api, Func<Task<T>> read, Func<T, bool> done, TimeSpan? budget = null, TimeSpan? pause = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(done);
        var total = budget ?? PollBudget;
        var limit = api.Limit;
        var clock = Stopwatch.StartNew();
        var reads = 0;
        try
        {
            while (total - clock.Elapsed is var left && left > TimeSpan.Zero)
            {
                api.Limit = left;
                reads++;
                try
                {
                    if (done(await read()))
                    {
                        return (true, reads, clock.Elapsed, null);
                    }
                }
                catch (LiveServiceException ex)
                {
                    return (false, reads, clock.Elapsed, ex.Message);
                }

                var wait = total - clock.Elapsed;
                if (wait <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(wait < (pause ?? PollPause) ? wait : pause ?? PollPause, TestContext.Current.CancellationToken);
            }

            return (false, reads, clock.Elapsed, null);
        }
        finally
        {
            api.Limit = limit;
        }
    }

    /// <summary><c>git --version</c>'s line, of the git the app finds (7a 14.5's item).</summary>
    public static async Task<string> GitVersionAsync()
    {
        var git = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The live command needs git."));
        var result = await git.RunAsync(new GitCommand(["--version"]) { WorkingDirectory = AppContext.BaseDirectory }, TestContext.Current.CancellationToken);
        return result.Succeeded ? result.StandardOutput.Trim() : throw new InvalidOperationException($"git --version failed ({result.ExitCode.ToString(CultureInfo.InvariantCulture)}).");
    }

    /// <summary>git's <c>User-Agent</c> for <paramref name="version"/>, <c>git version 2.56.0.windows.4</c>'s <c>git/2.56.0.windows.4</c>.</summary>
    public static string AgentOf(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        const string Prefix = "git version ";
        return "git/" + (version.StartsWith(Prefix, StringComparison.Ordinal) ? version[Prefix.Length..] : version).Trim();
    }

    /// <summary>
    /// Each method's call limits summed as 7a's are (each app step 60 s, the clone 120 s, each own call
    /// at its limit, a poll at its budget; 7a design 14.4), with the leftovers' calls at full limits:
    /// the watchdog's 600 s must cover each (15.5). The counts are the methods' own; the fake's start,
    /// a few git calls, and local waits are outside, as in 7a's sums.
    /// </summary>
    public static IReadOnlyDictionary<string, TimeSpan> Sums { get; } = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
    {
        // The identity, the sandbox, the active list, the refs read; five abandons and one refs POST.
        [nameof(AzureDevOpsLiveTests) + "." + nameof(AzureDevOpsLiveTests.Check_1_the_sandbox_and_its_leftovers)] = Own(4 + 6),
        // 16 probes, the first of them the identity read, each beside the fake.
        [nameof(AzureDevOpsLiveTests) + "." + nameof(AzureDevOpsLiveTests.Check_1b_probes)] = Own(16) + Fake(16),
        // The identity, the sandbox; sign-in, list, load, push, create; the clone.
        [nameof(AzureDevOpsLiveTests) + "." + nameof(AzureDevOpsLiveTests.Check_2_the_providers_and_the_session)] = Own(2) + App(5) + LiveApp.CloneLimit,
        // The identity, the read, the duplicate, the missing branch, the abandon, the empty title, the
        // tip's read, the refs POST, nothing ahead; two polls; the clean-up's active list, two abandons,
        // refs read and refs POST; the fake's draft, the eight probes beside and its two merge-ref reads.
        [nameof(AzureDevOpsLiveTests) + "." + nameof(AzureDevOpsLiveTests.Check_2b_probes)] = Own(9) + (PollBudget * 2) + Own(5) + Fake(1 + 6 + 2),
        // The identity; sign-in, list, load; the clone.
        [nameof(AzureDevOpsLiveTests) + "." + nameof(AzureDevOpsLiveTests.Check_4_a_clone_through_the_organization_host)] = Own(1) + App(3) + LiveApp.CloneLimit,
        // The identity, the sandbox, the opened list, the branches' list; five leftovers' close, delete and branch delete.
        [nameof(GitLabLiveTests) + "." + nameof(GitLabLiveTests.Check_1_the_sandbox_and_its_leftovers)] = Own(4 + 15),
        // 6 probes, the first of them the identity read, beside the fake's 7 (two for the 304: its own ETag first).
        [nameof(GitLabLiveTests) + "." + nameof(GitLabLiveTests.Check_1b_probes)] = Own(6) + Fake(7),
        // The identity, the sandbox; sign-in, list, load, the two pushes, create; the clone.
        [nameof(GitLabLiveTests) + "." + nameof(GitLabLiveTests.Check_2_the_providers_and_the_session)] = Own(2) + App(6) + LiveApp.CloneLimit,
        // The identity, the read, the duplicate, the missing branch, nothing ahead; the clean-up's
        // opened list, two requests' close and delete, two branches' delete; the fake's draft and four beside.
        [nameof(GitLabLiveTests) + "." + nameof(GitLabLiveTests.Check_2b_probes)] = Own(5) + Own(1 + 4 + 2) + Fake(1 + 4),
    };

    private static TimeSpan Own(int calls) => LiveServiceApi.CallLimit * calls;

    private static TimeSpan Fake(int calls) => LiveServiceApi.FakeCallLimit * calls;

    private static TimeSpan App(int steps) => LiveApp.NetworkStep * steps;

    public static string Stamp() => DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss'Z'", CultureInfo.InvariantCulture);

    public static IReadOnlyCollection<TTestCase> Order<TTestCase>(IReadOnlyCollection<TTestCase> testCases, IReadOnlyList<string> methods)
        where TTestCase : notnull, ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        int Rank(string? method) => method is not null && methods.Contains(method) ? methods.ToList().IndexOf(method) : int.MaxValue;
        return [.. testCases.OrderBy(test => Rank(test.TestMethodName)).ThenBy(test => test.TestMethodName, StringComparer.Ordinal)];
    }

    /// <summary>The six variables are removed from every test process and gone from this one (15.2).</summary>
    public static void AssertVariablesGone()
    {
        foreach (var variable in LiveSettings.NewVariables.Append(LiveSettings.TokenVariable).Append(LiveSettings.RepositoryVariable))
        {
            Assert.Contains(new KeyValuePair<string, string?>(variable, null), GitIsolation.MachineIndependence);
            Assert.True(Environment.GetEnvironmentVariable(variable) is null, $"{variable} is still in the process's environment.");
        }
    }

    /// <summary>The fake's answer beside the service's, or why there is none: a finding only.</summary>
    public static async Task<string> FakeBesideAsync(LiveServiceAnswer service, Func<Task<LiveServiceAnswer>>? fake, IReadOnlyList<string> serviceNames, IReadOnlyList<string> fakeNames)
    {
        if (fake is null)
        {
            return "not asked (it did not start)";
        }

        try
        {
            return LiveServiceAnswer.Beside(service, await fake(), serviceNames, fakeNames);
        }
        catch (Exception ex) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            return $"no answer ({ex.GetType().Name}: {ex.Message})";
        }
    }

    /// <summary>
    /// A test's end: the requests per host, the reach's problems, a request before the sign-in, then
    /// the problems noted; the failure first, then a test that found problems fails, screened.
    /// </summary>
    public static void Finish(LiveRun run, LiveRequestLog.Store requests, LiveReach reach, List<string> problems, ExceptionDispatchInfo? failure, LiveRequestLog.Store? own = null, DateTimeOffset? signInStarted = null)
    {
        var all = requests.Snapshot();
        foreach (var line in LiveRequestLog.PerHostOf(all))
        {
            run.Note(own is null ? line : "The app's " + line[4..]);
        }

        problems.AddRange(LiveRequestLog.ReachProblems(all, reach));
        if (own is not null)
        {
            var mine = own.Snapshot();
            foreach (var line in LiveRequestLog.PerHostOf(mine))
            {
                run.Note("The test's own " + line[4..]);
            }

            problems.AddRange(LiveRequestLog.ReachProblems(mine, reach));
        }

        if (signInStarted is { } started && all.FirstOrDefault(request => request.Time < started) is { } early)
        {
            problems.Add($"A request left before the sign-in: {early.Describe()}.");
        }

        foreach (var problem in problems)
        {
            run.Note("Problem: " + problem);
        }

        failure?.Throw();
        run.Check(problems.Count == 0, () => string.Join("\n", problems));
    }
}
