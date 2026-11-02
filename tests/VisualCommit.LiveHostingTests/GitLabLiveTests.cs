using System.Diagnostics;
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

/// <summary>Runs <see cref="GitLabLiveTests"/>' methods in 15.5's order (stage 1; stage 2's Check_3 takes its place after Check_2b_probes).</summary>
public sealed class GitLabLiveOrder : ITestCaseOrderer
{
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(GitLabLiveTests.Check_1_the_sandbox_and_its_leftovers),
        nameof(GitLabLiveTests.Check_1b_probes),
        nameof(GitLabLiveTests.Check_2_the_providers_and_the_session),
        nameof(GitLabLiveTests.Check_2b_probes),
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
            await IdentityAsync(api);

            step = "1, the sandbox";
            await ReadSandboxAsync(api);

            step = "2, the leftovers";
            await RemoveLeftoversAsync(api, problems, Cancelled);
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
    /// beside the fake's answer; every request Bearer. Its first call, <c>GET user</c>, is the run's
    /// identity read when Check_1 did not make it.
    /// </summary>
    [Fact]
    public async Task Check_1b_probes()
    {
        var sandbox = Sandbox;
        var store = new LiveRequestLog.Store();
        var reach = LiveReach.GitLab();
        var step = "1b, the probes";
        var problems = new List<string>();
        try
        {
            using var api = new GitLabLiveApi(sandbox, reach, store, run.Screener!, run.Withheld);
            run.Note("Check_1b. GitLab's probes.");
            await using var fake = await StartFakeAsync(sandbox, null, null);
            var user = await ProbeAsync(api, fake, "GET user", a => a.UserAsync(LiveCredential.Token, Cancelled));
            if (user is not { Status: 200 })
            {
                problems.Add($"GET user answered {user?.ToString() ?? "nothing"}, not 200 (15.7's premise).");
            }
            else
            {
                run.IdentityRead = true;
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
                var again = await ProbeAsync(api, fake, "GET user with If-None-Match, each side its own ETag", a => UserWithOwnETagAsync(a, api, etag, Cancelled));
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
    /// requests, pushes D325's commit and creates a draft; it also pushes the branch with nothing ahead
    /// (at the default branch's tip, through the app's helper: D364's token has Code: Push and no
    /// Branch: Create) for Check_2b's probe. Its steps 8 and 9, the probes of the create and the
    /// clean-up, are Check_2b's (15.5's sums): Check_2 hands its branches to the run before the pushes
    /// and its merge request once created, and cleans up nothing itself.
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
        var step = "1, the identity";
        var problems = new List<string>();
        ExceptionDispatchInfo? failure = null;
        try
        {
            await IdentityAsync(api);
            run.Note($"Check_2. The branch {branch}.");
            var project = await ReadSandboxAsync(api);
            var made = new LiveMade(branch, branch + LiveSteps.EvenSuffix, project.DefaultBranch, title);
            run.Made = made;
            step = "1, the start";
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
            var head = await app.PushAsync(opened, branch, stamp, title);
            step = "6, the push of the branch with nothing ahead";
            await app.PushNewBranchAsync(made.Even, "origin/" + project.DefaultBranch);

            step = "7, the create";
            var request = await app.CreateAsync(opened, title, branch, project.DefaultBranch, appRequests, "/merge_requests", problems);
            run.Made = made with { Number = request.Number, Head = head };
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
    /// Check_2's steps 8 and 9 (15.5, 15.7), in a method of their own as Azure DevOps' are: the
    /// create's probes (<c>draft</c>, <c>references</c> and the head; a duplicate, a missing branch,
    /// nothing ahead from the branch Check_2 pushed at the default branch's tip), each beside the
    /// fake's own draft on its namesake of the run's branch; then, in <c>finally</c>, the clean-up of
    /// what Check_2 handed over, whether Check_2 passed or not. A stopped run's leftovers are Check_1's.
    /// </summary>
    [Fact]
    public async Task Check_2b_probes()
    {
        var sandbox = Sandbox;
        var store = new LiveRequestLog.Store();
        var reach = LiveReach.GitLab();
        using var api = new GitLabLiveApi(sandbox, reach, store, run.Screener!, run.Withheld);
        var made = run.Made;
        run.Made = null;
        var step = "2b, the identity";
        var problems = new List<string>();
        ExceptionDispatchInfo? failure = null;
        try
        {
            await IdentityAsync(api);
            run.Check(made is not null, () => "Check_2 handed over nothing: it did not run in this process, so there is nothing to probe or clean up.");
            step = "8, the probes";
            if (made!.Number is not { } number)
            {
                run.Note("Check_2b. Check_2 created no merge request, so there is nothing to probe; its branches are cleaned up.");
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
    /// The identity read (<c>GET user</c>), each method's first call and once per run (15.3): 200, the
    /// namespace's first segment as its login, its member names noted. Nothing when the run made it.
    /// </summary>
    private async Task IdentityAsync(GitLabLiveApi api)
    {
        if (run.IdentityRead)
        {
            return;
        }

        var user = await api.UserAsync(LiveCredential.Token, Cancelled);
        run.Check(user.Status == 200, () => api.Describe(user, "GET user") + ": 401 means the token is wrong or expired.");
        run.IdentityRead = true;
        run.Note($"GET user: {user}; its members: {(user.Json is { ValueKind: JsonValueKind.Object } json ? string.Join(", ", json.EnumerateObject().Select(member => member.Name)) : user.Shape)}.");
        run.Check(string.Equals(user.Member("username"), api.Sandbox.Login, StringComparison.OrdinalIgnoreCase), () => $"The token's user is not {api.Sandbox.Login}, the namespace's first segment.");
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

    private async Task<LiveGitLabFake?> StartFakeAsync(LiveGitLabSandbox sandbox, string? branch, string? even)
    {
        try
        {
            return await LiveGitLabFake.StartAsync(sandbox.Project, branch, even, Cancelled);
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

    /// <summary>
    /// <c>GET user</c> with <c>If-None-Match</c>: the service with its first answer's ETag, the fake
    /// beside with its own (one read of its own first), so that each side is asked with its own ETag
    /// and the fake's 304 is compared, not its 200 to a made-up one (15.7, 4.4).
    /// </summary>
    internal static async Task<LiveServiceAnswer> UserWithOwnETagAsync(GitLabLiveApi asked, GitLabLiveApi service, string serviceETag, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(asked);
        var etag = asked == service ? serviceETag : (await asked.UserAsync(LiveCredential.Token, ct)).Header("ETag") ?? "\"none\"";
        return await asked.UserAsync(LiveCredential.Token, ct, etag);
    }

    /// <summary>
    /// Check_2b's probes of the create (15.7), each beside the fake's own draft from its namesake of the
    /// run's branch: <c>draft</c>, <c>references</c> and the head; a duplicate (409), a missing branch
    /// (400 naming <c>source_branch</c>); nothing ahead, from <see cref="LiveMade.Even"/>, which Check_2
    /// pushed at the default branch's tip (4.3 (h): a branch with no commit ahead, not the default
    /// branch into itself, which is 4.3 (e)'s refusal).
    /// </summary>
    internal async Task ProbeCreateAsync(GitLabLiveApi api, LiveGitLabFake? fake, LiveMade made, int iid, List<string> problems)
    {
        var fakeIid = await FakeDraftAsync(fake, made);
        var side = fakeIid is null ? null : fake;
        string Target(GitLabLiveApi a) => a == api ? made.Target : LiveAzureFake.DefaultBranch;
        int Iid(GitLabLiveApi a) => a == api ? iid : fakeIid.GetValueOrDefault();

        var read = await ProbeAsync(api, side, $"the read of the created !{Show(iid)}", a => a.MergeRequestAsync(Iid(a), Cancelled));
        var sha = read?.Member("sha");
        run.Note($"The created !{Show(iid)}: draft {read?.Member("draft") ?? "none"}, title starts Draft: {(read?.Member("title")?.StartsWith("Draft: ", StringComparison.Ordinal) == true ? "yes" : "no")}, references {read?.Member("references") ?? "none"}, the head {sha ?? "none"} ({(string.Equals(sha, made.Head, StringComparison.OrdinalIgnoreCase) ? "the pushed commit" : "not the pushed commit")}).");
        if (read?.Member("draft") != "true")
        {
            problems.Add($"The created !{Show(iid)}'s draft is {read?.Member("draft") ?? "absent"}, not true, though its title starts Draft: (15.7's premise).");
        }

        var duplicate = await ProbeAsync(api, side, "the duplicate create (409 expected)", a => a.CreateAsync(made.Branch, Target(a), GitLabProvider.TitleFor(made.Title, true), Cancelled));
        if (duplicate is null)
        {
            problems.Add("The duplicate create had no answer, not 409 (its note says why).");
        }
        else if (!LiveProviderPremises.IsGitLabDuplicate(duplicate))
        {
            problems.Add($"The duplicate create answered {duplicate}, not 409.");
        }

        var missing = await ProbeAsync(api, side, "the create from a missing branch (400 naming source_branch expected)", a => a.CreateAsync(MissingBranch, Target(a), made.Title, Cancelled));
        if (missing is null)
        {
            problems.Add("The create from a missing branch had no answer, not 400 naming source_branch (7.3; its note says why).");
        }
        else if (!LiveProviderPremises.IsGitLabMissingBranch(missing))
        {
            problems.Add($"The create from a missing branch answered {missing}, not 400 naming source_branch (7.3).");
        }

        await ProbeAsync(api, side, $"the create with nothing ahead of {made.Target}, from {made.Even}", a => a.CreateAsync(made.Even, Target(a), made.Title, Cancelled));
    }

    /// <summary>The fake's own draft from its namesake of the run's branch, for the probes beside: its iid, or null (noted).</summary>
    private async Task<int?> FakeDraftAsync(LiveGitLabFake? fake, LiveMade made)
    {
        if (fake is null)
        {
            return null;
        }

        try
        {
            var created = await fake.Api.CreateAsync(made.Branch, LiveAzureFake.DefaultBranch, GitLabProvider.TitleFor(made.Title, true), Cancelled);
            if (created is { Status: 201 or 200, Json: { ValueKind: JsonValueKind.Object } json } && json.TryGetProperty("iid", out var iid) && iid.TryGetInt32(out var number))
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

    /// <summary>15.5 step 9, Check_2b's <c>finally</c>: the run's merge requests (from either branch, never a fork's) closed and deleted, then each branch deleted (a 404: gone, g2 G1.7).</summary>
    internal async Task CleanUpAsync(GitLabLiveApi api, IReadOnlyList<string> branches, List<string> problems)
    {
        var opened = await LiveSteps.AttemptAsync(api, problems, "the clean-up's read of the opened merge requests", () => api.OpenedMergeRequestsAsync(CancellationToken.None));
        foreach (var request in (opened ?? []).Where(request => branches.Contains(request.SourceBranch) && !request.FromFork))
        {
            await CloseAndDeleteAsync(api, request.Iid, problems, CancellationToken.None);
        }

        foreach (var branch in branches)
        {
            await DeleteBranchAsync(api, branch, problems, CancellationToken.None);
        }
    }

    /// <summary>
    /// 15.5 step 2, Check_1's leftovers of a stopped run (D324): the opened merge requests from
    /// <c>visualcommit-live/</c> branches of the sandbox closed and deleted, then each such branch
    /// deleted. Each call goes on its own: one that fails is a problem, and the others are still sent,
    /// the branches' read and deletes after a failed list, close or delete too. Internal for <see cref="LiveSafetyTests"/>.
    /// </summary>
    internal async Task RemoveLeftoversAsync(GitLabLiveApi api, List<string> problems, CancellationToken ct)
    {
        var opened = await LiveSteps.AttemptAsync(api, problems, "the read of the opened merge requests", () => api.OpenedMergeRequestsAsync(ct));
        var leftovers = (opened ?? []).Where(LiveProviderLeftovers.IsLeftover).ToList();
        run.Note(opened is null
            ? $"Opened merge requests: unread (a problem above), so none is closed; the {LiveLeftovers.Prefix} branches are still read and deleted."
            : $"Opened merge requests: {Show(opened.Count)}, {Show(leftovers.Count)} of them from {LiveLeftovers.Prefix} branches of the sandbox.");
        foreach (var request in leftovers)
        {
            await CloseAndDeleteAsync(api, request.Iid, problems, CancellationToken.None);
        }

        var branches = await LiveSteps.AttemptAsync(api, problems, "the read of the leftover branches", () => api.BranchesAsync(LiveLeftovers.Prefix, ct));
        foreach (var branch in branches ?? [])
        {
            await DeleteBranchAsync(api, branch, problems, CancellationToken.None);
        }

        run.Note($"Leftovers of a stopped run: {Show(leftovers.Count)} merge requests, {(branches is null ? "the branches unread" : $"{Show(branches.Count)} branches")}.");
    }

    /// <summary>One merge request closed, then deleted, each call on its own: the delete is sent after a close that failed too.</summary>
    private async Task CloseAndDeleteAsync(GitLabLiveApi api, int iid, List<string> problems, CancellationToken ct)
    {
        var closed = await LiveSteps.AttemptAsync(api, problems, $"closing !{Show(iid)}", () => api.CloseAsync(iid, ct));
        var deleted = await LiveSteps.AttemptAsync(api, problems, $"deleting !{Show(iid)}", () => api.DeleteMergeRequestAsync(iid, ct));
        run.Note($"Merge request !{Show(iid)}: closed {closed?.ToString() ?? "no answer"}, deleted {deleted?.ToString() ?? "no answer"}{(deleted?.Status == 403 ? " (closed, kept, g2 G1.6)" : string.Empty)}.");
        if (closed is not null && closed.Status != 200)
        {
            problems.Add(api.Describe(closed, $"closing !{Show(iid)}"));
        }

        if (deleted is not null && deleted.Status is not (204 or 403))
        {
            problems.Add(api.Describe(deleted, $"deleting !{Show(iid)}"));
        }
    }

    private async Task DeleteBranchAsync(GitLabLiveApi api, string branch, List<string> problems, CancellationToken ct)
    {
        var deleted = await LiveSteps.AttemptAsync(api, problems, $"deleting {branch}", () => api.DeleteBranchAsync(branch, ct));
        if (deleted is null)
        {
            return;
        }

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
    /// <summary>How long a network step of the app may take (7a 14.4).</summary>
    internal static readonly TimeSpan NetworkStep = TimeSpan.FromSeconds(60);

    /// <summary>How long the clone may take, its two waits together (7a 14.4).</summary>
    internal static readonly TimeSpan CloneLimit = TimeSpan.FromSeconds(120);

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

    /// <summary>Step 1: git's version noted (7a 14.5), then the app with the provider on and no request sent.</summary>
    public async Task StartAsync(LiveRequestLog.Store requests)
    {
        _run.Note("Git: " + await LiveSteps.GitVersionAsync() + ".");
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
        var clock = Stopwatch.StartNew();
        App.Click(App.FindByAutomationId("CloneStartButton"));
        await WaitWatchingAsync(() => tab.Repository is not null || (!tab.Welcome.IsCloning && tab.Welcome.HasError), "the clone to open its repository", CloneLimit);
        if (tab.Repository is null)
        {
            throw new XunitException(_run.Screen("The clone failed: " + tab.Welcome.ErrorText));
        }

        var opened = await App.WaitForRepositoryAsync(Left(CloneLimit, clock));
        _run.Note($"The clone took {clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s, its repository opened.");
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

    /// <summary>Step 6: D325's commit on <paramref name="branch"/>, pushed with the branch menu's "Push {branch}…"; the commit's id.</summary>
    public async Task<string> PushAsync(RepositoryViewModel opened, string branch, string stamp, string title)
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
        var head = (await GitOkAsync(clone, identity, "rev-parse", "HEAD")).StandardOutput.Trim();
        await PushBranchAsync(branch);
        return head;
    }

    /// <summary>
    /// A branch made in the clone at <paramref name="startPoint"/> without tracking it (so that the push
    /// dialog never offers the start point's branch), then pushed as <see cref="PushAsync"/> pushes:
    /// GitLab's branch with nothing ahead (D364's token has Code: Push, no Branch: Create).
    /// </summary>
    public async Task PushNewBranchAsync(string branch, string startPoint)
    {
        var clone = _clone ?? throw new InvalidOperationException("No clone.");
        await App.WaitForWatchersQuietAsync();
        await GitOkAsync(clone, Identity(), "branch", "--no-track", branch, startPoint);
        await PushBranchAsync(branch);
    }

    /// <summary>
    /// The push's own record: a foreground network operation that started after <paramref name="mark"/>,
    /// never a background one (the backups' clean-up, auto-fetch) that ends first.
    /// </summary>
    internal static bool IsPushRecord(OperationRecord record, long mark)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Sequence > mark && !record.IsBackground && record.Kind == OperationKind.Network;
    }

    /// <summary>What is left of <paramref name="limit"/> on <paramref name="clock"/>, never below zero: a step's second wait shares its first's limit.</summary>
    internal static TimeSpan Left(TimeSpan limit, Stopwatch clock) => limit - clock.Elapsed is { Ticks: > 0 } left ? left : TimeSpan.Zero;

    /// <summary>Step 7: a draft through the repository's hosting (<c>Hosting.CreatePullRequestAsync</c>); its POST's status and request id noted (200 or 201, r5 9 item 6); <c>ByRef</c> holds it, within the step's one limit.</summary>
    public async Task<PullRequest> CreateAsync(RepositoryViewModel opened, string title, string branch, string baseBranch, LiveRequestLog.Store requests, string createPath, List<string> problems)
    {
        var target = opened.HostingState.Repository ?? throw new XunitException("The repository has no hosted repository.");
        var clock = Stopwatch.StartNew();
        var created = await opened.Hosting.CreatePullRequestAsync(target, new NewPullRequest(title, LiveSteps.Description, branch, null, baseBranch, Draft: true), Cancelled).WaitAsync(NetworkStep, Cancelled);
        if (created.Value is not { } pull)
        {
            throw new XunitException(_run.Screen($"The create failed: {created.Failure?.Kind}: {created.Failure?.Refusal}: {created.Failure?.Message} (request {created.Failure?.RequestId ?? "none"})."));
        }

        var post = requests.Snapshot().LastOrDefault(request => request.Method == "POST" && request.Path.EndsWith(createPath, StringComparison.Ordinal));
        _run.Note($"Created {HostingProviders.DisplayName(Provider)}'s !{pull.Number.ToString(CultureInfo.InvariantCulture)} from {branch} into {baseBranch}, draft {(pull.IsDraft ? "yes" : "no")}; the app's POST answered {post?.Status?.ToString(CultureInfo.InvariantCulture) ?? "nothing recorded"} (request {post?.RequestId ?? "none"}).");
        if (post?.Status is not (200 or 201))
        {
            problems.Add($"The app's create POST answered {post?.Status?.ToString(CultureInfo.InvariantCulture) ?? "nothing recorded"}, not 200 or 201 (15.7's premise).");
        }

        await App.WaitForAsync(() => opened.HostingState.ByRef.TryGetValue(LiveLeftovers.HeadsPrefix + branch, out var listed) && listed.Number == pull.Number, $"!{pull.Number} in ByRef", Left(NetworkStep, clock));
        return pull;
    }

    /// <summary>"Push {branch}…" from the branch's row under Local branches, the push dialog's Confirm, and the push's own record, which must have succeeded.</summary>
    private async Task PushBranchAsync(string branch)
    {
        string[] row = [LeftPanelViewModel.LocalBranchesTitle, .. branch.Split('/')];
        await App.WaitForAsync(() => HasRow(row[..2]), "the branch's folder under Local branches");
        await App.WaitForWatchersQuietAsync();
        if (row.Length > 2 && App.RefRowModel(row[..2]) is { IsOpen: false })
        {
            App.Click(await App.RefRowAsync(row[..2]));
        }

        await App.WaitForAsync(() => HasRow(row), "the branch's row");
        App.RightClick(await App.RefRowAsync(row));
        await App.WaitForAsync(() => App.Session.Shell.Menu.IsOpen, "the branch's menu");
        var mark = App.Session.Operations.Snapshot().Select(record => record.Sequence).DefaultIfEmpty(0).Max();
        App.ClickMenuItem(OperationTexts.MenuTexts.PushWithDialog(branch));
        await App.WaitForDialogAsync<PushDialogViewModel>();
        App.Click(App.FindByAutomationId("ConfirmButton"));
        OperationRecord? pushed = null;
        await WaitWatchingAsync(() => (pushed = App.Session.Operations.Snapshot().FirstOrDefault(record => IsPushRecord(record, mark))) is not null, "the push to end", NetworkStep);
        _run.Check(pushed!.Outcome == OperationOutcome.Succeeded, () => $"The push ended {pushed.Outcome}: {pushed.Message}");
        _run.Note($"The push of {branch}: {pushed.Title}, {pushed.Outcome}.");
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
