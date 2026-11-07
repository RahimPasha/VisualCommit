using System.Globalization;
using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.RequestTestData;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The requests' menus and items for each provider (docs/phase-7b-design.md 12.4, 12.7; D351, D355,
/// D356, D376): a request's menu, the section's signed in, signed out, loading and in D356's state,
/// the branch and drop items over <see cref="RepositoryHostingState.RequestRemotes"/> (none on
/// GitHub's <c>upstream</c> beside a listed GitLab origin, none while D356's state holds) and the
/// palette's request items; exact lines, as 7a's <c>RepositoryMenusHostingTests</c> writes them.
/// </summary>
public sealed class RepositoryMenusProvidersTests
{
    [Fact]
    public void A_request_s_menu_per_provider()
    {
        var (actions, recorder) = RecordingActions.Create();

        Assert.Equal(["!23 Fix a typo in the footer", "Open in browser", "Check out ada:fix-typo", "—", "Copy link"], Lines(RepositoryMenus.ForPullRequest(Context(Loaded(WebApp)), 23, actions)));
        Assert.Equal(["!22 Draft: Update the readme", "Open in browser", "Check out docs/readme", "—", "Copy link"], Lines(RepositoryMenus.ForPullRequest(Context(Loaded(WebApp)), 22, actions)));
        var mr21 = RepositoryMenus.ForPullRequest(Context(Loaded(WebApp)), 21, actions);
        Assert.Equal(["!21 Add the search box", "Open in browser", "Check out feature/search", "—", "Copy link"], Lines(mr21));
        Assert.Equal("!21 Add the search box", mr21.Entries.OfType<MenuHeader>().Single().ToolTip);

        Assert.Equal(
            ["!13 Fix a typo in the greeting", "Open in browser", "Check out mobile-app-fork:fix-typo (disabled: \"Add a remote for Fernhill-Labs/Mobile App/mobile-app-fork to check out this pull request\")", "—", "Copy link"],
            Lines(RepositoryMenus.ForPullRequest(Context(Loaded(MobileApp), MobileAppRefs()), 13, actions)));
        Assert.Equal(["!12 Write the guide", "Open in browser", "Check out docs/guide", "—", "Copy link"], Lines(RepositoryMenus.ForPullRequest(Context(Loaded(MobileApp), MobileAppRefs()), 12, actions)));
        Assert.Equal(
            ["!11 Add the sign-in page", "Open in browser", "Check out feature/sign-in (disabled: \"feature/sign-in is already checked out\")", "—", "Copy link"],
            Lines(RepositoryMenus.ForPullRequest(Context(Loaded(MobileApp), MobileAppRefs(head: "feature/sign-in")), 11, actions)));

        // A gone one's number in the listed provider's form.
        Assert.Equal(["!9"], Lines(RepositoryMenus.ForPullRequest(Context(Loaded(WebApp)), 9, actions)));

        foreach (var item in mr21.Items)
        {
            item.Invoke();
        }

        Assert.Equal(["OpenPullRequestAsync", "CheckOutPullRequestAsync", "CopyPullRequestLinkAsync"], recorder.Calls.Select(call => call.Method));
        Assert.All(recorder.Calls, call => Assert.Same(MR21, call.Arguments[0]));
    }

    [Fact]
    public void The_section_s_menu_in_each_state()
    {
        var (actions, recorder) = RecordingActions.Create();
        List<string> Section(RepositoryHostingState state) => Lines(RepositoryMenus.ForSection(Context(state, RefsOf(state.Repository!)), RefSection.PullRequests, actions)!);

        Assert.Equal(["Merge requests", "Refresh merge requests", "Open merge requests in browser"], Section(Loaded(WebApp)));
        Assert.Equal(["Merge requests", "Refresh merge requests", "Open merge requests in browser"], Section(State(WebApp, PullRequestsState.Failed, failure: GitLabForbidden)));
        Assert.Equal(["Merge requests", "Refresh merge requests (disabled: \"Merge requests are loading\")", "Open merge requests in browser"], Section(State(WebApp, PullRequestsState.Loading)));
        Assert.Equal(["Merge requests", "Sign in to GitLab…", "Open merge requests in browser"], Section(State(WebApp, PullRequestsState.SignedOut)));

        Assert.Equal(["Pull requests", "Refresh pull requests", "Open pull requests in browser"], Section(Loaded(MobileApp)));
        Assert.Equal(["Pull requests", "Refresh pull requests (disabled: \"Pull requests are loading\")", "Open pull requests in browser"], Section(State(MobileApp, PullRequestsState.Loading)));
        Assert.Equal(["Pull requests", "Sign in to Azure DevOps…", "Open pull requests in browser"], Section(State(MobileApp, PullRequestsState.SignedOut)));
        Assert.Equal(["Pull requests", "Open pull requests in browser"], Section(OtherOrganization()));

        RepositoryMenus.ForSection(Context(State(WebApp, PullRequestsState.SignedOut)), RefSection.PullRequests, actions)!.Items.First().Invoke();
        RepositoryMenus.ForSection(Context(OtherOrganization(), MobileAppRefs()), RefSection.PullRequests, actions)!.Items.Single().Invoke();
        Assert.Equal(["SignInToHostAsync", "OpenPullRequestsInBrowserAsync"], recorder.Calls.Select(call => call.Method));
        Assert.Null(RepositoryMenus.ForSection(Context(null), RefSection.PullRequests, actions));
    }

    [Fact]
    public void Branch_items_per_provider_and_none_on_github_s_upstream()
    {
        var (actions, recorder) = RecordingActions.Create();
        foreach (var (repository, branch, number, create) in new[] { (WebApp, "feature/search", 21, "Create merge request…"), (MobileApp, "feature/sign-in", 11, "Create pull request…") })
        {
            var refs = RefsOf(repository);
            var words = repository == WebApp ? "merge" : "pull";
            List<string> Local(RepositoryHostingState? state, string name) => Lines(RepositoryMenus.ForLocalBranch(Context(state, refs), "refs/heads/" + name, actions));
            List<string> Remote(RepositoryHostingState? state, string name) => Lines(RepositoryMenus.ForRemoteBranch(Context(state, refs), "refs/remotes/" + name, actions));
            var signedOut = State(repository, PullRequestsState.SignedOut);

            // The request's branch: "Open {R} {N}" ends its push group; signed out (no map) the create; without hosting none.
            Assert.Equal(TrackedLocalMenu(branch, $"Open {words} request !{number}"), Local(Loaded(repository), branch));
            Assert.Equal(TrackedLocalMenu(branch, create), Local(signedOut, branch));
            Assert.Equal(TrackedLocalMenu(branch, null), Local(null, branch));

            // A branch without upstream: the create after its push, signed in or out (the create signs in first).
            Assert.Equal(LocalWorkMenu(create), Local(Loaded(repository), "local-work"));
            Assert.Equal(LocalWorkMenu(create), Local(signedOut, "local-work"));
            Assert.Equal(LocalWorkMenu(null), Local(null, "local-work"));

            // Remote branches: the item in a group of its own after the delete group.
            var (draftBranch, draftNumber) = repository == WebApp ? ("docs/readme", 22) : ("docs/guide", 12);
            Assert.Equal(RemoteBranchMenu("origin", draftBranch, $"Open {words} request !{draftNumber}"), Remote(Loaded(repository), "origin/" + draftBranch));
            Assert.Equal(RemoteBranchMenu("origin", "topic/no-request", create), Remote(Loaded(repository), "origin/topic/no-request"));
            Assert.Equal(RemoteBranchMenu("origin", "topic/no-request", create), Remote(signedOut, "origin/topic/no-request"));
            Assert.Equal(RemoteBranchMenu("origin", "topic/no-request", null), Remote(null, "origin/topic/no-request"));
        }

        // upstream is GitHub's, no key of RequestRemotes (ruling 5, 12.4): no request item, the menu as without hosting.
        var upstream = Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded(WebApp)), "refs/remotes/upstream/main", actions));
        Assert.Equal(RemoteBranchMenu("upstream", "main", null), upstream);
        Assert.Equal(Lines(RepositoryMenus.ForRemoteBranch(Context(null), "refs/remotes/upstream/main", actions)), upstream);

        // A remote of another Azure DevOps organization beside the listed origin is no request remote (1.4, D356).
        var (harbor, harborRefs) = WithHarborCo();
        Assert.Equal(["origin"], harbor.RequestRemotes.Keys);
        var harborMain = Lines(RepositoryMenus.ForRemoteBranch(Context(harbor, harborRefs), "refs/remotes/harbor/main", actions));
        Assert.Equal(RemoteBranchMenu("harbor", "main", null), harborMain);
        Assert.Equal(Lines(RepositoryMenus.ForRemoteBranch(Context(null, harborRefs), "refs/remotes/harbor/main", actions)), harborMain);
        Assert.Equal(RemoteBranchMenu("origin", "topic/no-request", "Create pull request…"), Lines(RepositoryMenus.ForRemoteBranch(Context(harbor, harborRefs), "refs/remotes/origin/topic/no-request", actions)));

        RepositoryMenus.ForLocalBranch(Context(Loaded(WebApp)), "refs/heads/feature/search", actions).Items.Single(item => item.Label == "Open merge request !21").Invoke();
        RepositoryMenus.ForLocalBranch(Context(Loaded(MobileApp), MobileAppRefs()), "refs/heads/feature/sign-in", actions).Items.Single(item => item.Label == "Open pull request !11").Invoke();
        RepositoryMenus.ForLocalBranch(Context(Loaded(MobileApp), MobileAppRefs()), "refs/heads/local-work", actions).Items.Single(item => item.Label == "Create pull request…").Invoke();
        Assert.Equal(["OpenPullRequestAsync", "OpenPullRequestAsync", "CreatePullRequestAsync"], recorder.Calls.Select(call => call.Method));
        Assert.Same(MR21, recorder.Calls[0].Arguments[0]);
        Assert.Same(PR11, recorder.Calls[1].Arguments[0]);
        Assert.Equal(("refs/heads/local-work", (object?)null), (recorder.Calls[2].Arguments[0], recorder.Calls[2].Arguments[1]));
    }

    [Fact]
    public void Drops_onto_a_request_base_only()
    {
        var (actions, recorder) = RecordingActions.Create();
        foreach (var (repository, branch, number, create) in new[] { (WebApp, "feature/search", 21, "Create merge request…"), (MobileApp, "feature/sign-in", 11, "Create pull request…") })
        {
            var refs = RefsOf(repository);
            List<string> Drop(string source, string target, RepositoryHostingState? state, RepoRefs? with = null) =>
                Lines(RepositoryMenus.ForDrop(Context(state, with ?? refs), "refs/heads/" + source, target, actions));
            string[] ontoOriginMain = ["Push local-work to origin/main", "Push local-work to origin/main and track it"];
            string[] OntoMain(string source) => [$"Merge {source} into main", $"Merge {source} into main (create a merge commit)", $"Fast-forward main to {source}"];
            string[] ontoTracked = [$"Fast-forward {branch} to local-work", $"Check out {branch} and merge local-work into it"];
            var open = $"Open {(repository == WebApp ? "merge" : "pull")} request !{number}";

            // Onto origin's branch, onto main and onto the request's branch (both tracking one of the same name), signed in or out.
            foreach (var state in new[] { Loaded(repository), State(repository, PullRequestsState.SignedOut) })
            {
                Assert.Equal(DropMenu("local-work", "origin/main", ontoOriginMain, create), Drop("local-work", "refs/remotes/origin/main", state));
                Assert.Equal(DropMenu("local-work", "main", OntoMain("local-work"), create), Drop("local-work", "refs/heads/main", state));
                Assert.Equal(DropMenu("local-work", branch, ontoTracked, create), Drop("local-work", "refs/heads/" + branch, state));
            }

            Assert.Equal(DropMenu(branch, "main", OntoMain(branch), open), Drop(branch, "refs/heads/main", Loaded(repository)));

            // Without hosting every drop as before; onto a branch without upstream, or one whose upstream has another name, none.
            Assert.Equal(DropMenu("local-work", "origin/main", ontoOriginMain, null), Drop("local-work", "refs/remotes/origin/main", null));
            Assert.Equal(DropMenu(branch, "main", OntoMain(branch), null), Drop(branch, "refs/heads/main", null));
            var ontoNoUpstream = Drop("main", "refs/heads/local-work", Loaded(repository));
            Assert.DoesNotContain(ontoNoUpstream, IsRequestLine);
            Assert.Equal(Drop("main", "refs/heads/local-work", null), ontoNoUpstream);
            var renamed = WithRefspecs(refs with { Refs = [.. refs.Refs.Where(r => r.Name != branch), Local(branch, '3', "origin/main")] });
            var ontoRenamed = Drop("local-work", "refs/heads/" + branch, Loaded(repository), renamed);
            Assert.DoesNotContain(ontoRenamed, IsRequestLine);
            Assert.Equal(Drop("local-work", "refs/heads/" + branch, null, renamed), ontoRenamed);
        }

        // Onto GitHub's upstream beside a listed GitLab origin: none.
        var ontoUpstream = Lines(RepositoryMenus.ForDrop(Context(Loaded(WebApp)), "refs/heads/local-work", "refs/remotes/upstream/main", actions));
        Assert.Equal(DropMenu("local-work", "upstream/main", ["Push local-work to upstream/main", "Push local-work to upstream/main and track it"], null), ontoUpstream);
        Assert.Equal(Lines(RepositoryMenus.ForDrop(Context(null), "refs/heads/local-work", "refs/remotes/upstream/main", actions)), ontoUpstream);

        // Onto a remote of another Azure DevOps organization beside the listed origin: none; onto origin's still the create.
        var (harbor, harborRefs) = WithHarborCo();
        var ontoHarbor = Lines(RepositoryMenus.ForDrop(Context(harbor, harborRefs), "refs/heads/local-work", "refs/remotes/harbor/main", actions));
        Assert.Equal(DropMenu("local-work", "harbor/main", ["Push local-work to harbor/main", "Push local-work to harbor/main and track it"], null), ontoHarbor);
        Assert.Equal(Lines(RepositoryMenus.ForDrop(Context(null, harborRefs), "refs/heads/local-work", "refs/remotes/harbor/main", actions)), ontoHarbor);
        var ontoOrigin = RepositoryMenus.ForDrop(Context(harbor, harborRefs), "refs/heads/local-work", "refs/remotes/origin/main", actions);
        Assert.Equal(DropMenu("local-work", "origin/main", ["Push local-work to origin/main", "Push local-work to origin/main and track it"], "Create pull request…"), Lines(ontoOrigin));

        ontoOrigin.Items.Single(item => item.Label == "Create pull request…").Invoke();
        RepositoryMenus.ForDrop(Context(Loaded(MobileApp), MobileAppRefs()), "refs/heads/feature/sign-in", "refs/heads/main", actions).Items.Single(item => item.Label == "Open pull request !11").Invoke();
        Assert.Equal(("CreatePullRequestAsync", "refs/heads/local-work", (object?)"refs/remotes/origin/main"), (recorder.Calls[0].Method, recorder.Calls[0].Arguments[0], recorder.Calls[0].Arguments[1]));
        Assert.Equal("OpenPullRequestAsync", recorder.Calls[1].Method);
        Assert.Same(PR11, recorder.Calls[1].Arguments[0]);
    }

    [Fact]
    public void D356_s_state_offers_no_request_in_any_branch_or_drop_menu()
    {
        var (actions, _) = RecordingActions.Create();
        var refs = MobileAppRefs();
        var context = Context(OtherOrganization(), refs);

        foreach (var lines in new[]
        {
            Lines(RepositoryMenus.ForLocalBranch(context, "refs/heads/main", actions)),
            Lines(RepositoryMenus.ForLocalBranch(context, "refs/heads/local-work", actions)),
            Lines(RepositoryMenus.ForRemoteBranch(context, "refs/remotes/origin/main", actions)),
            Lines(RepositoryMenus.ForDrop(context, "refs/heads/local-work", "refs/remotes/origin/main", actions)),
            Lines(RepositoryMenus.ForDrop(context, "refs/heads/local-work", "refs/heads/main", actions)),
        })
        {
            Assert.DoesNotContain(lines, IsRequestLine);
        }

        // Even with a map and remotes that a request could go to, D356's state offers none (12.4).
        var held = OtherOrganization() with { Remotes = MobileAppRemotes, ByRef = MobileAppByRef };
        Assert.DoesNotContain(Lines(RepositoryMenus.ForLocalBranch(Context(held, refs), "refs/heads/feature/sign-in", actions)), line => line.Contains("request", StringComparison.Ordinal));
    }

    [Fact]
    public void The_palette_s_request_items_in_the_provider_s_words()
    {
        var (actions, _) = RecordingActions.Create();
        var keys = new KeyTexts(CommandKey.Ctrl);

        var items = AppCommands.RepositoryItems(Context(Loaded(WebApp)), actions, keys, hasWorkingChanges: false, diff: null, page: null, commands: []);
        var requests = items.Where(item => item.Detail?.StartsWith('!') == true).Select(item => $"{item.Label} | {item.Detail}").ToList();
        Assert.Equal(
            [
                "Open in browser | !23 Fix a typo in the footer", "Check out ada:fix-typo | !23 Fix a typo in the footer", "Copy link | !23 Fix a typo in the footer",
                "Open in browser | !22 Draft: Update the readme", "Check out docs/readme | !22 Draft: Update the readme", "Copy link | !22 Draft: Update the readme",
                "Open in browser | !21 Add the search box", "Check out feature/search | !21 Add the search box", "Copy link | !21 Add the search box",
            ],
            requests);
        Assert.DoesNotContain(items, item => item.Detail?.StartsWith('#') == true);

        var azure = AppCommands.RepositoryItems(Context(Loaded(MobileApp), MobileAppRefs()), actions, keys, false, null, null, []);
        Assert.Contains(azure, item => item.Label == "Check out mobile-app-fork:fix-typo" && item.Detail == "!13 Fix a typo in the greeting");
        Assert.DoesNotContain(AppCommands.RepositoryItems(Context(OtherOrganization(), MobileAppRefs()), actions, keys, false, null, null, []), item => item.Detail?.StartsWith('!') == true);
    }

    /// <summary>A line that names a request: a create or an open, in either provider's words.</summary>
    private static bool IsRequestLine(string line) => line.Contains(" request", StringComparison.Ordinal);

    /// <summary>
    /// mobile-app's state and refs with a remote <c>harbor</c> of harbor-co's <c>manifest</c> beside
    /// <c>origin</c>: another organization than the listed one's, so no key of <c>RequestRemotes</c>.
    /// </summary>
    private static (RepositoryHostingState State, RepoRefs Refs) WithHarborCo() =>
    (
        Loaded(MobileApp) with { Remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = MobileApp, ["harbor"] = Manifest } },
        WithRefspecs(MobileAppRefs(extra: [Remote("harbor/main", '2')]) with { Remotes = ["harbor", "origin"] })
    );

    /// <summary>A local branch tracking <c>origin/{branch}</c>, up to date, HEAD on <c>main</c>: 7a's lines, the request item ending the push group.</summary>
    private static string[] TrackedLocalMenu(string branch, string? item) =>
    [
        branch, $"Check out {branch}", $"Merge {branch} into main", $"Merge {branch} into main (create a merge commit)",
        $"Rebase main onto {branch}", $"Interactive rebase of main onto {branch}…",
        $"Fast-forward {branch} to origin/{branch} (disabled: \"{branch} is up to date with origin/{branch}\")", "—",
        $"Push {branch} to origin/{branch}", $"Force push {branch}… (danger)", .. Optional(item), "—",
        "Create branch here…", "Create tag here…", $"Rename {branch}…", $"Delete {branch}… (danger)", "—",
        "Change upstream…", $"Stop tracking origin/{branch}", "—",
        $"Compare {branch} with main", $"Show reflog of {branch}", $"Hide {branch}", $"Solo {branch}", "—",
        "Copy branch name",
    ];

    /// <summary><c>local-work</c>, without upstream: the request item after its push.</summary>
    private static string[] LocalWorkMenu(string? item) =>
    [
        "local-work", "Check out local-work", "Merge local-work into main", "Merge local-work into main (create a merge commit)",
        "Rebase main onto local-work", "Interactive rebase of main onto local-work…", "—",
        "Push local-work…", .. Optional(item), "—",
        "Create branch here…", "Create tag here…", "Rename local-work…", "Delete local-work… (danger)", "—",
        "Set upstream…", "—",
        "Compare local-work with main", "Show reflog of local-work", "Hide local-work", "Solo local-work", "—",
        "Copy branch name",
    ];

    /// <summary>A remote branch's menu, HEAD on <c>main</c>: the request item in a group of its own after the delete group (7a's).</summary>
    private static string[] RemoteBranchMenu(string remote, string branch, string? item) =>
    [
        $"{remote}/{branch}", branch == "main" ? $"Check out {remote}/main (disabled: \"main is already checked out\")" : $"Check out {remote}/{branch}",
        $"Merge {remote}/{branch} into main", $"Merge {remote}/{branch} into main (create a merge commit)",
        $"Rebase main onto {remote}/{branch}", $"Interactive rebase of main onto {remote}/{branch}…", "—",
        "Create branch here…", "Create tag here…", "—",
        $"Delete {branch} on {remote}… (danger)", "—",
        .. item is null ? [] : new[] { item, "—" },
        $"Compare {remote}/{branch} with main", $"Hide {remote}/{branch}", $"Solo {remote}/{branch}", "—",
        "Copy branch name",
    ];

    /// <summary>A drop's menu: its first group, the rebase group, the request item's group between them and the compare (7a's).</summary>
    private static string[] DropMenu(string source, string target, string[] first, string? item) =>
    [
        $"{source} → {target}", .. first, "—",
        $"Check out {source} and rebase it onto {target}", $"Check out {source} and rebase it onto {target} interactively…", "—",
        .. item is null ? [] : new[] { item, "—" },
        $"Compare {source} with {target}",
    ];

    private static string[] Optional(string? item) => item is null ? [] : [item];

    private static RepositoryMenuContext Context(RepositoryHostingState? hosting, RepoRefs? refs = null)
    {
        refs ??= WebAppRefs();
        return new RepositoryMenuContext("web-app", "C:/web-app", refs, WorkingTreeStatus.Clean, [], ToolbarViewModel.StatesFor(refs, WorkingTreeStatus.Clean), IsOperationRunning: false)
        {
            Hosting = hosting,
        };
    }

    /// <summary>A menu's lines as 7a's <c>RepositoryMenusHostingTests</c> writes them.</summary>
    private static List<string> Lines(MenuModel menu) =>
    [
        .. menu.Entries.Select(entry => entry switch
        {
            MenuHeader header => header.Text,
            MenuSeparator => "—",
            MenuItem { IsEnabled: false, DisabledReason: { } reason } item => string.Create(CultureInfo.InvariantCulture, $"{item.Label} (disabled: \"{reason}\")"),
            MenuItem { IsEnabled: false } item => item.Label + " (disabled)",
            MenuItem { IsDestructive: true } item => item.Label + " (danger)",
            MenuItem item => item.Label,
            _ => "?",
        }),
    ];
}
