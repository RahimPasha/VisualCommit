using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.RequestTestData;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The left panel's requests section for each provider (docs/phase-7b-design.md 12.3, 12.7; D351,
/// D355, D356; ruling 24): each state's title and rows on GitLab and Azure DevOps, 7a's placeholder
/// without a hosted remote, <c>!21</c>, a draft and the fork labels, past 300, the filter on
/// <c>!21</c> and <c>21</c> but never <c>#21</c>, a branch row's <c>!21</c>, D356's row (its words for a
/// held account needing sign-in too, D366) and the section's open or closed state kept from GitHub's state to GitLab's.
/// </summary>
public sealed class RequestSectionProvidersTests
{
    public static TheoryData<string> Providers => ["GitLab", "AzureDevOps"];

    [Fact]
    public void Without_a_hosted_remote_the_section_is_7a_s_placeholder()
    {
        var panel = Panel(WebApp, null);

        Assert.Contains("> Pull requests 0", Describe(panel));
        Assert.False(Header(panel).CanToggle);
        Assert.Equal("section:PullRequests", Header(panel).Key);
        panel.UpdatePullRequests(RepositoryHostingState.NoHostedRemote);
        Assert.Contains("> Pull requests 0", Describe(panel));
        Assert.DoesNotContain(panel.Rows, row => row is PullRequestRow or PullRequestMessageRow);
    }

    [Fact]
    public void Each_state_s_title_and_message_on_gitlab()
    {
        Assert.Equal(["v Merge requests 0", "  [SignIn] Sign in to GitLab to see merge requests…"], SectionLines(Panel(WebApp, State(WebApp, PullRequestsState.SignedOut))));
        Assert.Equal(["v Merge requests 0", "  [Loading] Loading merge requests…"], SectionLines(Panel(WebApp, State(WebApp, PullRequestsState.Loading))));
        Assert.Equal(["v Merge requests 0", "  [None] No open merge requests"], SectionLines(Panel(WebApp, State(WebApp, PullRequestsState.Loaded, new PullRequestList([], false)))));

        var failed = Panel(WebApp, State(WebApp, PullRequestsState.Failed, failure: GitLabForbidden));
        var text = "GitLab answered: " + GitLabForbidden.Message;
        Assert.Equal(["v Merge requests 0", "  [Failed] " + text], SectionLines(failed));
        Assert.Equal(text + "\ninsufficient_granular_scope", Messages(failed).Single().ToolTip);
        Assert.True(Messages(Panel(WebApp, State(WebApp, PullRequestsState.SignedOut))).Single().CanActivate);
    }

    [Fact]
    public void Each_state_s_title_and_message_on_azure_devops()
    {
        Assert.Equal(["v Pull requests 0", "  [SignIn] Sign in to Azure DevOps to see pull requests…"], SectionLines(Panel(MobileApp, State(MobileApp, PullRequestsState.SignedOut))));
        Assert.Equal(["v Pull requests 0", "  [Loading] Loading pull requests…"], SectionLines(Panel(MobileApp, State(MobileApp, PullRequestsState.Loading))));
        Assert.Equal(["v Pull requests 0", "  [None] No open pull requests"], SectionLines(Panel(MobileApp, State(MobileApp, PullRequestsState.Loaded, new PullRequestList([], false)))));

        var failed = Panel(MobileApp, State(MobileApp, PullRequestsState.Failed, failure: AzureServerError));
        Assert.Equal(["v Pull requests 0", "  [Failed] Azure DevOps had a problem. Try again later."], SectionLines(failed));
        Assert.Equal("Azure DevOps had a problem. Try again later.", Messages(failed).Single().ToolTip);
    }

    [Fact]
    public void Gitlab_s_rows_with_their_numbers_draft_fork_label_and_tooltips()
    {
        var panel = Panel(WebApp, Loaded(WebApp));

        Assert.Equal(["v Merge requests 3", "  !23 Fix a typo in the footer", "  !22 Draft: Update the readme", "  !21 Add the search box"], SectionLines(panel));
        var rows = Rows(panel);
        Assert.Equal(["!23", "!22", "!21"], rows.Select(row => row.NumberText));
        Assert.Equal([false, true, false], rows.Select(row => row.IsDraft));
        Assert.Equal(["ada", "octo", "ada"], rows.Select(row => row.Author));
        Assert.Equal(["ada:fix-typo", "docs/readme", "feature/search"], rows.Select(row => row.HeadLabel));
        Assert.Equal(
            [
                "!23 Fix a typo in the footer\nada · ada:fix-typo → main",
                "!22 Draft: Update the readme\nocto · docs/readme → main · Draft",
                "!21 Add the search box\nada · feature/search → main",
            ],
            rows.Select(row => row.ToolTip));
        Assert.All(rows, row => Assert.Equal(HostingProvider.GitLab, row.Provider));
        Assert.Equal(RefRowTarget.PullRequest, LeftPanelViewModel.TargetOf(rows[0]));
    }

    [Fact]
    public void Azure_devops_rows_with_their_numbers_draft_fork_label_and_tooltips()
    {
        var panel = Panel(MobileApp, Loaded(MobileApp));

        Assert.Equal(["v Pull requests 3", "  !13 Fix a typo in the greeting", "  !12 Write the guide", "  !11 Add the sign-in page"], SectionLines(panel));
        Assert.Equal(
            [
                "!13 Fix a typo in the greeting\nAda Lovelace · mobile-app-fork:fix-typo → main",
                "!12 Write the guide\nOcto Cat · docs/guide → main · Draft",
                "!11 Add the sign-in page\nAda Lovelace · feature/sign-in → main",
            ],
            Rows(panel).Select(row => row.ToolTip));
    }

    [Fact]
    public void Fork_labels_by_provider()
    {
        Assert.Equal("ada:fix-typo", PullRequestRow.BranchLabelOf(AdaWebApp, "fix-typo", WebApp));
        Assert.Equal("northfield/team:fix-typo", PullRequestRow.BranchLabelOf(new HostedRepository(HostingProvider.GitLab, GitLabHost, "northfield/team", "web-app"), "fix-typo", WebApp));
        Assert.Equal("mobile-app-fork:fix-typo", PullRequestRow.BranchLabelOf(MobileAppFork, "fix-typo", MobileApp));
        var otherProject = new HostedRepository(HostingProvider.AzureDevOps, AzureHost, "Fernhill-Labs/Tools", "mobile-app-fork");
        Assert.Equal("Tools/mobile-app-fork:fix-typo", PullRequestRow.BranchLabelOf(otherProject, "fix-typo", MobileApp));
        Assert.Equal("octo:main", PullRequestRow.BranchLabelOf(OctoWebApp, "main", OctoWebApp));

        // A head without a repository shows its branch (7a's).
        var gone = Request(24, "Gone", "ada", "fix-typo", '7', null, WebApp);
        Assert.Equal("fix-typo", PullRequestRow.HeadLabelOf(gone));
    }

    [Fact]
    public void Past_300_the_gitlab_sentence_opens_the_list_in_the_browser()
    {
        var many = Enumerable.Range(22, 300).Reverse().Select(n => Request(n, $"More work {n}", "octo", "topic/no-request", '5', WebApp, WebApp)).ToList();
        var panel = Panel(WebApp, State(WebApp, PullRequestsState.Loaded, new PullRequestList(many, MoreExist: true)));

        var lines = SectionLines(panel);
        Assert.Equal("v Merge requests 300", lines[0]);
        Assert.Equal(302, lines.Count);
        Assert.Equal("  [More] More than 300 open merge requests", lines[^1]);
        var more = Messages(panel).Single();
        Assert.True(more.CanActivate);
        Assert.Equal("Open merge requests in browser", more.ToolTip);

        var azure = Panel(MobileApp, State(MobileApp, PullRequestsState.Loaded, new PullRequestList([PR13], MoreExist: true)));
        Assert.Equal("  [More] More than 300 open pull requests", SectionLines(azure)[^1]);
    }

    [Fact]
    public void The_filter_matches_the_provider_s_number_never_github_s()
    {
        var panel = Panel(WebApp, Loaded(WebApp));

        panel.FilterText = "!21";
        Assert.Equal(["v Merge requests 1", "  !21 Add the search box"], SectionLines(panel));
        panel.FilterText = "21";
        Assert.Equal(["v Merge requests 1", "  !21 Add the search box"], SectionLines(panel));
        panel.FilterText = "#21";
        Assert.Equal(["v Merge requests 0"], SectionLines(panel));
        panel.FilterText = "ada";
        Assert.Equal(["v Merge requests 2", "  !23 Fix a typo in the footer", "  !21 Add the search box"], SectionLines(panel));
        panel.FilterText = "mobile-app-fork";
        Assert.Equal(["v Merge requests 0"], SectionLines(panel));

        var azure = Panel(MobileApp, Loaded(MobileApp));
        azure.FilterText = "mobile-app-fork";
        Assert.Equal(["v Pull requests 1", "  !13 Fix a typo in the greeting"], SectionLines(azure));
        azure.FilterText = "#11";
        Assert.Equal(["v Pull requests 0"], SectionLines(azure));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void A_branch_row_shows_the_provider_s_number_and_words(string name)
    {
        var repository = name == "GitLab" ? WebApp : MobileApp;
        var panel = Panel(repository, Loaded(repository));
        OpenFolders(panel);

        var (branch, draftRef, number, draft, words) = name == "GitLab"
            ? ("feature/search", "refs/remotes/origin/docs/readme", "!21", "!22", "Merge request !21: Add the search box")
            : ("feature/sign-in", "refs/remotes/origin/docs/guide", "!11", "!12", "Pull request !11: Add the sign-in page");
        var local = Item(panel, "refs/heads/" + branch);
        Assert.Equal(number, local.PullRequestText);
        Assert.Equal(words, local.PullRequestToolTip);
        Assert.Equal(number, Item(panel, "refs/remotes/origin/" + branch).PullRequestText);
        Assert.Equal(draft, Item(panel, draftRef).PullRequestText);
        Assert.False(Item(panel, "refs/heads/main").ShowsPullRequest);
        Assert.False(Item(panel, "refs/remotes/origin/topic/no-request").ShowsPullRequest);
        if (name == "GitLab")
        {
            // ByRef is the listed provider's (9.7): GitHub's upstream shows none.
            Assert.False(Item(panel, "refs/remotes/upstream/main").ShowsPullRequest);
        }
    }

    [Fact]
    public void D356_s_row_names_both_organizations_and_takes_no_click()
    {
        var account = new AccountKey(HostingProvider.AzureDevOps, AzureHost, "Fernhill-Labs");
        var panel = Panel(MobileApp, Loaded(MobileApp), refs: MobileAppRefs());
        panel.UpdatePullRequests(Loaded(MobileApp), account, "Fernhill-Labs");
        Assert.Equal(3, Rows(panel).Count);

        panel.UpdatePullRequests(OtherOrganization(), account, "Fernhill-Labs");

        Assert.Equal(["v Pull requests 0", "  [OtherOrganization] In harbor-co; signed in to Fernhill-Labs"], SectionLines(panel));
        var row = Messages(panel).Single();
        Assert.False(row.CanActivate);
        Assert.False(row.ActivateCommand.CanExecute(null));
        Assert.Equal(RefRowTarget.None, LeftPanelViewModel.TargetOf(row));
        Assert.Equal(
            "This repository is in the Azure DevOps organization harbor-co; the signed-in account is in Fernhill-Labs, so nothing is asked of Azure DevOps for it. To see its pull requests, sign out and sign in to harbor-co in Settings, Accounts.",
            row.ToolTip);
        Assert.True(Header(panel).CanToggle);
        Assert.Equal(RefRowTarget.Section, LeftPanelViewModel.TargetOf(Header(panel)));
        Assert.All(panel.Rows.OfType<RefItemRow>(), item => Assert.False(item.ShowsPullRequest));

        // It toggles.
        Header(panel).ActivateCommand.Execute(null);
        Assert.Equal(["> Pull requests 0"], SectionLines(panel));
        Header(panel).ActivateCommand.Execute(null);

        // Without the login the key's, lower-cased.
        panel.UpdatePullRequests(OtherOrganization(), account);
        Assert.Equal("In harbor-co; signed in to fernhill-labs", Messages(panel).Single().Text);
    }

    [Theory]
    [InlineData(AccountNoteKind.Refused)]
    [InlineData(AccountNoteKind.SessionEnded)]
    public void D356_s_row_for_a_held_account_that_needs_sign_in_never_says_signed_in(AccountNoteKind note)
    {
        // D366 keeps the state for an account needing sign-in (refused, or kept for the last session
        // only); the row and its tooltip then say so (the review before the gate, ui-1), as
        // RepositoryViewModel passes the held account's state.
        var account = new HostingAccount(new AccountKey(HostingProvider.AzureDevOps, AzureHost, "Fernhill-Labs"), "Fernhill-Labs", 0, "Fernhill-Labs", null, true);
        var held = new AccountStatus(account, AccountState.NeedsSignIn, null, note, null);
        var panel = Panel(MobileApp, Loaded(MobileApp), refs: MobileAppRefs());

        panel.UpdatePullRequests(OtherOrganization(), held.Account.Key, held.Account.Login, held.State);

        Assert.Equal(["v Pull requests 0", "  [OtherOrganization] In harbor-co; the account for Fernhill-Labs needs sign-in"], SectionLines(panel));
        var row = Messages(panel).Single();
        Assert.False(row.CanActivate);
        Assert.False(row.ActivateCommand.CanExecute(null));
        Assert.Equal(RefRowTarget.None, LeftPanelViewModel.TargetOf(row));
        Assert.Equal(
            "This repository is in the Azure DevOps organization harbor-co; the account VisualCommit holds is in Fernhill-Labs and needs signing in again, so nothing is asked of Azure DevOps for it. To see its pull requests, sign out and sign in to harbor-co in Settings, Accounts.",
            row.ToolTip);
        Assert.Empty(Rows(panel));
        Assert.All(panel.Rows.OfType<RefItemRow>(), item => Assert.False(item.ShowsPullRequest));

        // Signed in again: D356's signed-in texts, word for word; and back.
        panel.UpdatePullRequests(OtherOrganization(), held.Account.Key, held.Account.Login, AccountState.SignedIn);
        Assert.Equal(["v Pull requests 0", "  [OtherOrganization] In harbor-co; signed in to Fernhill-Labs"], SectionLines(panel));
        Assert.Equal(
            "This repository is in the Azure DevOps organization harbor-co; the signed-in account is in Fernhill-Labs, so nothing is asked of Azure DevOps for it. To see its pull requests, sign out and sign in to harbor-co in Settings, Accounts.",
            Messages(panel).Single().ToolTip);
        panel.UpdatePullRequests(OtherOrganization(), held.Account.Key, held.Account.Login, held.State);
        Assert.Equal("In harbor-co; the account for Fernhill-Labs needs sign-in", Messages(panel).Single().Text);

        // Without the login the key's, lower-cased.
        panel.UpdatePullRequests(OtherOrganization(), held.Account.Key, accountState: held.State);
        Assert.Equal("In harbor-co; the account for fernhill-labs needs sign-in", Messages(panel).Single().Text);
    }

    [Fact]
    public void D356_s_state_lets_a_kept_list_of_the_same_repository_and_account_go()
    {
        // A list kept for manifest itself, with the same account (and with none, as 7a's callers pass):
        // only D356's state, as NoHostedRemote's, lets it go (12.3), so a failure afterwards shows no rows.
        var account = new AccountKey(HostingProvider.AzureDevOps, AzureHost, "Fernhill-Labs");
        foreach (var key in new[] { account, null })
        {
            var panel = Panel(Manifest, null, refs: MobileAppRefs());
            panel.UpdatePullRequests(State(Manifest, PullRequestsState.Loaded, new PullRequestList([PR11], false)), key, "Fernhill-Labs");
            Assert.Single(Rows(panel));

            panel.UpdatePullRequests(OtherOrganization(), key, "Fernhill-Labs");
            Assert.Empty(Rows(panel));

            panel.UpdatePullRequests(State(Manifest, PullRequestsState.Failed, failure: AzureServerError), key, "Fernhill-Labs");
            Assert.Empty(Rows(panel));
            Assert.Equal(PullRequestMessageKind.Failed, Messages(panel).Single().Kind);
        }
    }

    [Fact]
    public void Open_or_closed_is_kept_from_github_s_state_to_gitlab_s()
    {
        var github = new RepositoryHostingState(PullRequestsState.Loaded, WebAppRemotes, OctoWebApp, new PullRequestList([], false), null, NoRequests);
        var panel = Panel(WebApp, github);
        Assert.Equal(["v Pull requests 0", "  [None] No open pull requests"], SectionLines(panel));

        Header(panel).ActivateCommand.Execute(null);
        panel.UpdatePullRequests(Loaded(WebApp));
        Assert.Equal(["> Merge requests 3"], SectionLines(panel));

        Header(panel).ActivateCommand.Execute(null);
        panel.UpdatePullRequests(github);
        Assert.Equal(["v Pull requests 0", "  [None] No open pull requests"], SectionLines(panel));
    }

    private static LeftPanelViewModel Panel(HostedRepository repository, RepositoryHostingState? state, Core.Git.RepoRefs? refs = null)
    {
        var panel = new LeftPanelViewModel();
        panel.Update(refs ?? RefsOf(repository));
        if (state is not null)
        {
            panel.UpdatePullRequests(state);
        }

        return panel;
    }

    private static RefSectionRow Header(LeftPanelViewModel panel) =>
        panel.Rows.OfType<RefSectionRow>().Single(row => row.Section == RefSection.PullRequests);

    private static List<PullRequestRow> Rows(LeftPanelViewModel panel) => [.. panel.Rows.OfType<PullRequestRow>()];

    private static List<PullRequestMessageRow> Messages(LeftPanelViewModel panel) => [.. panel.Rows.OfType<PullRequestMessageRow>()];

    private static RefItemRow Item(LeftPanelViewModel panel, string fullName) =>
        panel.Rows.OfType<RefItemRow>().Single(row => LeftPanelViewModel.RefNameOf(row) == fullName);

    private static void OpenFolders(LeftPanelViewModel panel)
    {
        while (panel.Rows.OfType<RefItemRow>().FirstOrDefault(row => row.IsContainer && !row.IsOpen) is { } closed)
        {
            closed.ActivateCommand.Execute(null);
        }
    }

    /// <summary>The section's lines, from its header to the next header (excluded).</summary>
    private static List<string> SectionLines(LeftPanelViewModel panel)
    {
        var rows = panel.Rows.ToList();
        var start = rows.IndexOf(Header(panel));
        var end = rows.FindIndex(start + 1, row => row is RefSectionRow);
        return Describe(panel).Skip(start).Take((end < 0 ? rows.Count : end) - start).ToList();
    }

    private static List<string> Describe(LeftPanelViewModel panel) => panel.Rows.Select(row => row switch
    {
        RefSectionRow section => $"{(section.IsOpen ? "v" : ">")} {section.Text} {section.CountText}",
        PullRequestRow request => new string(' ', 2 * request.Depth) + request.Text,
        PullRequestMessageRow message => new string(' ', 2 * message.Depth) + $"[{message.Kind}] {message.Text}",
        RefItemRow item => new string(' ', 2 * item.Depth) + item.Text,
        _ => throw new InvalidOperationException($"Unexpected row {row}."),
    }).ToList();
}
