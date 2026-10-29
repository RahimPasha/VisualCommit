using System.Globalization;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.PullRequestTestData;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The left panel's "Pull requests" section as a view model (docs/phase-7a-design.md 10.3, 10.6;
/// D303, D304, D308): without a GitHub remote phase 1's placeholder; each state's rows; the rows'
/// texts; the filter; a click on a row; the selection's key across refreshes; a branch row's
/// <c>#n</c>; <see cref="LeftPanelViewModel.TargetOf"/>; and the rows a failed refresh keeps (design
/// 17 item 3).
/// </summary>
public sealed class PullRequestSectionTests
{
    [Fact]
    public void Without_a_GitHub_remote_the_section_is_phase_1_s_placeholder()
    {
        var panel = Panel();

        Assert.Contains("> Pull requests 0", Describe(panel));
        var header = Header(panel);
        Assert.False(header.CanToggle);
        Assert.False(header.ActivateCommand.CanExecute(null));
        Assert.Equal(RefRowTarget.None, LeftPanelViewModel.TargetOf(header));
        Assert.DoesNotContain(panel.Rows, row => row is PullRequestRow or PullRequestMessageRow);
        Assert.All(panel.Rows.OfType<RefItemRow>(), row => Assert.False(row.ShowsPullRequest));

        panel.UpdatePullRequests(RepositoryHostingState.NotGitHub);
        Assert.Contains("> Pull requests 0", Describe(panel));
    }

    [Fact]
    public void Signed_out_one_clickable_sign_in_row()
    {
        var panel = Panel(SignedOut());

        Assert.Equal(["v Pull requests 0", "  [SignIn] Sign in to GitHub to see pull requests…", "v Tags 0"], SectionLines(panel));
        var message = Messages(panel).Single();
        Assert.True(message.ActivateCommand.CanExecute(null));
        Assert.Equal("Sign in to GitHub to see pull requests…", message.ToolTip);
        Assert.Equal("pr-message:SignIn", message.Key);
        Assert.Equal(1, message.Depth);
        Assert.True(Header(panel).CanToggle);
        Assert.Equal(RefRowTarget.Section, LeftPanelViewModel.TargetOf(Header(panel)));
        Assert.Equal(RefRowTarget.None, LeftPanelViewModel.TargetOf(message));
    }

    [Fact]
    public void Loading_without_a_list_shows_its_message()
    {
        var panel = Panel(Loading());

        Assert.Equal(["v Pull requests 0", "  [Loading] Loading pull requests…", "v Tags 0"], SectionLines(panel));
        Assert.False(Messages(panel).Single().ActivateCommand.CanExecute(null));
    }

    [Fact]
    public void A_failure_in_describe_s_words_with_the_detail_in_its_tooltip()
    {
        var panel = Panel(Failed(Forbidden));

        Assert.Equal(["v Pull requests 0", "  [Failed] GitHub answered: Resource not accessible by personal access token.", "v Tags 0"], SectionLines(panel));
        var message = Messages(panel).Single();
        Assert.Equal("GitHub answered: Resource not accessible by personal access token.\npull_requests=read", message.ToolTip);
        Assert.False(message.ActivateCommand.CanExecute(null));

        var withoutDetail = Panel(Failed(new HostingFailure(HostingFailureKind.ServerError, "Server Error")));
        Assert.Equal("GitHub had a problem. Try again later.", Messages(withoutDetail).Single().ToolTip);
    }

    [Fact]
    public void None_open()
    {
        var panel = Panel(Loaded(new PullRequestList([], MoreExist: false)));

        Assert.Equal(["v Pull requests 0", "  [None] No open pull requests", "v Tags 0"], SectionLines(panel));
        Assert.All(panel.Rows.OfType<RefItemRow>(), row => Assert.False(row.ShowsPullRequest));
    }

    [Fact]
    public void Rows_newest_first_with_number_title_author_draft_and_head_label()
    {
        var panel = Panel(Loaded());

        Assert.Equal(
            ["v Pull requests 4", "  #5 Fix a typo in the greeting", "  #3 Shorten long labels", "  #2 Update the readme", "  #1 Add the login form", "v Tags 0"],
            SectionLines(panel));
        var rows = PullRequests(panel);
        Assert.Equal(["#5", "#3", "#2", "#1"], rows.Select(row => row.NumberText));
        Assert.Equal(["ada", "octo", "octo", "ada"], rows.Select(row => row.Author));
        Assert.Equal([false, false, true, false], rows.Select(row => row.IsDraft));
        Assert.Equal(["ada:fix-typo", LongBranch, "docs/readme", "feature/login"], rows.Select(row => row.HeadLabel));
        Assert.Equal("#5 Fix a typo in the greeting\nada · ada:fix-typo → main", rows[0].ToolTip);
        Assert.Equal("#2 Update the readme\nocto · docs/readme → main · Draft", rows[2].ToolTip);
        Assert.Equal("#1 Add the login form\nada · feature/login → main", rows[3].ToolTip);
        Assert.Equal("Draft", rows[2].DraftText);
        Assert.Equal("pr:1", rows[3].Key);
        Assert.Equal(1, rows[3].Depth);
        Assert.Equal(Sha('e'), rows[3].TargetSha);
        Assert.All(rows, row => Assert.Equal(RefRowTarget.PullRequest, LeftPanelViewModel.TargetOf(row)));
        Assert.Empty(Messages(panel));

        // A deleted fork's head is its branch alone; an author GitHub did not give is empty.
        var gone = Pull(8, "An old idea", null, "idea", 'i', null);
        var goneRow = PullRequests(Panel(Loaded(new PullRequestList([gone], false)))).Single();
        Assert.Equal("idea", goneRow.HeadLabel);
        Assert.Equal(string.Empty, goneRow.Author);
        Assert.Equal("#8 An old idea\nidea → main", goneRow.ToolTip);
    }

    [Fact]
    public void Three_hundred_rows_then_more()
    {
        var many = Enumerable.Range(1, 300).Reverse().Select(n => Pull(n, $"Work {n}", "octo", "topic/no-pr", 't', OctoHello)).ToList();
        var panel = Panel(Loaded(new PullRequestList(many, MoreExist: true)));

        Assert.Equal("300", Header(panel).CountText);
        Assert.Equal(300, PullRequests(panel).Count);
        var more = Messages(panel).Single();
        Assert.Equal(PullRequestMessageKind.More, more.Kind);
        Assert.Equal("More than 300 open pull requests", more.Text);
        Assert.Equal("Open pull requests in browser", more.ToolTip);
        Assert.True(more.ActivateCommand.CanExecute(null));
        var section = SectionLines(panel);
        Assert.Equal("  #1 Work 1", section[^3]);
        Assert.Equal("  [More] More than 300 open pull requests", section[^2]);
    }

    [Theory]
    [InlineData("#1", new[] { 1 })]
    [InlineData("1", new[] { 1 })]
    [InlineData("#2", new[] { 2 })]
    [InlineData("LOGIN", new[] { 1 })]
    [InlineData("ADA", new[] { 5, 1 })]
    [InlineData("ada:fix", new[] { 5 })]
    [InlineData("docs/readme", new[] { 2 })]
    [InlineData("nothing like it", new int[0])]
    public void The_filter_matches_number_title_author_and_head_label(string filter, int[] numbers)
    {
        var panel = Panel(Loaded());

        panel.FilterText = filter;

        Assert.Equal(numbers, PullRequests(panel).Select(row => row.Number));
        Assert.Equal(numbers.Length.ToString(CultureInfo.InvariantCulture), Header(panel).CountText);
    }

    [Fact]
    public void Message_rows_show_while_filtering()
    {
        var panel = Panel(SignedOut());

        panel.FilterText = "zzz";

        Assert.Equal(["v Pull requests 0", "  [SignIn] Sign in to GitHub to see pull requests…", "v Tags 0"], SectionLines(panel));
    }

    [Fact]
    public void A_click_selects_the_head_and_one_not_in_the_graph_selects_nothing()
    {
        var panel = Panel(Loaded());
        panel.IsInGraph = sha => sha != Five.HeadSha;
        var activated = new List<string>();
        var nothing = 0;
        panel.RefActivated += (_, sha) => activated.Add(sha);
        panel.NothingActivated += (_, _) => nothing++;

        Row(panel, 1).ActivateCommand.Execute(null);

        Assert.Equal([Sha('e')], activated);
        Assert.True(Row(panel, 1).IsSelected);
        Assert.Single(PullRequests(panel), row => row.IsSelected);

        Row(panel, 5).ActivateCommand.Execute(null);

        Assert.Equal([Sha('e')], activated);
        Assert.Equal(1, nothing);
        Assert.DoesNotContain(PullRequests(panel), row => row.IsSelected);
        Assert.DoesNotContain(panel.Rows.OfType<RefItemRow>(), row => row.IsSelected);

        // A message row does nothing in the panel: the view hands its click to the repository.
        var signedOut = Panel(SignedOut());
        Messages(signedOut).Single().ActivateCommand.Execute(null);
        Assert.Contains("  [SignIn] Sign in to GitHub to see pull requests…", SectionLines(signedOut));
    }

    [Fact]
    public void A_clicked_pull_request_stays_selected_across_updates_and_follows_the_graph()
    {
        var panel = Panel(Loaded());
        Row(panel, 1).ActivateCommand.Execute(null);

        panel.Update(HelloRefs());
        Assert.True(Row(panel, 1).IsSelected);
        panel.UpdatePullRequests(Loaded());
        Assert.True(Row(panel, 1).IsSelected);

        panel.SyncSelection(Sha('e'));
        Assert.True(Row(panel, 1).IsSelected);
        panel.SyncSelection(Sha('a'));
        Assert.False(Row(panel, 1).IsSelected);

        // Selected again, then no longer listed: the key goes, and a later list does not bring it back.
        Row(panel, 1).ActivateCommand.Execute(null);
        panel.UpdatePullRequests(Loaded(new PullRequestList([Five, Three, Two], false)));
        Assert.DoesNotContain(PullRequests(panel), row => row.IsSelected);
        panel.UpdatePullRequests(Loaded());
        Assert.False(Row(panel, 1).IsSelected);

        // A branch's click unmarks the pull request, and the other way round.
        Row(panel, 1).ActivateCommand.Execute(null);
        Branch(panel, "main").ActivateCommand.Execute(null);
        Assert.False(Row(panel, 1).IsSelected);
        Assert.True(Branch(panel, "main").IsSelected);
        Row(panel, 3).ActivateCommand.Execute(null);
        Assert.False(Branch(panel, "main").IsSelected);
        Assert.True(Row(panel, 3).IsSelected);
    }

    [Fact]
    public void A_branch_row_shows_its_pull_request_from_by_ref()
    {
        var panel = Panel(Loaded());
        OpenFolders(panel);

        var local = Item(panel, "refs/heads/feature/login");
        Assert.True(local.ShowsPullRequest);
        Assert.Equal("#1", local.PullRequestText);
        Assert.Equal("Pull request #1: Add the login form", local.PullRequestToolTip);
        Assert.Equal("#1", Item(panel, "refs/remotes/origin/feature/login").PullRequestText);
        Assert.Equal("#2", Item(panel, "refs/remotes/origin/docs/readme").PullRequestText);
        Assert.Equal("#3", Item(panel, "refs/remotes/origin/" + LongBranch).PullRequestText);
        foreach (var without in new[] { "refs/heads/main", "refs/heads/local-work", "refs/remotes/origin/main", "refs/remotes/origin/topic/no-pr" })
        {
            Assert.False(Item(panel, without).ShowsPullRequest, without);
            Assert.Equal(string.Empty, Item(panel, without).PullRequestText);
            Assert.Null(Item(panel, without).PullRequestToolTip);
        }

        Assert.All(panel.Rows.OfType<RefItemRow>().Where(row => row.IsContainer), row => Assert.False(row.ShowsPullRequest));

        // Signed out the numbers go.
        panel.UpdatePullRequests(SignedOut());
        Assert.False(Item(panel, "refs/heads/feature/login").ShowsPullRequest);
    }

    [Fact]
    public void A_failed_refresh_after_a_list_keeps_the_rows_under_the_failed_row()
    {
        var panel = Panel(Loaded());
        Row(panel, 1).ActivateCommand.Execute(null);

        panel.UpdatePullRequests(Failed(Forbidden));

        Assert.Equal(
            [
                "v Pull requests 4",
                "  [Failed] GitHub answered: Resource not accessible by personal access token.",
                "  #5 Fix a typo in the greeting",
                "  #3 Shorten long labels",
                "  #2 Update the readme",
                "  #1 Add the login form",
                "v Tags 0",
            ],
            SectionLines(panel));
        Assert.True(Row(panel, 1).IsSelected);

        // The filter counts the kept rows; the state's ByRef (empty) decides the branch rows.
        panel.FilterText = "ada";
        Assert.Equal("2", Header(panel).CountText);
        panel.FilterText = string.Empty;

        // A refresh running over the failure shows the kept rows and no loading row (no flicker).
        panel.UpdatePullRequests(Loading());
        Assert.Equal(["v Pull requests 4", "  #5 Fix a typo in the greeting", "  #3 Shorten long labels", "  #2 Update the readme", "  #1 Add the login form", "v Tags 0"], SectionLines(panel));

        // Signed out, the kept rows go; a later failure shows none.
        panel.UpdatePullRequests(SignedOut());
        panel.UpdatePullRequests(Failed(Forbidden));
        Assert.Equal(["v Pull requests 0", "  [Failed] GitHub answered: Resource not accessible by personal access token.", "v Tags 0"], SectionLines(panel));
    }

    [Fact]
    public void Another_repository_s_failure_does_not_keep_the_rows()
    {
        var panel = Panel(Loaded());

        panel.UpdatePullRequests(Failed(Forbidden, AcmeTool));

        Assert.Equal(["v Pull requests 0", "  [Failed] GitHub answered: Resource not accessible by personal access token.", "v Tags 0"], SectionLines(panel));

        // Without a GitHub remote they go too.
        var other = Panel(Loaded());
        other.UpdatePullRequests(RepositoryHostingState.NotGitHub);
        other.UpdatePullRequests(Failed(Forbidden));
        Assert.Empty(PullRequests(other));
    }

    [Fact]
    public void Loading_with_the_state_s_list_shows_its_rows_and_no_loading_row()
    {
        // 10.3's "Loading, List kept": the state itself holds the list while a refresh runs.
        var panel = Panel(Loading(HelloList));

        Assert.Equal(["v Pull requests 4", "  #5 Fix a typo in the greeting", "  #3 Shorten long labels", "  #2 Update the readme", "  #1 Add the login form", "v Tags 0"], SectionLines(panel));
        Assert.Empty(Messages(panel));
        panel.FilterText = "ada";
        Assert.Equal("2", Header(panel).CountText);
    }

    [Fact]
    public void Another_account_s_failure_does_not_keep_the_rows()
    {
        var octo = new AccountKey(HostingProvider.GitHub, "127.0.0.1:47395", "octo");
        var ada = new AccountKey(HostingProvider.GitHub, "127.0.0.1:47395", "ada");
        var panel = new LeftPanelViewModel();
        panel.Update(HelloRefs());
        panel.UpdatePullRequests(Loaded(), octo);

        // The same account's failure keeps the rows (17 item 3).
        panel.UpdatePullRequests(Failed(Forbidden), octo);
        Assert.Equal(4, PullRequests(panel).Count);

        // ada replaces octo: the hosting shows octo's list until ada's answer (7.8, D263), which
        // fails; octo's rows go (ruling 23).
        panel.UpdatePullRequests(Loaded(), octo);
        panel.UpdatePullRequests(Loaded(), ada);
        Assert.Equal(4, PullRequests(panel).Count);
        panel.UpdatePullRequests(Failed(Forbidden), ada);
        Assert.Equal(["v Pull requests 0", "  [Failed] GitHub answered: Resource not accessible by personal access token.", "v Tags 0"], SectionLines(panel));

        // ada's own list is kept for ada's next failure.
        panel.UpdatePullRequests(Loaded(new PullRequestList([.. HelloList.PullRequests], false)), ada);
        panel.UpdatePullRequests(Failed(Forbidden), ada);
        Assert.Equal(4, PullRequests(panel).Count);
    }

    [Fact]
    public void The_header_closes_and_keeps_its_count()
    {
        var panel = Panel(Loaded());

        Header(panel).ActivateCommand.Execute(null);

        Assert.Equal(["> Pull requests 4", "v Tags 0"], SectionLines(panel));
        Header(panel).ActivateCommand.Execute(null);
        Assert.Equal(4, PullRequests(panel).Count);
    }

    private static LeftPanelViewModel Panel(RepositoryHostingState? state = null)
    {
        var panel = new LeftPanelViewModel();
        panel.Update(HelloRefs());
        if (state is not null)
        {
            panel.UpdatePullRequests(state);
        }

        return panel;
    }

    private static RefSectionRow Header(LeftPanelViewModel panel) =>
        panel.Rows.OfType<RefSectionRow>().Single(row => row.Section == RefSection.PullRequests);

    private static List<PullRequestRow> PullRequests(LeftPanelViewModel panel) => [.. panel.Rows.OfType<PullRequestRow>()];

    private static List<PullRequestMessageRow> Messages(LeftPanelViewModel panel) => [.. panel.Rows.OfType<PullRequestMessageRow>()];

    private static PullRequestRow Row(LeftPanelViewModel panel, int number) => PullRequests(panel).Single(row => row.Number == number);

    private static RefItemRow Item(LeftPanelViewModel panel, string fullName) =>
        panel.Rows.OfType<RefItemRow>().Single(row => LeftPanelViewModel.RefNameOf(row) == fullName);

    private static RefItemRow Branch(LeftPanelViewModel panel, string name) => Item(panel, "refs/heads/" + name);

    /// <summary>Opens every closed folder of the branch trees, so that every branch has a row.</summary>
    private static void OpenFolders(LeftPanelViewModel panel)
    {
        while (panel.Rows.OfType<RefItemRow>().FirstOrDefault(row => row.IsContainer && !row.IsOpen) is { } closed)
        {
            closed.ActivateCommand.Execute(null);
        }
    }

    /// <summary>The section's lines, from its header to the next header: "v"/"&gt;" and the count; a message's kind in brackets.</summary>
    private static List<string> SectionLines(LeftPanelViewModel panel)
    {
        var lines = Describe(panel);
        var start = lines.FindIndex(line => line.EndsWith(" Pull requests " + Header(panel).CountText, StringComparison.Ordinal));
        return lines.Skip(start).Take(lines.FindIndex(start + 1, line => line[0] is 'v' or '>') - start + 1).ToList();
    }

    private static List<string> Describe(LeftPanelViewModel panel) => panel.Rows.Select(row => row switch
    {
        RefSectionRow section => $"{(section.IsOpen ? "v" : ">")} {section.Text} {section.CountText}",
        PullRequestRow pullRequest => new string(' ', 2 * pullRequest.Depth) + pullRequest.Text,
        PullRequestMessageRow message => new string(' ', 2 * message.Depth) + $"[{message.Kind}] {message.Text}",
        RefItemRow item => new string(' ', 2 * item.Depth) + item.Text,
        _ => throw new InvalidOperationException($"Unexpected row {row}."),
    }).ToList();
}
