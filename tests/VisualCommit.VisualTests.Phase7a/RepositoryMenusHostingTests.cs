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
using static VisualCommit.VisualTests.Phase7a.PullRequestTestData;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The pull requests' menus and items (docs/phase-7a-design.md 10.4, 10.6; D306, D307, D313): a
/// pull request's menu with both checkout labels and its actions; the section's menu signed in,
/// signed out, loading and without hosting; the branch and drop menus with and without hosting,
/// exact lines; the palette's items after the remotes.
/// </summary>
public sealed class RepositoryMenusHostingTests
{
    [Fact]
    public void A_pull_request_s_menu_with_its_checkout_labels_and_actions()
    {
        var (actions, recorder) = RecordingActions.Create();

        var one = RepositoryMenus.ForPullRequest(Context(Loaded()), 1, actions);
        Assert.Equal(["#1 Add the login form", "Open in browser", "Check out feature/login", "—", "Copy link"], Lines(one));
        Assert.Equal("#1 Add the login form", one.Entries.OfType<MenuHeader>().Single().ToolTip);
        Assert.Equal(["#5 Fix a typo in the greeting", "Open in browser", "Check out ada:fix-typo", "—", "Copy link"], Lines(RepositoryMenus.ForPullRequest(Context(Loaded()), 5, actions)));
        Assert.Equal(
            ["#1 Add the login form", "Open in browser", "Check out feature/login (disabled: \"feature/login is already checked out\")", "—", "Copy link"],
            Lines(RepositoryMenus.ForPullRequest(Context(Loaded(), HelloRefs(head: "feature/login")), 1, actions)));

        foreach (var item in one.Items)
        {
            item.Invoke();
        }

        Assert.Equal(["OpenPullRequestAsync", "CheckOutPullRequestAsync", "CopyPullRequestLinkAsync"], recorder.Calls.Select(call => call.Method));
        Assert.All(recorder.Calls, call => Assert.Same(One, call.Arguments[0]));
    }

    [Fact]
    public void A_pull_request_no_longer_listed_has_the_header_alone_unless_its_row_has_it()
    {
        var (actions, _) = RecordingActions.Create();

        Assert.Equal(["#9"], Lines(RepositoryMenus.ForPullRequest(Context(Loaded()), 9, actions)));
        Assert.Equal(["#9"], Lines(RepositoryMenus.ForPullRequest(Context(null), 9, actions)));

        // A failed refresh keeps the rows (design 17 item 3): the row's own pull request.
        Assert.Equal(
            ["#1 Add the login form", "Open in browser", "Check out feature/login", "—", "Copy link"],
            Lines(RepositoryMenus.ForPullRequest(Context(Failed(Forbidden)), 1, actions, One)));
    }

    [Fact]
    public void The_section_s_menu_signed_in_signed_out_loading_and_without_hosting()
    {
        var (actions, recorder) = RecordingActions.Create();

        Assert.Equal(["Pull requests", "Refresh pull requests", "Open pull requests in browser"], Lines(RepositoryMenus.ForSection(Context(Loaded()), RefSection.PullRequests, actions)!));
        Assert.Equal(["Pull requests", "Refresh pull requests", "Open pull requests in browser"], Lines(RepositoryMenus.ForSection(Context(Failed(Forbidden)), RefSection.PullRequests, actions)!));
        Assert.Equal(
            ["Pull requests", "Refresh pull requests (disabled: \"Pull requests are loading\")", "Open pull requests in browser"],
            Lines(RepositoryMenus.ForSection(Context(Loading()), RefSection.PullRequests, actions)!));
        var signedOut = RepositoryMenus.ForSection(Context(SignedOut()), RefSection.PullRequests, actions)!;
        Assert.Equal(["Pull requests", "Sign in to GitHub…", "Open pull requests in browser"], Lines(signedOut));
        Assert.Null(RepositoryMenus.ForSection(Context(null), RefSection.PullRequests, actions));

        foreach (var item in signedOut.Items)
        {
            item.Invoke();
        }

        RepositoryMenus.ForSection(Context(Loaded()), RefSection.PullRequests, actions)!.Items.First().Invoke();
        Assert.Equal(["SignInToGitHubAsync", "OpenPullRequestsInBrowserAsync", "RefreshPullRequestsAsync"], recorder.Calls.Select(call => call.Method));
    }

    [Fact]
    public void A_local_branch_s_menu_ends_its_push_group_with_the_pull_request_item()
    {
        var (actions, recorder) = RecordingActions.Create();
        var login = RepositoryMenus.ForLocalBranch(Context(Loaded()), "refs/heads/feature/login", actions);
        Assert.Equal(
            [
                "feature/login", "Check out feature/login", "Merge feature/login into main", "Merge feature/login into main (create a merge commit)",
                "Rebase main onto feature/login", "Interactive rebase of main onto feature/login…",
                "Fast-forward feature/login to origin/feature/login (disabled: \"feature/login is up to date with origin/feature/login\")", "—",
                "Push feature/login to origin/feature/login", "Force push feature/login… (danger)", "Open pull request #1", "—",
                "Create branch here…", "Create tag here…", "Rename feature/login…", "Delete feature/login… (danger)", "—",
                "Change upstream…", "Stop tracking origin/feature/login", "—",
                "Compare feature/login with main", "Show reflog of feature/login", "Hide feature/login", "Solo feature/login", "—",
                "Copy branch name",
            ],
            Lines(login));
        login.Items.Single(item => item.Label == "Open pull request #1").Invoke();
        Assert.Same(One, recorder.Calls.Single().Arguments[0]);

        var localWork = RepositoryMenus.ForLocalBranch(Context(Loaded()), "refs/heads/local-work", actions);
        Assert.Equal(["—", "Push local-work…", "Create pull request…", "—"], Lines(localWork).Skip(6).Take(4));
        localWork.Items.Single(item => item.Label == "Create pull request…").Invoke();
        Assert.Equal(("CreatePullRequestAsync", "refs/heads/local-work", (object?)null), (recorder.Calls[^1].Method, recorder.Calls[^1].Arguments[0], recorder.Calls[^1].Arguments[1]));

        // Signed out the item is there and enabled (the create signs in first).
        Assert.Contains("Create pull request…", Lines(RepositoryMenus.ForLocalBranch(Context(SignedOut()), "refs/heads/feature/login", actions)));

        // Without hosting: as before phase 7a.
        var plain = Lines(RepositoryMenus.ForLocalBranch(Context(null), "refs/heads/feature/login", actions));
        Assert.Equal(Lines(login).Where(line => line != "Open pull request #1"), plain);
    }

    [Fact]
    public void A_remote_branch_s_menu_has_the_item_in_a_group_after_its_delete_group()
    {
        var (actions, _) = RecordingActions.Create();
        string[] Expected(string branch, string item) =>
        [
            $"origin/{branch}", $"Check out origin/{branch}", $"Merge origin/{branch} into main", $"Merge origin/{branch} into main (create a merge commit)",
            $"Rebase main onto origin/{branch}", $"Interactive rebase of main onto origin/{branch}…", "—",
            "Create branch here…", "Create tag here…", "—",
            $"Delete {branch} on origin… (danger)", "—",
            item, "—",
            $"Compare origin/{branch} with main", $"Hide origin/{branch}", $"Solo origin/{branch}", "—",
            "Copy branch name",
        ];

        Assert.Equal(Expected("docs/readme", "Open pull request #2"), Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded()), "refs/remotes/origin/docs/readme", actions)));
        Assert.Equal(Expected("topic/no-pr", "Create pull request…"), Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded()), "refs/remotes/origin/topic/no-pr", actions)));
        Assert.Equal(
            Expected("topic/no-pr", "Create pull request…").Where((_, index) => index is not (12 or 13)),
            Lines(RepositoryMenus.ForRemoteBranch(Context(null), "refs/remotes/origin/topic/no-pr", actions)));

        // A remote GitHub does not have gets no item.
        var other = HelloRefs() with { Refs = [.. HelloRefs().Refs, Remote("mirror/main", 'a')], Remotes = ["mirror", "origin"] };
        Assert.DoesNotContain(Lines(RepositoryMenus.ForRemoteBranch(Context(Loaded(), other), "refs/remotes/mirror/main", actions)), line => line.Contains("pull request", StringComparison.Ordinal));
    }

    [Fact]
    public void A_drop_s_menus_have_the_item_between_rebase_and_compare()
    {
        var (actions, recorder) = RecordingActions.Create();

        Assert.Equal(
            [
                "feature/login → main", "Merge feature/login into main", "Merge feature/login into main (create a merge commit)", "Fast-forward main to feature/login", "—",
                "Check out feature/login and rebase it onto main", "Check out feature/login and rebase it onto main interactively…", "—",
                "Open pull request #1", "—",
                "Compare feature/login with main",
            ],
            Lines(RepositoryMenus.ForDrop(Context(Loaded()), "refs/heads/feature/login", "refs/heads/main", actions)).Select(StripReason));
        var ontoRemote = RepositoryMenus.ForDrop(Context(Loaded()), "refs/heads/local-work", "refs/remotes/origin/main", actions);
        Assert.Equal(
            [
                "local-work → origin/main", "Push local-work to origin/main", "Push local-work to origin/main and track it", "—",
                "Check out local-work and rebase it onto origin/main", "Check out local-work and rebase it onto origin/main interactively…", "—",
                "Create pull request…", "—",
                "Compare local-work with origin/main",
            ],
            Lines(ontoRemote).Select(StripReason));
        ontoRemote.Items.Single(item => item.Label == "Create pull request…").Invoke();
        Assert.Equal(("CreatePullRequestAsync", "refs/heads/local-work", (object?)"refs/remotes/origin/main"), (recorder.Calls[^1].Method, recorder.Calls[^1].Arguments[0], recorder.Calls[^1].Arguments[1]));

        // Onto another local branch that tracks one of the same name: the item too.
        Assert.Contains("Create pull request…", Lines(RepositoryMenus.ForDrop(Context(Loaded()), "refs/heads/local-work", "refs/heads/feature/login", actions)));

        // Onto a local branch whose upstream has another name, or one without upstream: none.
        var renamed = HelloRefs() with { Refs = [.. HelloRefs().Refs.Where(r => r.Name != "feature/login"), Local("feature/login", 'e', "origin/main")] };
        Assert.DoesNotContain("Create pull request…", Lines(RepositoryMenus.ForDrop(Context(Loaded(), renamed), "refs/heads/local-work", "refs/heads/feature/login", actions)));
        Assert.DoesNotContain("Create pull request…", Lines(RepositoryMenus.ForDrop(Context(Loaded()), "refs/heads/main", "refs/heads/local-work", actions)));

        // Without hosting, every drop's menu as before.
        foreach (var (source, target) in new[] { ("refs/heads/feature/login", "refs/heads/main"), ("refs/heads/local-work", "refs/remotes/origin/main"), ("refs/heads/local-work", "refs/heads/feature/login") })
        {
            var with = Lines(RepositoryMenus.ForDrop(Context(Loaded()), source, target, actions));
            var without = Lines(RepositoryMenus.ForDrop(Context(null), source, target, actions));
            Assert.DoesNotContain(without, line => line.Contains("pull request", StringComparison.Ordinal));
            Assert.Equal(with.Count - 2, without.Count);
        }
    }

    [Fact]
    public void The_palette_lists_the_pull_requests_after_the_remotes()
    {
        var (actions, _) = RecordingActions.Create();
        var keys = new KeyTexts(CommandKey.Ctrl);

        var items = AppCommands.RepositoryItems(Context(Loaded()), actions, keys, hasWorkingChanges: false, diff: null, page: null, commands: []);

        var details = items.Select(item => item.Detail).ToList();
        var lastRemote = details.LastIndexOf("origin");
        var pullRequests = items.Skip(lastRemote + 1).Take(12).Select(item => $"{item.Label} | {item.Detail}").ToList();
        Assert.Equal(
            [
                "Open in browser | #5 Fix a typo in the greeting", "Check out ada:fix-typo | #5 Fix a typo in the greeting", "Copy link | #5 Fix a typo in the greeting",
                "Open in browser | #3 Shorten long labels", $"Check out {LongBranch} | #3 Shorten long labels", "Copy link | #3 Shorten long labels",
                "Open in browser | #2 Update the readme", "Check out docs/readme | #2 Update the readme", "Copy link | #2 Update the readme",
                "Open in browser | #1 Add the login form", "Check out feature/login | #1 Add the login form", "Copy link | #1 Add the login form",
            ],
            pullRequests);
        Assert.DoesNotContain(AppCommands.RepositoryItems(Context(null), actions, keys, false, null, null, []), item => item.Detail?.StartsWith('#') == true);
    }

    private static RepositoryMenuContext Context(RepositoryHostingState? hosting, RepoRefs? refs = null)
    {
        refs ??= HelloRefs();
        refs = refs with { Refspecs = FetchRefspecs.Parse(refs.Remotes.Select(remote => (remote, $"+refs/heads/*:refs/remotes/{remote}/*"))) };
        return new RepositoryMenuContext("hello", "C:/hello", refs, WorkingTreeStatus.Clean, [], ToolbarViewModel.StatesFor(refs, WorkingTreeStatus.Clean), IsOperationRunning: false)
        {
            Hosting = hosting,
        };
    }

    private static List<string> Lines(MenuModel menu) => RepositoryMenusTestLines.Of(menu);

    /// <summary>A disabled item's line without its reason: what the drop read decides, which these contexts lack.</summary>
    private static string StripReason(string line) => line.IndexOf(" (disabled", StringComparison.Ordinal) is var at and >= 0 ? line[..at] : line;

    /// <summary>A menu's lines as the report writes them (as <c>RepositoryMenusTests.Lines</c>).</summary>
    private static class RepositoryMenusTestLines
    {
        public static List<string> Of(MenuModel menu) =>
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
}
