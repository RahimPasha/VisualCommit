using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using CloneTexts = VisualCommit.App.ViewModels.Operations.OperationTexts.CloneTexts;
using CreateTexts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.LiveHostingTests;

/// <summary>Runs <see cref="GitHubLiveTests"/>' three methods in 14.4's order: the sandbox and its leftovers first.</summary>
public sealed class GitHubLiveOrder : ITestCaseOrderer
{
    /// <summary>The methods in their order.</summary>
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(GitHubLiveTests.Check_1_the_sandbox_and_its_leftovers),
        nameof(GitHubLiveTests.Check_2_sign_in_clone_list_push_and_create),
        nameof(GitHubLiveTests.Check_3_the_providers_answers_from_github),
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
/// The live command against real GitHub (docs/phase-7a-design.md 14.4; D249 to D261, D323 to D328):
/// the sandbox read and the leftovers of a stopped run removed, then the whole app headless signing
/// in through the "Add GitHub account" dialog, cloning the private sandbox through its own credential
/// helper, listing its pull requests, pushing a branch and opening a draft pull request through the
/// form, which the test reads back, tries again (r5 14), closes and deletes; then the app's own
/// <c>GitHubProvider</c> against GitHub, for what D342 changed. GitHub accepted the draft in the
/// owner's private sandbox in the run of 2026-10-30; on a plan that refuses drafts there, the form's
/// error line is recorded for the fake and the same form creates a ready pull request, which the
/// later steps then expect (design 18 item 2 has the lead's ruling). Every test asks
/// <see cref="LiveSettings.Require"/> first and fails without the variables. Its notes go to
/// <see cref="LiveRun"/>, searched for the token, and to run.txt; so is a failed step's exception
/// before it reaches the test output (<see cref="LiveRun.FailureOf"/>). The token is sent only by the
/// dialog's typing, the test's own API calls and Check_3's <c>GitHubProvider</c> (behind
/// <see cref="LiveRequestLog"/>), to <c>https://api.github.com/</c> alone (the fake in process has a
/// token of its own), and compared only with <c>string.Equals</c> or <c>string.Contains</c>; it is
/// never an assertion's argument or in a message.
/// </summary>
[TestCaseOrderer(typeof(GitHubLiveOrder))]
public sealed class GitHubLiveTests(LiveRun run) : IClassFixture<LiveRun>
{
    /// <summary>The branches the command pushes, and the only ones it removes (D324).</summary>
    private const string LivePrefix = LiveLeftovers.Prefix;

    private const string HeadsPrefix = LiveLeftovers.HeadsPrefix;

    /// <summary>The avatars' host (6.4): the only other host the app may ask, never with the token.</summary>
    private const string AvatarHost = "avatars.githubusercontent.com";

    /// <summary>The identity of the command's commit (D325): no address of the owner's.</summary>
    private const string IdentityName = "VisualCommit live test";

    private const string IdentityEmail = "live-test@visualcommit.invalid";

    private const string Description = "Opened and closed by VisualCommit's live command (tests/VisualCommit.LiveHostingTests). Nothing to review.";

    /// <summary>A SHA no repository holds, for GitHub's text on an unknown one (2.4); <see cref="LivePremises.UnknownSha"/> gives one that is not all <c>f</c>s.</summary>
    private const string UnknownSha = "ffffffffffffffffffffffffffffffffffffffff";

    /// <summary>An address no commit has and GitHub links to no one, for the avatars' lookup that finds nothing (D339).</summary>
    private const string NoAuthor = "no-such-author@visualcommit.invalid";

    /// <summary>Another such address, for Check_3's lookup of a commit GitHub does not know (D342).</summary>
    private const string NoAuthor2 = "no-such-author-2@visualcommit.invalid";

    /// <summary>A branch the sandbox never has (the leftovers' clean-up removes every branch under <see cref="LivePrefix"/>): the commits page's case of a branch GitHub lacks (A6).</summary>
    private const string MissingBranch = LiveLeftovers.Prefix + "no-such-branch";

    /// <summary>A version GitHub's API does not have, for its text on one (2.4).</summary>
    private const string UnknownVersion = "2000-01-01";

    /// <summary>A network step's limit (14.4).</summary>
    private static readonly TimeSpan NetworkStep = TimeSpan.FromSeconds(60);

    /// <summary>The clone's limit (14.4).</summary>
    private static readonly TimeSpan CloneLimit = TimeSpan.FromSeconds(120);

    private static readonly Regex Created = new(@"^Created (?:draft )?pull request #(?<number>\d+)$", RegexOptions.CultureInvariant);

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>
    /// 14.4's Check_1: the variables gone from the process; the sandbox read (200, permissions.push,
    /// private, D326, not a fork); the leftovers of a stopped run removed: open pull requests from the
    /// sandbox's own <c>visualcommit-live/</c> branches closed, those branches deleted, nothing else
    /// touched (D324, <see cref="LiveLeftovers"/>); then GitHub's texts that the fake writes from
    /// memory probed (2.4, 15 point 1) beside the fake's answers, as findings, except the answers the
    /// app relies on (<see cref="LivePremises"/>, D342), which fail the test once every note is kept.
    /// </summary>
    [Fact]
    public async Task Check_1_the_sandbox_and_its_leftovers()
    {
        var sandbox = LiveSettings.Require();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss'Z'", CultureInfo.InvariantCulture);
        var step = "1, the variables";
        try
        {
            // Step 1: both variables are removed from every test process and every app the harness
            // starts, and this process holds neither any more.
            Assert.Contains(new KeyValuePair<string, string?>(LiveSettings.TokenVariable, null), GitIsolation.MachineIndependence);
            Assert.Contains(new KeyValuePair<string, string?>(LiveSettings.RepositoryVariable, null), GitIsolation.MachineIndependence);
            Assert.True(Environment.GetEnvironmentVariable(LiveSettings.TokenVariable) is null, $"{LiveSettings.TokenVariable} is still in the process's environment.");
            Assert.True(Environment.GetEnvironmentVariable(LiveSettings.RepositoryVariable) is null, $"{LiveSettings.RepositoryVariable} is still in the process's environment.");
            run.Note($"Check_1. The sandbox {sandbox.FullName}; the token is {LiveSettings.KindOf(sandbox.Token)}.");

            // Step 2: the sandbox.
            step = "2, the sandbox";
            using var api = new GitHubLiveApi(sandbox, run.Withheld);
            var repository = await ReadSandboxAsync(api, sandbox);

            // Step 3: the leftovers of a stopped run (D324), before anything that is a finding only.
            step = "3, the leftovers";
            var open = await api.OpenPullRequestsAsync(Cancelled);
            var leftovers = open.Where(pull => LiveLeftovers.IsLeftover(pull, sandbox.FullName)).ToList();
            run.Note($"Open pull requests: {Show(open.Count)}, {Show(leftovers.Count)} of them from {LivePrefix} branches of the sandbox.");
            var refused = new List<string>();
            foreach (var pull in leftovers)
            {
                var closed = await api.ClosePullRequestAsync(pull.Number, Cancelled);
                run.Note($"Leftover pull request #{Show(pull.Number)} ({pull.HeadRef}) closed: {closed}.");
                if (closed.Status != 200)
                {
                    refused.Add(api.Describe(closed, $"closing #{Show(pull.Number)}"));
                }
            }

            var found = await api.BranchesUnderAsync(LivePrefix, Cancelled);
            var branches = found.Under;
            if (found.Others.Count > 0)
            {
                run.Note($"GitHub's matching-refs for heads/{LivePrefix} also returned {Show(found.Others.Count)} refs not under {HeadsPrefix}{LivePrefix}, not touched: {string.Join(", ", found.Others)}.");
            }

            foreach (var branch in branches)
            {
                var deleted = await api.DeleteBranchAsync(branch, Cancelled);
                run.Note($"Leftover branch {branch} deleted: {deleted}.");
                if (deleted.Status != 204)
                {
                    refused.Add(api.Describe(deleted, $"deleting {branch}"));
                }
            }

            run.Note(leftovers.Count == 0 && branches.Count == 0
                ? "Leftovers of a stopped run: none."
                : $"Leftovers of a stopped run: {Show(leftovers.Count)} pull requests closed, {Show(branches.Count)} branches deleted, {Show(refused.Count)} refused.");
            Assert.True(refused.Count == 0, "The leftovers' clean-up was refused: " + string.Join("; ", refused));

            // GitHub's texts that the fake writes from memory (2.4; 15 point 1), beside the fake's: a
            // finding, which fails nothing, unless the app relies on the answer (D342); then the
            // answer that breaks a premise, or none, fails the test after every note is kept.
            step = "3, GitHub's texts";
            var broken = await ProbeTextsAsync(api, sandbox.Owner, sandbox.Name, repository.DefaultBranch, stamp);
            foreach (var premise in broken)
            {
                run.Note("Problem: " + premise);
            }

            Assert.True(broken.Count == 0, "GitHub does not answer as the app relies on: " + string.Join("; ", broken));
        }
        catch (Exception ex)
        {
            run.Note($"Check_1 failed in step {step}: {ex.GetType().Name}: {ex.Message}");
            run.FailureOf(ex, step).Throw();
        }
    }

    /// <summary>
    /// 14.4's Check_2: the app signs in, clones the sandbox from the repository browser through its
    /// own helper (no credentials dialog, no helper of the user's or the system's, the broker's line
    /// that git's get was answered from the account), lists its pull
    /// requests and waits for its avatars, pushes a new branch and creates a draft pull request through
    /// the form (a ready one when GitHub refuses the draft, the refusal recorded); the test
    /// reads it back, tries the same create again (r5 14), closes it and deletes the branch, then
    /// proves that the token reached no file and no git call and that every request with it went to
    /// <c>https://api.github.com/</c> (D327).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_2_sign_in_clone_list_push_and_create()
    {
        var sandbox = LiveSettings.Require();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss'Z'", CultureInfo.InvariantCulture);
        var branch = LivePrefix + stamp;
        var title = "VisualCommit live test " + stamp;
        using var data = new TempDirectory("live-data");
        using var clones = new TempDirectory("live-clone");
        using var home = new TempDirectory("live-home");
        using var api = new GitHubLiveApi(sandbox, run.Withheld);
        var git = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The live command needs git."));
        var session = GitIsolation.SessionGitEnvironment(data.Path);

        var requests = new LiveRequestLog.Store();
        var hosting = new HostingOptions(GitHubEndpoints.GitHubCom, () => new LiveRequestLog(HostingOptions.DefaultHandler(GitHubEndpoints.GitHubCom)(), requests));
        var step = "0, the sandbox";
        ShellDriver? app = null;
        DateTimeOffset? signInStarted = null;
        int? number = null;
        var createClicked = false;
        var draft = true;
        var duplicates = new List<int>();
        ExceptionDispatchInfo? failure = null;
        var problems = new List<string>();
        try
        {
            run.Note($"Check_2. The branch {branch}.");
            var repository = await ReadSandboxAsync(api, sandbox);
            run.Note("Git: " + (await GitOkAsync(git, data.Path, session, "--version")).StandardOutput.Trim() + ".");

            // Step 1: the start. The session knows github.com and has asked it nothing.
            step = "1, the start";
            app = ShellDriver.StartWithTabs(data.Path, [null], hosting: hosting);
            await app.WaitUntilReadyAsync();
            var hostingSession = app.Session.Services.Hosting;
            Assert.False(hostingSession.IsOff, "GitHub is off in the app.");
            await app.WaitForAsync(() => app.LogLines().Any(line => line.EndsWith(" [INF] GitHub: github.com.", StringComparison.Ordinal)), "the log's \"GitHub: github.com.\"");
            Assert.True(requests.Count == 0, "The app sent a request before the sign-in: " + string.Join("; ", requests.Snapshot().Select(request => request.Describe())));

            // Step 2: the sign-in through Settings, Accounts, as a user signs in.
            step = "2, the sign-in";
            signInStarted = DateTimeOffset.UtcNow;
            var screen = await app.OpenAccountsAsync();
            var dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(sandbox.Token);
            await app.ConfirmSignInAsync(NetworkStep);
            if (dialog.ErrorText is { } refusedText)
            {
                throw new XunitException(run.Screen("The sign-in failed: " + refusedText));
            }

            Assert.False(dialog.IsSaveFailed, "The sign-in kept the token for the session only: the fake keychain refused it.");
            await app.WaitForAccountAsync(account => account is { State: AccountState.SignedIn }, NetworkStep);
            var login = hostingSession.Account!.Account.Login;
            if (!string.Equals(login, sandbox.Owner, StringComparison.OrdinalIgnoreCase))
            {
                throw new XunitException($"The token belongs to {login}, not to {sandbox.Owner}, the sandbox's owner.");
            }

            var signIn = requests.Snapshot();
            run.Note($"The sign-in: {string.Join("; ", signIn.Select(request => request.Describe()))}; GET /user for a token with no account permission answered {(signIn.Count > 0 && signIn[0].Status is { } status ? Show(status) : "nothing")}"
                + (signIn.Count > 0 ? $"; its answer's {signIn[0].Headers()}." : "."));
            if (signIn is not [{ Method: "GET", Scheme: "https", Host: LiveRequestLog.ApiHost, Path: "/user", Status: 200, AuthorizationSent: true }])
            {
                throw new XunitException("The sign-in did not send exactly one GET https://api.github.com/user, answered 200 with Authorization: " + string.Join("; ", signIn.Select(request => request.Describe())));
            }

            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");

            // Step 3: the repository browser.
            step = "3, the browser";
            var tab = app.Session.Shell.ActiveTab;
            app.Click(app.FindByAutomationId("CloneRepoButton"));
            await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
            await app.WaitForAsync(() => app.Find<Button>("CloneFromGitHubButton").IsEffectivelyVisible, "From GitHub… to show");
            app.Click(app.Find<Button>("CloneFromGitHubButton"));
            await app.WaitForAsync(() => Browser(app) is not null, "the repository browser");
            var browser = Browser(app)!;
            await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list", NetworkStep);
            if (browser.ErrorText is { } listError)
            {
                throw new XunitException(run.Screen("The browser's list failed: " + listError));
            }

            var rows = browser.AllRows;
            if (rows.Count == 0)
            {
                throw new XunitException(run.Screen("The browser lists no repository: " + (browser.StatusText ?? "(no status line)")));
            }

            var outside = rows.Where(row => !string.Equals(row.FullName, sandbox.FullName, StringComparison.OrdinalIgnoreCase)).ToList();
            var outsidePrivate = outside.Count(row => row.Detail is { } detail && detail.Split(CloneTexts.DetailSeparator)[0] == CloneTexts.Private);
            run.Note($"The browser (GET /user/repos): {Show(rows.Count)} rows, more exist: {(browser.MoreText is null ? "no" : "yes")}; "
                + $"{Show(outside.Count)} outside the sandbox, {Show(outside.Count - outsidePrivate)} public and {Show(outsidePrivate)} private.");
            var row = rows.FirstOrDefault(candidate => string.Equals(candidate.FullName, sandbox.FullName, StringComparison.OrdinalIgnoreCase))
                ?? throw new XunitException($"The browser does not list {sandbox.FullName}.");
            app.TypeInto("CloneFromGitHubFilter", sandbox.Name);
            ListBoxItem? item = null;
            await app.WaitForAsync(() => (item = BrowserItem(app, row.FullName)) is not null, $"{row.FullName}'s row in the filtered list");
            app.Click(item!);
            await app.WaitForAsync(() => Equals(browser.Selected, row), $"{row.FullName} selected");
            app.Click(app.FindByAutomationId("ConfirmButton"));
            await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
            var url = app.Find<TextBox>("CloneUrlBox").Text ?? string.Empty;
            Assert.True(
                string.Equals(url, $"https://github.com/{sandbox.FullName}.git", StringComparison.OrdinalIgnoreCase),
                $"The URL box holds {url}, not https://github.com/{sandbox.FullName}.git.");

            // Step 4: the clone, through the app's helper (D255) and no dialog.
            step = "4, the clone";
            app.ReplaceText("CloneParentBox", clones.Path);
            var clone = Path.Combine(clones.Path, app.Find<TextBox>("CloneNameBox").Text ?? sandbox.Name);
            var cloneMark = app.LogMark();
            var cloneStarted = DateTimeOffset.UtcNow;
            var cloneClock = Stopwatch.StartNew();
            app.Click(app.FindByAutomationId("CloneStartButton"));
            await WaitWatchingAsync(app, () => tab.Repository is not null || (!tab.Welcome.IsCloning && tab.Welcome.HasError), "the clone to open its repository", CloneLimit);
            if (tab.Repository is null)
            {
                throw new XunitException(run.Screen("The clone failed: " + tab.Welcome.ErrorText));
            }

            var opened = await app.WaitForRepositoryAsync(CloneLimit);
            run.Note($"The clone: {cloneClock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s to the repository opened in the tab.");

            // The account's token reached git through the app's helper (D255): a credential of
            // another kind (a netrc entry, D340) would clone the private sandbox as well.
            var answered = $"{Path.GetFileName(clone)}: Clone: git's credential get for github.com answered from the account.";
            Assert.True(
                app.LogLinesSince(cloneMark).Any(line => line.EndsWith(answered, StringComparison.Ordinal)),
                $"No line of the log since the click on Clone ends with \"{answered}\".");
            Assert.Equal(url, (await GitOkAsync(git, clone, session, "config", "--get", "remote.origin.url")).StandardOutput.Trim());
            var credentialKeys = await GitAsync(git, clone, session, "config", "--get-regexp", "^credential");
            Assert.True(credentialKeys.ExitCode == 1, "The clone's configuration holds credential keys: " + string.Join(", ", Keys(credentialKeys.StandardOutput)));
            var helpers = await GitAsync(git, clone, session, "config", "--show-origin", "--get-all", "credential.helper");
            Assert.True(helpers.ExitCode == 1, run.Screen("Git finds a credential helper, so Git Credential Manager could start: " + helpers.StandardOutput.Trim()));

            // Step 5: the sandbox's pull requests.
            step = "5, the pull requests";
            await app.WaitForAsync(() => opened.HostingState.PullRequests is PullRequestsState.Loaded or PullRequestsState.Failed, "the sandbox's pull requests", NetworkStep);
            if (opened.HostingState is { PullRequests: PullRequestsState.Failed, Failure: { } listFailure })
            {
                throw new XunitException(run.Screen($"Listing the sandbox's pull requests failed: {listFailure.Kind}: {listFailure.Message} (request {listFailure.RequestId ?? "none"})."));
            }

            await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(opened.Name), mark: cloneMark, timeout: NetworkStep);
            var listed = opened.HostingState.List!.PullRequests.Count;
            var shown = tab.LeftPanel.Rows.OfType<PullRequestRow>().Count();
            var none = tab.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(message => message is { Kind: PullRequestMessageKind.None } && message.Text == OperationTexts.NoOpenPullRequests);
            Assert.True(listed == 0 ? none && shown == 0 : shown == listed, $"The section shows {Show(shown)} pull request rows{(none ? " and \"No open pull requests\"" : string.Empty)} for {Show(listed)} listed.");
            run.Note($"Open pull requests the app listed before the create: {Show(listed)}.");

            // The avatars (11.3; 15 point 1): when GitHub links an account to the sandbox's latest
            // commit, the app's avatar run ends in the AvatarsShown line (only the shown tab, here the
            // only one, writes it) or in an answer after which no picture comes; the wait is for
            // either, so that run.txt's avatar findings do not hang on the network's speed.
            step = "5, the avatars";
            var linked = await api.LatestCommitAuthorAsync(repository.DefaultBranch, Cancelled);
            if (linked is null)
            {
                run.Note("GitHub links no account to the sandbox's latest commit: no avatar to wait for.");
            }
            else
            {
                string? avatars = null;
                await app.WaitForAsync(
                    () => (avatars = AvatarsEnded(app, opened.Name, cloneMark, requests, cloneStarted, sandbox)) is not null,
                    "the avatars' run to end",
                    NetworkStep);
                run.Note($"GitHub links {linked} to the sandbox's latest commit; the app's avatar run ended: {avatars}.");
            }

            // Step 6: the branch, its commit (D325), the push and the create through the form.
            step = "6, the branch";
            await app.WaitForWatchersQuietAsync();
            var identity = LiveIdentity(home.Path);
            await GitOkAsync(git, clone, identity, "switch", "-c", branch);
            var file = Path.Combine(clone, "visualcommit-live", stamp + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, $"VisualCommit live test {stamp}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await GitOkAsync(git, clone, identity, "add", "--", $"visualcommit-live/{stamp}.txt");
            await GitOkAsync(git, clone, identity, "commit", "-m", title);
            var head = (await GitOkAsync(git, clone, identity, "rev-parse", "HEAD")).StandardOutput.Trim();

            await app.WaitForAsync(() => HasRow(app, LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live"), "the branch's folder under Local branches");
            await app.WaitForWatchersQuietAsync();
            if (app.RefRowModel(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live") is { IsOpen: false })
            {
                app.Click(await app.RefRowAsync(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live"));
            }

            await app.WaitForAsync(() => HasRow(app, LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live", stamp), "the branch's row");
            app.RightClick(await app.RefRowAsync(LeftPanelViewModel.LocalBranchesTitle, "visualcommit-live", stamp));
            await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the branch's menu");
            app.ClickMenuItem(OperationTexts.MenuTexts.CreatePullRequest);

            step = "6, the form";
            var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
            await app.WaitForAsync(() => form.Base is not null || (!form.IsReadingBase && form.Error is not null), "the form's base", NetworkStep);
            if (form.Base is not { } baseEnd)
            {
                throw new XunitException(run.Screen("The form found no base: " + form.Error));
            }

            Assert.Equal((branch, repository.DefaultBranch), (form.Head.Text, baseEnd.Branch));

            // In the sandbox and from it (0.2: for a fork the app creates in the parent, where the
            // number would name another repository's pull request than the one the clean-up closes).
            Assert.True(
                string.Equals(baseEnd.Repository, sandbox.FullName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(form.Head.Repository, sandbox.FullName, StringComparison.OrdinalIgnoreCase),
                $"The form would create in {baseEnd.Repository} from {form.Head.Repository}, not in and from {sandbox.FullName}.");
            app.ReplaceText("PullRequestTitle", title);
            app.TypeInto("PullRequestDescription", Description);
            app.Click(app.FindByAutomationId("PullRequestDraft"));
            Assert.Equal((title, Description, true), (form.PullRequestTitle, form.Description, form.IsDraft));

            step = "6, the create";
            var createMark = app.LogMark();
            var operationMark = app.Session.Operations.Snapshot().Select(record => record.Sequence).DefaultIfEmpty(0).Max();
            var operationTitle = CreateTexts.Operation(branch).Title;
            createClicked = true;
            await CreateAsync(app, form);
            if (!form.IsClosed && form.Error is { } refusal && refusal.Contains("draft", StringComparison.OrdinalIgnoreCase))
            {
                // GitHub accepted the draft in the owner's private sandbox (D326) in the run of
                // 2026-10-30; this is for a plan that refuses drafts in a private repository, where
                // the POST would be refused with a 422 whose words the form shows. The push before it
                // went through, and a refused create starts no refresh of the list (only a succeeded
                // network operation does). GitHub's words are recorded for the fake (2.4); the same
                // form then creates a ready pull request, which the later steps expect.
                step = "6, the create without Draft";
                run.Note($"GitHub refused a draft pull request in the sandbox; the form's error line: {refusal}");
                var refusedCreate = await OperationAfterAsync(app, operationMark, operationTitle);
                if (refusedCreate.Outcome == OperationOutcome.Succeeded)
                {
                    throw new XunitException(run.Screen($"The refused create's operation ended {refusedCreate.Outcome}: {refusedCreate.Message}"));
                }

                operationMark = refusedCreate.Sequence;
                draft = false;
                app.Click(app.FindByAutomationId("PullRequestDraft"));
                await app.WaitForAsync(() => !form.IsDraft && form.Error is null, "Draft unticked and the error line cleared");
                createMark = app.LogMark();
                await CreateAsync(app, form);
            }

            if (!form.IsClosed)
            {
                // The sandbox's read checks the user's role (permissions.push); the token's own grant
                // shows first here, at the push or the create.
                throw new XunitException(run.Screen($"The form says: {form.Error} (when GitHub refused the push or the create, the token may lack Contents or Pull requests \"Read and write\": docs/phase-7a-design.md 14.6)."));
            }

            var created = await OperationAfterAsync(app, operationMark, operationTitle);
            var match = Created.Match(created.Message);
            if (created.Outcome != OperationOutcome.Succeeded || !match.Success)
            {
                throw new XunitException(run.Screen($"The create ended {created.Outcome}: {created.Message}"));
            }

            number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            Assert.Equal(CreateTexts.Created(number.Value, draft), created.Message);
            run.Note($"{created.Message} with the head {head}{(draft ? "; GitHub accepts drafts in the sandbox" : string.Empty)}.");
            await app.WaitForAsync(
                () => opened.HostingState.ByRef.TryGetValue(HeadsPrefix + branch, out var pull) && pull.Number == number,
                $"#{Show(number.Value)} on {HeadsPrefix}{branch}",
                NetworkStep);
            Assert.True(opened.HostingState.ByRef[HeadsPrefix + branch].IsDraft == draft, $"#{Show(number.Value)} is {(draft ? "not a draft" : "a draft")} in the app.");
            await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(opened.Name), occurrence: 2, mark: createMark, timeout: NetworkStep);
            await app.WaitForWatchersQuietAsync();
            Assert.Equal("origin", (await GitOkAsync(git, clone, session, "config", "--get", $"branch.{branch}.remote")).StandardOutput.Trim());
            Assert.Equal(HeadsPrefix + branch, (await GitOkAsync(git, clone, session, "config", "--get", $"branch.{branch}.merge")).StandardOutput.Trim());

            // Step 7: GitHub's own record of it.
            step = "7, the read back";
            var read = await api.ReadPullRequestAsync(number.Value, Cancelled);
            if (read is not { Status: 200, Json: { } pullRequest })
            {
                throw new XunitException(api.Describe(read, $"pull request #{Show(number.Value)}") + ".");
            }

            var differences = new List<string>();
            void Expect(string field, string? actual, string expected, StringComparison comparison = StringComparison.Ordinal)
            {
                if (!string.Equals(actual, expected, comparison))
                {
                    differences.Add($"{field} is {actual ?? "missing"}, not {expected}");
                }
            }

            Expect("draft", pullRequest.GetProperty("draft").GetBoolean() ? "true" : "false", draft ? "true" : "false");
            Expect("state", pullRequest.GetProperty("state").GetString(), "open");
            Expect("title", pullRequest.GetProperty("title").GetString(), title);
            Expect("head.ref", pullRequest.GetProperty("head").GetProperty("ref").GetString(), branch);
            Expect("head.sha", pullRequest.GetProperty("head").GetProperty("sha").GetString(), head);
            Expect("base.ref", pullRequest.GetProperty("base").GetProperty("ref").GetString(), repository.DefaultBranch);
            Expect("user.login", pullRequest.GetProperty("user").GetProperty("login").GetString(), login, StringComparison.OrdinalIgnoreCase);
            Assert.True(differences.Count == 0, $"GitHub's #{Show(number.Value)} differs: " + string.Join("; ", differences));
            run.Note($"Read back: #{Show(number.Value)} is an open {(draft ? "draft" : "pull request")} from {branch} at {head} into {repository.DefaultBranch}, opened by {login} ({read}).");

            // Step 8: the same create again (r5 14), recorded as GitHub answers it. The create form
            // relies on its refusal (D316, LivePremises.AlreadyExists): another answer is a problem,
            // which fails the test after the clean-up.
            step = "8, the duplicate create";
            var duplicate = await api.CreatePullRequestAsync(branch, repository.DefaultBranch, title, draft, Cancelled);
            run.Note($"The duplicate create (POST …/pulls with the same head, base, title and draft {(draft ? "true" : "false")}): {duplicate}; 422 with a first error's message starting \"{LivePremises.AlreadyExists}\" expected. {duplicate.BodyForNotes}");
            if (duplicate is { Status: 201, Json: { } second } && second.TryGetProperty("number", out var secondNumber))
            {
                duplicates.Add(secondNumber.GetInt32());
            }

            if (!LivePremises.IsDuplicateRefusal(duplicate))
            {
                problems.Add($"The duplicate create answered {duplicate}, not 422 with a detail starting \"{LivePremises.AlreadyExists}\" as the client reads it (its first error's message), which the create form reads to show the open pull request (D316).");
            }
        }
        catch (Exception ex)
        {
            failure = run.FailureOf(ex, step);
            var last = requests.Snapshot() is [.., var lastRequest] ? $" The app's last request: {lastRequest.Describe()}." : string.Empty;
            run.Note($"Check_2 failed in step {step}: {ex.GetType().Name}: {ex.Message}{last}");
        }
        finally
        {
            try
            {
                // Step 9: the clean-up, each call on its own (D324); a failure is recorded and fails the
                // test. Before the app closes, so that nothing the closing does can keep it from running.
                if (createClicked)
                {
                    await CleanUpAsync(api, sandbox, branch, number, duplicates, problems);
                }
            }
            finally
            {
                app?.Dispose();
            }
        }

        // Step 10: the tripwires (D327).
        if (app is not null)
        {
            try
            {
                TokenTripwire.AssertAbsent(sandbox.Token, [data.Path, clones.Path, home.Path], app.Session.GitCalls);

                // The tripwire searches each call's command, output and error; its standard input,
                // which a call records only for update-ref --stdin, is searched here.
                foreach (var call in app.Session.GitCalls.Snapshot())
                {
                    if (LiveTokenForms.FoundIn(call.StandardInput, sandbox.Token) is { } form)
                    {
                        throw new TokenTripwireException($"The token was found in the standard input of the app's git call {call.Sequence.ToString(CultureInfo.InvariantCulture)} ({form}).");
                    }
                }

                run.Note("The token's tripwire: not in the data folder, the clone, the identity's home or the app's git calls (command, output, error and standard input).");
            }
            catch (TokenTripwireException ex)
            {
                problems.Add(run.Screen(ex.Message));
                run.Note(ex.Message);
            }

            NoteAvatars(data.Path);
        }

        var all = requests.Snapshot();
        foreach (var line in LiveRequestLog.PerHost(all))
        {
            run.Note(line);
        }

        problems.AddRange(LiveRequestLog.ReachProblems(all));
        if (all.FirstOrDefault(request => signInStarted is null || request.Time < signInStarted) is { } early)
        {
            problems.Add($"A request left before the sign-in: {early.Describe()}.");
        }

        run.Note($"X-RateLimit-Remaining at the end: {api.RateLimitRemaining ?? "not sent"}.");
        foreach (var problem in problems)
        {
            run.Note("Problem: " + problem);
        }

        failure?.Throw();
        Assert.True(problems.Count == 0, run.Screen(string.Join("\n", problems)));
    }

    /// <summary>
    /// Check_3 (D342, after the live run of 2026-10-30): the app's own <see cref="GitHubProvider"/>
    /// against <c>https://api.github.com/</c> with the sandbox's token, over the session's handler
    /// behind <see cref="LiveRequestLog"/>, as Check_2's clients go. (a) Asking for an API version
    /// GitHub does not have (<see cref="LiveVersionRewriter"/>), A1 fails as
    /// <see cref="HostingFailureKind.ApiVersion"/> with GitHub's explanation, which names the header.
    /// (b) A6's lookups alone, without the commits page, as the app sends them when the page is not
    /// due (<c>RepositoryHosting.Avatars.cs</c>; with the page, the latest commit's address would be
    /// mapped before its lookup): a commit GitHub does not know with an address it links to no one,
    /// then the latest commit of the default branch with its author's address. The first is left out
    /// and the lookups go on, so the second maps to the sandbox's owner. Then D327's reach over its
    /// requests, of which each step must have sent at least one through the log, and the token in
    /// none of the provider's log lines or failures. Its notes give GitHub's words, the lookups'
    /// statuses and request ids in order, and counts, never an address. Before D342's change to the
    /// client and the provider, (a) and (b) fail.
    /// </summary>
    [Fact]
    public async Task Check_3_the_providers_answers_from_github()
    {
        var sandbox = LiveSettings.Require();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss'Z'", CultureInfo.InvariantCulture);
        var requests = new LiveRequestLog.Store();
        var log = new LiveLog();
        var step = "1, the sandbox";
        ExceptionDispatchInfo? failure = null;
        var problems = new List<string>();
        try
        {
            run.Note("Check_3. The app's GitHubProvider against api.github.com (D342).");
            using var api = new GitHubLiveApi(sandbox, run.Withheld);
            var repository = await ReadSandboxAsync(api, sandbox);
            var hosted = new HostedRepository(HostingProvider.GitHub, GitHubEndpoints.GitHubCom.Host, sandbox.Owner, sandbox.Name);

            // (a) A1 asking for a version GitHub does not have. Each step proves that its requests
            // passed the log, so that D327's reach below is not checked over none of them.
            step = "2, an API version GitHub does not have";
            using (var provider = Provider(requests, log, UnknownVersion))
            {
                var before = requests.Count;
                var checkedToken = await provider.CheckTokenAsync(sandbox.Token, Cancelled);
                if (!requests.Snapshot().Skip(before).Any(request => request is { Method: "GET", Path: "/user", AuthorizationSent: true }))
                {
                    problems.Add("The provider's A1 (GET /user) did not pass LiveRequestLog, so D327's reach is not checked over it.");
                }

                if (checkedToken.Failure is not { } refused)
                {
                    problems.Add($"A1 (GET /user) asking for X-GitHub-Api-Version {UnknownVersion} succeeded.");
                }
                else if (LiveTokenForms.FoundIn(refused.Message + "\n" + refused.Detail, sandbox.Token) is { } form)
                {
                    problems.Add($"A1's failure asking for X-GitHub-Api-Version {UnknownVersion} held the token ({form}).");
                }
                else
                {
                    run.Note($"The provider's A1 (GET /user) asking for X-GitHub-Api-Version {UnknownVersion}: {refused.Kind} (request {refused.RequestId ?? "none"}): {refused.Message}");
                    if (refused.Kind != HostingFailureKind.ApiVersion || !LivePremises.NamesVersionHeader(refused.Message))
                    {
                        problems.Add($"A1 asking for X-GitHub-Api-Version {UnknownVersion} failed as {refused.Kind}, not as ApiVersion with a message that names {LivePremises.VersionHeader} (D342).");
                    }
                }
            }

            // (b) A6's lookups alone: a commit GitHub does not know first, then the latest commit.
            // The latest commit's author's address is never noted (as LatestCommitAsync's).
            step = "3, the avatars' lookups";
            var latest = await api.LatestCommitAsync(repository.DefaultBranch, Cancelled)
                ?? throw new XunitException($"GitHub gave no latest commit of {repository.DefaultBranch} with a SHA and an author's address.");
            var authorAddress = latest.Email.ToLowerInvariant();
            var commitsPath = $"/repos/{sandbox.Owner}/{sandbox.Name}/commits";
            using (var provider = Provider(requests, log))
            {
                var before = requests.Count;
                var read = await provider.ReadAuthorsAsync(
                    sandbox.Token,
                    hosted,
                    null,
                    [new AuthorCommit(NoAuthor2, LivePremises.UnknownSha(stamp), hosted), new AuthorCommit(authorAddress, latest.Sha, hosted)],
                    Cancelled);

                // Each lookup's answer in order, from the log, which keeps no query and so no address.
                var lookups = requests.Snapshot().Skip(before)
                    .Where(request => request.Method == "GET" && string.Equals(request.Path, commitsPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                run.Note("The provider's A6 lookups as the log saw them, in order: "
                    + (lookups.Count == 0
                        ? "none."
                        : string.Join(", ", lookups.Select(request => $"{(request.Status is { } status ? Show(status) : "failed: " + request.FailedWith)} (request {request.RequestId ?? "none"})")) + "."));
                if (lookups.Count == 0)
                {
                    problems.Add("None of the provider's A6 lookups passed LiveRequestLog, so D327's reach is not checked over them.");
                }

                if (read.Value is not { } map)
                {
                    problems.Add($"A6's lookups failed: {read.Failure?.Kind} (request {read.Failure?.RequestId ?? "none"}).");
                }
                else
                {
                    var linked = map.ByEmail.Values.Count(avatar => avatar is not null);
                    run.Note($"The provider's A6 lookups alone (a commit GitHub does not know with {NoAuthor2}, then the latest commit of {repository.DefaultBranch} with its author's address): "
                        + $"{Show(map.ByEmail.Count)} addresses in the map, {Show(linked)} of them linked to an account.");
                    if (map.ByEmail.TryGetValue(NoAuthor2, out var stranger))
                    {
                        problems.Add($"A6 kept {NoAuthor2}, whose commit GitHub does not know, as {(stranger is null ? "linked to no one" : "linked to " + stranger.Login)}: it is left out (D342).");
                    }

                    if (!map.ByEmail.TryGetValue(authorAddress, out var found) || found is null || !string.Equals(found.Login, sandbox.Owner, StringComparison.OrdinalIgnoreCase))
                    {
                        problems.Add("A6 did not map the latest commit's author's address to " + sandbox.Owner + ": "
                            + (found is not null
                                ? $"it is linked to {found.Login}."
                                : map.ByEmail.ContainsKey(authorAddress)
                                    ? "it is linked to no one."
                                    : "it is absent: its own lookup failed, or the lookups stopped before it (the lookups' answers are noted above; D342)."));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            failure = run.FailureOf(ex, step);
            run.Note($"Check_3 failed in step {step}: {ex.GetType().Name}: {ex.Message}");
        }

        // The tripwires (D327): every request to api.github.com, over https and with the token
        // there alone, and the token in none of the provider's log lines.
        var all = requests.Snapshot();
        foreach (var line in LiveRequestLog.PerHost(all))
        {
            run.Note("Check_3: " + line);
        }

        problems.AddRange(LiveRequestLog.ReachProblems(all));
        if (all.FirstOrDefault(request => !string.Equals(request.Host, LiveRequestLog.ApiHost, StringComparison.OrdinalIgnoreCase)) is { } elsewhere)
        {
            problems.Add($"The provider asked another host than {LiveRequestLog.ApiHost}: {elsewhere.Describe()}.");
        }

        if (log.FoundIn(sandbox.Token) is { } logged)
        {
            problems.Add($"A line of the provider's log held the token ({logged}).");
        }
        else
        {
            run.Note($"The token's tripwire: not in the provider's {Show(log.Count)} log lines.");
        }

        foreach (var problem in problems)
        {
            run.Note("Problem: " + problem);
        }

        failure?.Throw();
        Assert.True(problems.Count == 0, run.Screen(string.Join("\n", problems)));
    }

    // ---- The sandbox ---------------------------------------------------------------------------

    /// <summary>
    /// 14.4 Check_1 step 2, once per run (<see cref="LiveRun.Repository"/>): <c>GET /repos/{o}/{r}</c>
    /// answered 200, <c>permissions.push</c> true (the user's role as GitHub reports it, not the
    /// token's grant, which only the push proves), the sandbox private (D326: then the clone proves that the
    /// token reached git) and not a fork (whose pull requests the app keeps in the parent, 0.2);
    /// visibility, default branch, the selected API version, <c>X-OAuth-Scopes</c> and the token's
    /// expiry recorded.
    /// </summary>
    private async Task<LiveRepository> ReadSandboxAsync(GitHubLiveApi api, LiveSandbox sandbox)
    {
        if (run.Repository is { } known)
        {
            return known;
        }

        var answer = await api.ReadRepositoryAsync(Cancelled);
        if (answer is not { Status: 200, Json: { ValueKind: JsonValueKind.Object } json })
        {
            throw new GitHubLiveException(api.Describe(answer, sandbox.FullName) + ": 401 means the token is wrong or expired, 404 that it cannot see the repository or the name is wrong.");
        }

        var repository = new LiveRepository(
            json.GetProperty("full_name").GetString() ?? sandbox.FullName,
            json.GetProperty("private").GetBoolean(),
            json.TryGetProperty("visibility", out var visibility) ? visibility.GetString() ?? "not given" : "not given",
            json.GetProperty("default_branch").GetString() ?? string.Empty,
            json.TryGetProperty("permissions", out var permissions) && permissions.TryGetProperty("push", out var push) && push.GetBoolean());
        run.Note($"GET /repos/{repository.FullName}: {answer}; visibility {repository.Visibility}, default branch {repository.DefaultBranch}, permissions.push (the user's role, as GitHub reports it): {(repository.CanPush ? "true" : "false")}; "
            + $"API version selected {answer.ApiVersionSelected ?? "not sent"} (asked {VisualCommit.Hosting.GitHub.GitHubApi.Version}); X-OAuth-Scopes {(answer.ScopesHeaderPresent ? "present" : "absent")}; "
            + $"the token expires {answer.TokenExpiration ?? "(no GitHub-Authentication-Token-Expiration)"}.");
        if (!repository.CanPush)
        {
            throw new XunitException($"GitHub's permissions.push for {sandbox.FullName} is false: the token's account has no push role there, so the sandbox is not that account's own (docs/phase-7a-design.md 14.6).");
        }

        if (!repository.IsPrivate)
        {
            throw new XunitException($"{sandbox.FullName} is public: D326 wants the sandbox private, so that the clone proves the token reached git.");
        }

        if (json.TryGetProperty("fork", out var fork) && fork.ValueKind == JsonValueKind.True)
        {
            throw new XunitException($"{sandbox.FullName} is a fork: the app lists and creates a fork's pull requests in its parent (docs/phase-7a-design.md 0.2), so the sandbox must be a repository of its own (14.6).");
        }

        if (repository.DefaultBranch.Length == 0)
        {
            throw new XunitException($"GitHub gave {sandbox.FullName} no default branch.");
        }

        run.Repository = repository;
        return repository;
    }

    /// <summary>
    /// GitHub's texts that the fake writes from memory (2.4; 15 point 1), each beside the fake's
    /// answer to the same probe as the fake gives it now (<see cref="LiveFake"/>): an API version it
    /// does not have, <c>type</c> beside <c>visibility</c>, a SHA and a branch it does not know; the
    /// avatars' lookups (D339, D342): the latest commit of <paramref name="branch"/> asked with its
    /// author's address, which the notes never name, and with an address no commit has, and a SHA it
    /// does not know (all <c>f</c>s, and <see cref="LivePremises.UnknownSha"/> of
    /// <paramref name="stamp"/>) beside either address; and the fake's answer for an empty
    /// repository, which the sandbox cannot show. Each note gives the status, <c>Content-Type</c>, the
    /// selected API version and <c>X-RateLimit-Remaining</c>; each body is kept whole only for a
    /// refusal (<see cref="LiveAnswer.BodyForNotes"/>). Returns the premises (<see cref="LivePremises"/>)
    /// that GitHub's answers broke, a premise's probe without an answer among them; any other probe
    /// without one is noted and fails nothing. The fake is given the sandbox's
    /// <paramref name="owner"/> and <paramref name="name"/> alone. Internal for
    /// <see cref="LiveSafetyTests"/>, which runs it with a second fake standing in for GitHub.
    /// </summary>
    internal async Task<List<string>> ProbeTextsAsync(GitHubLiveApi api, string owner, string name, string branch, string stamp)
    {
        var broken = new List<string>();
        await using var fake = await StartFakeAsync(owner, name);
        var what = $"X-GitHub-Api-Version {UnknownVersion} on GET /repos/{{o}}/{{r}}";
        var version = await ProbeAsync(what, () => api.ProbeVersionAsync(UnknownVersion, Cancelled), Fake(fake, f => f.Api.ProbeVersionAsync(UnknownVersion, Cancelled)));
        if (version is null || !LivePremises.NamesVersionHeader(version))
        {
            broken.Add($"{what}: {Answered(version)}, not 400 with a first error or a message that names {LivePremises.VersionHeader} (the client's ApiVersion, D342).");
        }

        await ProbeAsync("GET /user/repos?type=owner&visibility=all", () => api.ProbeTypeWithVisibilityAsync(Cancelled), Fake(fake, f => f.Api.ProbeTypeWithVisibilityAsync(Cancelled)));
        await ProbeAsync($"GET /repos/{{o}}/{{r}}/commits?sha={UnknownSha}", () => api.ProbeCommitsAsync(UnknownSha, Cancelled), Fake(fake, f => f.Api.ProbeCommitsAsync(UnknownSha, Cancelled)));
        await ProbeAsync(
            $"GET /repos/{{o}}/{{r}}/commits?sha={MissingBranch} (the commits page's path for a branch GitHub lacks)",
            () => api.ProbeCommitsAsync(MissingBranch, Cancelled),
            Fake(fake, f => f.Api.ProbeCommitsAsync(MissingBranch, Cancelled)));

        // The avatars' lookups (D339): of a commit GitHub does not know, each leaves out only its own
        // address and the others go on (D342), whoever the address is.
        what = $"GET /repos/{{o}}/{{r}}/commits?sha={UnknownSha}&author={NoAuthor}";
        var unknown = await ProbeAsync(what, () => api.ProbeLookupAsync(UnknownSha, NoAuthor, Cancelled), Fake(fake, f => f.Api.ProbeLookupAsync(UnknownSha, NoAuthor, Cancelled)));
        LeavesOutOnlyItsAddress(broken, what, unknown);
        (string Sha, string Email)? latest;
        try
        {
            latest = await api.LatestCommitAsync(branch, Cancelled);
        }
        catch (GitHubLiveException ex)
        {
            run.Note($"The avatars' lookup (D339): not tried, the latest commit could not be read: {ex.Message}");
            latest = null;
        }

        if (latest is { } commit)
        {
            what = $"GET /repos/{{o}}/{{r}}/commits?sha=<the latest commit of {branch}>&author=<its author's address> (the avatars' lookup, D339)";
            var found = await ProbeAsync(
                what,
                () => api.ProbeLookupAsync(commit.Sha, commit.Email, Cancelled),
                Fake(fake, f => f.Api.ProbeLookupAsync(f.Sha, LiveFake.Email, Cancelled)),
                commit.Email,
                LiveFake.Email);
            if (found is null || !LivePremises.IsArrayOf(found, 1))
            {
                broken.Add($"{what}: {Answered(found)}, not 200 with an array of 1 (D339).");
            }

            what = $"GET /repos/{{o}}/{{r}}/commits?sha=<the latest commit of {branch}>&author={NoAuthor}";
            var none = await ProbeAsync(what, () => api.ProbeLookupAsync(commit.Sha, NoAuthor, Cancelled), Fake(fake, f => f.Api.ProbeLookupAsync(f.Sha, NoAuthor, Cancelled)));
            if (none is null || !LivePremises.IsArrayOf(none, 0))
            {
                broken.Add($"{what}: {Answered(none)}, not 200 with an array of 0 (D339).");
            }

            what = $"GET /repos/{{o}}/{{r}}/commits?sha={UnknownSha}&author=<that address>";
            unknown = await ProbeAsync(
                what,
                () => api.ProbeLookupAsync(UnknownSha, commit.Email, Cancelled),
                Fake(fake, f => f.Api.ProbeLookupAsync(UnknownSha, LiveFake.Email, Cancelled)),
                commit.Email,
                LiveFake.Email);
            LeavesOutOnlyItsAddress(broken, what, unknown);
            var other = LivePremises.UnknownSha(stamp);
            what = $"GET /repos/{{o}}/{{r}}/commits?sha={other}&author=<that address> (a SHA GitHub does not know, not all f's)";
            unknown = await ProbeAsync(
                what,
                () => api.ProbeLookupAsync(other, commit.Email, Cancelled),
                Fake(fake, f => f.Api.ProbeLookupAsync(other, LiveFake.Email, Cancelled)),
                commit.Email,
                LiveFake.Email);
            LeavesOutOnlyItsAddress(broken, what, unknown);
        }
        else
        {
            run.Note("The avatars' lookup (D339): not tried, GitHub gave no latest commit, or one without a SHA or an author's address.");
            broken.Add($"The avatars' lookups with the latest commit of {branch} and its author's address (D339, D342): not tried, GitHub gave no such commit.");
        }

        var empty = await FakeAnswerAsync(null, Fake(fake, f => f.EmptyApi.ProbeCommitsAsync("main", Cancelled)));
        run.Note($"GitHub's text for the commits of an empty repository: not tried (the sandbox has a commit); the fake: {empty}.");
        return broken;
    }

    /// <summary>
    /// One probe of <see cref="ProbeTextsAsync"/>: GitHub's answer, its headers and its body beside
    /// the fake's (<see cref="FakeAnswerAsync"/>), or why none came; GitHub's answer, or null without
    /// one. <paramref name="address"/>, an address the request asked with, is replaced in a body that
    /// echoes it, as written and escaped; <paramref name="fakeAddress"/> is the fake's.
    /// </summary>
    private async Task<LiveAnswer?> ProbeAsync(string what, Func<Task<LiveAnswer>> probe, Func<Task<LiveAnswer>>? fake, string? address = null, string? fakeAddress = null)
    {
        LiveAnswer answer;
        try
        {
            answer = await probe();
        }
        catch (GitHubLiveException ex)
        {
            run.Note($"GitHub's text for {what}: no answer: {ex.Message}");
            return null;
        }

        var beside = await FakeAnswerAsync(answer, fake, address, fakeAddress);
        run.Note($"GitHub's text for {what}: {answer}; {answer.HeadersForNotes}; the fake: {beside}. {LiveAnswer.Hide(answer.BodyForNotes, address)}");
        return answer;
    }

    /// <summary>
    /// The fake's answer to a probe beside <paramref name="github"/>'s, or alone without it
    /// (<see cref="LiveAnswer.FakeBeside"/>), or why there is none: a finding only, so a failure of
    /// the fake's ends no probe of GitHub's.
    /// </summary>
    private static async Task<string> FakeAnswerAsync(LiveAnswer? github, Func<Task<LiveAnswer>>? fake, string? address = null, string? fakeAddress = null)
    {
        if (fake is null)
        {
            return "not asked (it did not start)";
        }

        try
        {
            return LiveAnswer.FakeBeside(github, await fake(), address, fakeAddress);
        }
        catch (Exception ex) when (!Cancelled.IsCancellationRequested)
        {
            // The fake's side is a finding: whatever fails there is recorded and the probes go on.
            return $"no answer ({ex.GetType().Name}: {ex.Message})";
        }
    }

    /// <summary>The fake of GitHub in process for <see cref="ProbeTextsAsync"/>, with the sandbox's <paramref name="owner"/> and <paramref name="name"/> and never its token, or null, noted, when it could not start: its side is a finding only.</summary>
    private async Task<LiveFake?> StartFakeAsync(string owner, string name)
    {
        try
        {
            return await LiveFake.StartAsync(owner, name, Cancelled);
        }
        catch (Exception ex) when (!Cancelled.IsCancellationRequested)
        {
            // As for its answers: the probes of GitHub go on without the fake's beside them.
            run.Note($"The fake of GitHub in process did not start, so no probe has its answer beside GitHub's: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The fake's side of a probe, or null without the fake.</summary>
    private static Func<Task<LiveAnswer>>? Fake(LiveFake? fake, Func<LiveFake, Task<LiveAnswer>> probe) =>
        fake is { } started ? () => probe(started) : null;

    /// <summary>Adds to <paramref name="broken"/> a lookup of a commit GitHub does not know that it answered with a status after which the provider would not go on (D342), or not at all.</summary>
    private static void LeavesOutOnlyItsAddress(List<string> broken, string what, LiveAnswer? answer)
    {
        if (answer is null || !LivePremises.LeavesOutOnlyItsAddress(answer))
        {
            broken.Add($"{what}: {Answered(answer)}, not 422, a 5xx or 404, after which the provider leaves out only that address and goes on (D342).");
        }
    }

    /// <summary>A probe's answer in a premise's sentence: its status, request id and body's shape, or that none came.</summary>
    private static string Answered(LiveAnswer? answer) => answer is null ? "no answer" : $"{answer}, {answer.Shape}";

    // ---- The clean-up --------------------------------------------------------------------------

    /// <summary>
    /// 14.4 step 9: closes the created pull request, and any a duplicate create made, then deletes the
    /// branch; each call on its own, with its own 30 s and not the test's cancellation, each recorded.
    /// A number is closed only once GitHub confirms it as the sandbox's pull request from the branch
    /// (<see cref="LiveLeftovers.IsFrom"/>); without a confirmed one (the app's answer never came, or
    /// named another) the sandbox's open pull requests from the branch are looked up by their head.
    /// A refusal goes to <paramref name="problems"/>; the next Check_1 removes what is left (D324).
    /// </summary>
    private async Task CleanUpAsync(GitHubLiveApi api, LiveSandbox sandbox, string branch, int? number, List<int> duplicates, List<string> problems)
    {
        var toClose = new List<int>();
        foreach (var candidate in (number is { } known ? [known] : Array.Empty<int>()).Concat(duplicates))
        {
            try
            {
                var read = await api.ReadPullRequestAsync(candidate, CancellationToken.None);
                if (read is { Status: 200, Json: { ValueKind: JsonValueKind.Object } pull } && LiveLeftovers.IsFrom(LivePullRequest.From(pull), branch, sandbox.FullName))
                {
                    toClose.Add(candidate);
                }
                else
                {
                    problems.Add($"#{Show(candidate)} in {sandbox.FullName} is not confirmed as the pull request from {branch} ({read}), so it was not closed.");
                }
            }
            catch (Exception ex)
            {
                // The clean-up records what went wrong and goes on: it runs in the test's finally.
                problems.Add($"The clean-up could not read #{Show(candidate)}: {ex.GetType().Name}: {api.Screen(ex.Message)}");
            }
        }

        if (number is null || !toClose.Contains(number.Value))
        {
            try
            {
                var open = await api.OpenPullRequestsAsync(CancellationToken.None, head: $"{sandbox.Owner}:{branch}");
                toClose.AddRange(open.Where(pull => LiveLeftovers.IsFrom(pull, branch, sandbox.FullName) && !toClose.Contains(pull.Number)).Select(pull => pull.Number));
            }
            catch (Exception ex)
            {
                // The clean-up records what went wrong and goes on: it runs in the test's finally.
                problems.Add($"The clean-up could not look for a pull request from {branch}: {ex.GetType().Name}: {api.Screen(ex.Message)}");
            }
        }

        foreach (var pull in toClose)
        {
            try
            {
                var closed = await api.ClosePullRequestAsync(pull, CancellationToken.None);
                run.Note($"Clean-up: PATCH …/pulls/{Show(pull)} state closed: {closed}.");
                if (closed.Status != 200)
                {
                    problems.Add(api.Describe(closed, $"closing #{Show(pull)}"));
                }
            }
            catch (Exception ex)
            {
                problems.Add($"{ex.GetType().Name}: {api.Screen(ex.Message)}");
            }
        }

        try
        {
            var deleted = await api.DeleteBranchAsync(branch, CancellationToken.None);
            run.Note($"Clean-up: DELETE …/git/refs/heads/{branch}: {deleted}.");

            // 422 without a pull request: the push never reached GitHub, so no branch was left.
            if (deleted.Status != 204 && !(deleted.Status == 422 && number is null && toClose.Count == 0))
            {
                problems.Add(api.Describe(deleted, $"deleting {branch}"));
            }
        }
        catch (Exception ex)
        {
            problems.Add($"{ex.GetType().Name}: {api.Screen(ex.Message)}");
        }
    }

    // ---- The app -------------------------------------------------------------------------------

    /// <summary>
    /// Waits for <paramref name="condition"/> and fails at once when a dialog of git's prompts or an
    /// error dialog opens meanwhile (14.4 steps 4 and 6): a credentials dialog means the app's helper
    /// did not answer git (D255). The dialog is cancelled first, so that git is not left waiting.
    /// </summary>
    private async Task WaitWatchingAsync(ShellDriver app, Func<bool> condition, string what, TimeSpan timeout)
    {
        await app.WaitForAsync(() => Interruption(app) is not null || condition(), what, timeout);
        switch (Interruption(app))
        {
            case PromptDialogViewModel prompt:
                prompt.Cancel();
                throw new XunitException("Git asked for credentials: the app's helper did not answer (D255).");
            case ErrorDialogViewModel error:
                var text = $"{error.Title}: {error.Summary}";
                error.Cancel();
                throw new XunitException(run.Screen("The app showed an error: " + text));
        }
    }

    /// <summary>Clicks the form's Create and waits until it closes or shows its error line, failing on a credentials or error dialog meanwhile.</summary>
    private Task CreateAsync(ShellDriver app, CreatePullRequestDialogViewModel form)
    {
        app.Click(app.FindByAutomationId("ConfirmButton"));
        return WaitWatchingAsync(app, () => form.IsClosed || (!form.IsCreating && form.Error is not null), "the push and the create to end", NetworkStep);
    }

    /// <summary>The first operation titled <paramref name="title"/> after <paramref name="mark"/> in the activity log, once it is there.</summary>
    private static async Task<OperationRecord> OperationAfterAsync(ShellDriver app, long mark, string title)
    {
        OperationRecord? found = null;
        await app.WaitForAsync(
            () => (found = app.Session.Operations.Snapshot().FirstOrDefault(record => record.Sequence > mark && record.Title == title)) is not null,
            "the create's operation in the activity log",
            NetworkStep);
        return found!;
    }

    /// <summary>
    /// How the app's avatar run for <paramref name="repository"/> ended since <paramref name="mark"/>
    /// (11.3), or null while it runs: the <see cref="AppLogFile.AvatarsShown"/> line, or an answer
    /// after which no picture comes: the avatar host's other than 200 (a redirect to another host
    /// among them, 6.4), or the sandbox's or its commits' read (A3, A6) other than 200 or 304 (the
    /// app's client asks again with the ETag it kept).
    /// </summary>
    private static string? AvatarsEnded(ShellDriver app, string repository, int mark, LiveRequestLog.Store requests, DateTimeOffset since, LiveSandbox sandbox)
    {
        var shown = AppLogFile.AvatarsShown(repository);
        if (app.LogLinesSince(mark).Select(line => shown.Match(line)).FirstOrDefault(match => match.Success) is { } line)
        {
            return $"the app showed avatars for {line.Groups["count"].Value} authors";
        }

        var sandboxPath = "/repos/" + sandbox.FullName;
        var ended = requests.Snapshot().FirstOrDefault(request => request.Time >= since
            && request.Status is not (200 or 304)
            && (string.Equals(request.Host, AvatarHost, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(request.Host, LiveRequestLog.ApiHost, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(request.Path, sandboxPath, StringComparison.OrdinalIgnoreCase) || string.Equals(request.Path, sandboxPath + "/commits", StringComparison.OrdinalIgnoreCase)))));
        return ended is null ? null : "no picture, after " + ended.Describe();
    }

    /// <summary>
    /// The app's provider for Check_3, over the session's handler for github.com behind
    /// <see cref="LiveRequestLog"/>, as Check_2's session goes (14.4 step 1); with
    /// <paramref name="version"/> every request asks for that API version (<see cref="LiveVersionRewriter"/>,
    /// between the log and the handler). <paramref name="inner"/> stands in for the session's handler
    /// in <see cref="LiveSafetyTests"/>, which proves the chain offline.
    /// </summary>
    internal static GitHubProvider Provider(LiveRequestLog.Store requests, LiveLog log, string? version = null, Func<HttpMessageHandler>? inner = null) =>
        new(
            GitHubEndpoints.GitHubCom,
            () =>
            {
                var handler = (inner ?? HostingOptions.DefaultHandler(GitHubEndpoints.GitHubCom))();
                return new LiveRequestLog(version is null ? handler : new LiveVersionRewriter(version, handler), requests);
            },
            log,
            TimeProvider.System);

    private static DialogViewModel? Interruption(ShellDriver app) =>
        app.Session.Services.DialogHost.Open.FirstOrDefault(dialog => dialog is PromptDialogViewModel or ErrorDialogViewModel);

    /// <summary>The open repository browser, or null.</summary>
    private static CloneFromGitHubDialogViewModel? Browser(ShellDriver app) =>
        app.Session.Services.DialogHost.Open.OfType<CloneFromGitHubDialogViewModel>().LastOrDefault();

    /// <summary>The browser's shown item named <paramref name="fullName"/> (its automation name), or null.</summary>
    private static ListBoxItem? BrowserItem(ShellDriver app, string fullName) =>
        app.Window.GetVisualDescendants().OfType<ListBoxItem>()
            .FirstOrDefault(item => item.IsEffectivelyVisible && AutomationProperties.GetName(item) == fullName);

    /// <summary>Whether the left panel shows the row at <paramref name="path"/> now (<see cref="ShellDriver.RefRowModel"/>).</summary>
    private static bool HasRow(ShellDriver app, params string[] path)
    {
        try
        {
            app.RefRowModel(path);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The avatar images the session kept: how many, and their sizes (whether <c>s=64</c> sizes a <c>v=4</c> URL, 15 point 1).</summary>
    private void NoteAvatars(string dataDirectory)
    {
        var folder = Path.Combine(dataDirectory, "avatars");
        var images = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Where(path => Path.GetExtension(path) is ".png" or ".jpg").ToList()
            : [];
        var sizes = new List<string>();
        foreach (var image in images)
        {
            try
            {
                using var bitmap = new Bitmap(image);
                sizes.Add($"{Show(bitmap.PixelSize.Width)}×{Show(bitmap.PixelSize.Height)} {Path.GetExtension(image)[1..]}");
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                sizes.Add($"unreadable ({ex.GetType().Name})");
            }
        }

        run.Note(images.Count == 0 ? "Avatars kept: none." : $"Avatars kept: {Show(images.Count)}, {string.Join(", ", sizes)}.");
    }

    // ---- Git -----------------------------------------------------------------------------------

    /// <summary>
    /// The command's commit's environment (14.4 step 6, D325): <see cref="TempRepo.IsolatedEnvironment"/>
    /// with <paramref name="home"/>, the identity <c>VisualCommit live test
    /// &lt;live-test@visualcommit.invalid&gt;</c>, dated now (the process's fixed dates replaced).
    /// </summary>
    private static Dictionary<string, string?> LiveIdentity(string home)
    {
        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'+00:00'", CultureInfo.InvariantCulture);
        return new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(home))
        {
            ["GIT_AUTHOR_NAME"] = IdentityName,
            ["GIT_AUTHOR_EMAIL"] = IdentityEmail,
            ["GIT_COMMITTER_NAME"] = IdentityName,
            ["GIT_COMMITTER_EMAIL"] = IdentityEmail,
            ["GIT_AUTHOR_DATE"] = now,
            ["GIT_COMMITTER_DATE"] = now,
        };
    }

    private static Task<GitResult> GitAsync(GitRunner git, string folder, IReadOnlyDictionary<string, string?> environment, params string[] arguments) =>
        git.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = environment }, Cancelled);

    /// <summary>A git call outside the app that must succeed; its failure names the command and git's error.</summary>
    private async Task<GitResult> GitOkAsync(GitRunner git, string folder, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var result = await GitAsync(git, folder, environment, arguments);
        return result.Succeeded
            ? result
            : throw new XunitException(run.Screen($"git {string.Join(' ', arguments)} failed ({Show(result.ExitCode)}): {result.StandardError.Trim()}"));
    }

    /// <summary>The keys of <c>git config --get-regexp</c>'s lines, without their values.</summary>
    private static IEnumerable<string> Keys(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => line.Split(' ')[0]);

    private static string Show(int value) => value.ToString(CultureInfo.InvariantCulture);
}
