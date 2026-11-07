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

        var search = Lines(RepositoryMenus.ForLocalBranch(Context(Loaded(WebApp)), "refs/heads/feature/search", actions));
        Assert.Equal(["Push feature/search to origin/feature/search", "Force push feature/search… (danger)", "Open merge request !21", "—"], search.Skip(8).Take(4));
        Assert.Contains("Create merge request…", Lines(RepositoryMenus.ForLocalBranch(Context(Loaded(WebApp)), "refs/heads/local-work", actions)));
        Assert.Contains("Open merge request !22", Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded(WebApp)), "refs/remotes/origin/docs/readme", actions)));
        Assert.Contains("Create merge request…", Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded(WebApp)), "refs/remotes/origin/topic/no-request", actions)));
        Assert.Contains("Create merge request…", Lines(RepositoryMenus.ForLocalBranch(Context(State(WebApp, PullRequestsState.SignedOut)), "refs/heads/local-work", actions)));

        // upstream is GitHub's, no key of RequestRemotes (ruling 5, 12.4): no request item.
        var upstream = Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded(WebApp)), "refs/remotes/upstream/main", actions));
        Assert.Equal("upstream/main", upstream[0]);
        Assert.DoesNotContain(upstream, line => line.Contains("request", StringComparison.Ordinal));
        Assert.Equal(Lines(RepositoryMenus.ForRemoteBranch(Context(null), "refs/remotes/upstream/main", actions)), upstream);

        Assert.Contains("Open pull request !11", Lines(RepositoryMenus.ForLocalBranch(Context(Loaded(MobileApp), MobileAppRefs()), "refs/heads/feature/sign-in", actions)));
        Assert.Contains("Create pull request…", Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded(MobileApp), MobileAppRefs()), "refs/remotes/origin/topic/no-request", actions)));

        RepositoryMenus.ForLocalBranch(Context(Loaded(WebApp)), "refs/heads/feature/search", actions).Items.Single(item => item.Label == "Open merge request !21").Invoke();
        Assert.Same(MR21, recorder.Calls.Single().Arguments[0]);
    }

    [Fact]
    public void Drops_onto_a_request_base_only()
    {
        var (actions, _) = RecordingActions.Create();
        List<string> Drop(string source, string target, RepositoryHostingState? state = null) =>
            Lines(RepositoryMenus.ForDrop(Context(state ?? Loaded(WebApp)), source, target, actions));

        Assert.Contains("Create merge request…", Drop("refs/heads/local-work", "refs/remotes/origin/main"));
        Assert.Contains("Create merge request…", Drop("refs/heads/local-work", "refs/heads/main"));
        Assert.Contains("Open merge request !21", Drop("refs/heads/feature/search", "refs/heads/main"));
        Assert.DoesNotContain(Drop("refs/heads/local-work", "refs/remotes/upstream/main"), line => line.Contains("request", StringComparison.Ordinal));
        Assert.Equal(Lines(RepositoryMenus.ForDrop(Context(null), "refs/heads/local-work", "refs/remotes/upstream/main", actions)), Drop("refs/heads/local-work", "refs/remotes/upstream/main"));
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
            Assert.DoesNotContain(lines, line => line.Contains("request", StringComparison.Ordinal));
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
