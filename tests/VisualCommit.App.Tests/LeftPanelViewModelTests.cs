using System.Globalization;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The left panel's view model (no UI): sections, folders, counts, the filter, what stays open
/// across a refresh and what a click does, against "Left panel, with a repository open" and
/// checks 5, 6 and 7 of docs/test-reports/phase-1.md.
/// </summary>
public class LeftPanelViewModelTests
{
    /// <summary>The graph scenario's left panel, as the report draws it.</summary>
    private static readonly string[] GraphScenarioTree =
    [
        "v Local branches 4",
        "  > bugfix",
        "  > feature",
        "  main +1 -1 (head)",
        "v Remotes 2",
        "  v origin",
        "    > feature",
        "    main",
        "> Pull requests 0",
        "v Tags 2",
        "  v0.1",
        "  v0.2",
        "v Stashes 1",
        "  On main: Work in progress on README",
    ];

    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    [Fact]
    public void A_panel_that_was_never_updated_shows_five_closed_sections_with_the_count_0()
    {
        var panel = new LeftPanelViewModel();

        Assert.Equal(
            ["> Local branches 0", "> Remotes 0", "> Pull requests 0", "> Tags 0", "> Stashes 0"],
            Describe(panel));

        // Nothing opens: there is nothing in them.
        Assert.All(panel.Rows, row => Assert.False(row.ActivateCommand.CanExecute(null)));
        panel.Activate(panel.Rows[0]);
        panel.FilterText = "feature";
        Assert.Equal("> Local branches 0", Describe(panel)[0]);
        Assert.Equal(5, panel.Rows.Count);
    }

    [Fact]
    public void The_graph_scenario_shows_its_sections_folders_counts_and_head_branch()
    {
        var panel = Updated(GraphRefs());

        Assert.Equal(GraphScenarioTree, Describe(panel));
        Assert.Equal([4, 2, 0, 2, 1], panel.Rows.OfType<RefSectionRow>().Select(section => section.Count));
        var main = Item(panel, "main", RefItemKind.LocalBranch);
        Assert.True(main.IsHead);
        Assert.True(main.ShowsAhead);
        Assert.True(main.ShowsBehind);
        Assert.Equal(("1", "1"), (main.AheadText, main.BehindText));
        Assert.Equal("main", main.ToolTip);
    }

    [Fact]
    public void Rows_are_indented_14_per_level()
    {
        var panel = Updated(GraphRefs());

        Assert.Equal(10, panel.Rows[0].Indent);
        Assert.Equal(24, Item(panel, "bugfix", RefItemKind.Folder).Indent);
        Assert.Equal(38, Item(panel, "feature", RefItemKind.Folder, depth: 2).Indent);
    }

    [Fact]
    public async Task The_graph_scenarios_real_refs_show_as_the_report_draws_them()
    {
        // The same panel from what the git layer reads, not from refs built by hand.
        using var repo = await Scenarios.GraphAsync();
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var refs = await repository.ReadRefsAsync(TestContext.Current.CancellationToken);

        var panel = Updated(refs);

        Assert.Equal(GraphScenarioTree, Describe(panel));
    }

    [Fact]
    public void Opening_a_folder_shows_its_branches_with_their_own_ahead_and_behind_counts()
    {
        var panel = Updated(GraphRefs());

        panel.Activate(Item(panel, "feature", RefItemKind.Folder, depth: 1));

        Assert.Equal(
            ["v Local branches 4", "  > bugfix", "  v feature", "    login", "    search +1", "  main +1 -1 (head)"],
            Describe(panel).Take(6));
        var search = Item(panel, "search", RefItemKind.LocalBranch);
        Assert.True(search.ShowsAhead);
        Assert.False(search.ShowsBehind);
        Assert.False(Item(panel, "login", RefItemKind.LocalBranch).ShowsAheadBehind);
        Assert.Equal("feature/search", search.ToolTip);
    }

    [Fact]
    public void Closing_a_section_keeps_its_count_and_moves_the_next_section_up()
    {
        var panel = Updated(GraphRefs());

        panel.Activate(Section(panel, RefSection.Tags));

        Assert.Equal(["> Tags 2", "v Stashes 1", "  On main: Work in progress on README"], Describe(panel).TakeLast(3));

        panel.Activate(Section(panel, RefSection.Tags));

        Assert.Equal(GraphScenarioTree, Describe(panel));
    }

    [Fact]
    public void Pull_requests_stay_closed()
    {
        var panel = Updated(GraphRefs());
        var pullRequests = Section(panel, RefSection.PullRequests);

        Assert.False(pullRequests.ActivateCommand.CanExecute(null));
        panel.Activate(pullRequests);

        Assert.False(Section(panel, RefSection.PullRequests).IsOpen);
    }

    [Fact]
    public void What_the_user_opened_and_closed_survives_an_update()
    {
        var panel = Updated(GraphRefs());
        panel.Activate(Item(panel, "feature", RefItemKind.Folder, depth: 1));
        panel.Activate(Item(panel, "origin", RefItemKind.Remote));
        panel.Activate(Section(panel, RefSection.Tags));

        // A refresh after an outside change: a new tag and a new branch in the open folder.
        var refs = GraphRefs();
        panel.Update(refs with
        {
            Refs = [.. refs.Refs, Local("feature/zoom", 'z'), Tag("v0.3", 'y')],
        });

        Assert.Equal(
            [
                "v Local branches 5",
                "  > bugfix",
                "  v feature",
                "    login",
                "    search +1",
                "    zoom",
                "  main +1 -1 (head)",
                "v Remotes 2",
                "  > origin",
                "> Pull requests 0",
                "> Tags 3",
                "v Stashes 1",
                "  On main: Work in progress on README",
            ],
            Describe(panel));
    }

    [Fact]
    public void A_folder_that_holds_the_head_branch_starts_open()
    {
        var panel = Updated(GraphRefs(head: "feature/login"));

        Assert.Equal(
            ["v Local branches 4", "  > bugfix", "  v feature", "    login (head)", "    search +1", "  main +1 -1"],
            Describe(panel).Take(6));
    }

    [Fact]
    public void Folders_come_before_items_each_sorted_by_name_ignoring_case()
    {
        var refs = new RepoRefs(
            new HeadState("zebra", Sha('1')),
            [Local("zebra", '1', isHead: true), Local("apple", '2'), Local("middle/one", '3'), Local("Alpha", '4'), Local("Kilo/two", '5'), Local("beta", '6')],
            [],
            []);

        var panel = Updated(refs);

        Assert.Equal(
            ["v Local branches 6", "  > Kilo", "  > middle", "  Alpha", "  apple", "  beta", "  zebra (head)"],
            Describe(panel).Take(7));
    }

    [Fact]
    public void The_filter_keeps_matching_items_and_opens_the_folders_that_hold_them()
    {
        var panel = Updated(GraphRefs());

        panel.FilterText = "sea";

        Assert.Equal(
            [
                "v Local branches 1",
                "  v feature",
                "    search +1",
                "v Remotes 1",
                "  v origin",
                "    v feature",
                "      search",
                "> Pull requests 0",
                "v Tags 0",
                "v Stashes 0",
            ],
            Describe(panel));
        Assert.Equal([1, 1, 0, 0, 0], panel.Rows.OfType<RefSectionRow>().Select(section => section.Count));
    }

    [Fact]
    public void The_filter_ignores_case_and_matches_full_names_and_stash_messages()
    {
        var panel = Updated(GraphRefs());

        panel.FilterText = "ORIGIN/MA";
        Assert.Equal(["v Remotes 1", "  v origin", "    main"], Describe(panel).Skip(1).Take(3));
        Assert.Equal(0, Section(panel, RefSection.LocalBranches).Count);

        panel.FilterText = "progress";
        Assert.Equal(["v Stashes 1", "  On main: Work in progress on README"], Describe(panel).TakeLast(2));

        panel.FilterText = "V0.";
        Assert.Equal(2, Section(panel, RefSection.Tags).Count);
    }

    [Fact]
    public void Clearing_the_filter_brings_back_the_panel_as_it_was()
    {
        var panel = Updated(GraphRefs());
        panel.FilterText = "sea";

        // Closing a folder while filtering lasts only as long as that filter.
        panel.Activate(Item(panel, "feature", RefItemKind.Folder, depth: 1));
        Assert.Equal(["v Local branches 1", "  > feature"], Describe(panel).Take(2));

        panel.FilterText = string.Empty;

        Assert.Equal(GraphScenarioTree, Describe(panel));
    }

    [Fact]
    public void Clicking_a_ref_raises_RefActivated_with_its_commit_and_selects_it()
    {
        var refs = GraphRefs();
        var panel = Updated(refs);
        var activated = new List<string>();
        panel.RefActivated += (_, sha) => activated.Add(sha);

        panel.Activate(Item(panel, "v0.1", RefItemKind.Tag));
        Assert.Equal([Sha('b')], activated);
        Assert.Equal(["v0.1"], SelectedTexts(panel));

        panel.Activate(Item(panel, "On main: Work in progress on README", RefItemKind.Stash));
        Assert.Equal([Sha('b'), Sha('s')], activated);
        Assert.Equal(["On main: Work in progress on README"], SelectedTexts(panel));

        panel.Activate(Item(panel, "main", RefItemKind.RemoteBranch));
        Assert.Equal(Sha('r'), activated[^1]);
        Assert.Equal(["main"], SelectedTexts(panel));
        Assert.Equal(RefItemKind.RemoteBranch, panel.Rows.OfType<RefItemRow>().Single(row => row.IsSelected).Kind);
    }

    [Fact]
    public void Clicking_a_folder_or_section_raises_nothing_and_keeps_the_selection()
    {
        var panel = Updated(GraphRefs());
        var activated = new List<string>();
        panel.RefActivated += (_, sha) => activated.Add(sha);
        panel.Activate(Item(panel, "v0.2", RefItemKind.Tag));

        panel.Activate(Item(panel, "bugfix", RefItemKind.Folder));
        panel.Activate(Section(panel, RefSection.Remotes));

        Assert.Equal([Sha('c')], activated);
        Assert.Equal(["v0.2"], SelectedTexts(panel));
    }

    [Fact]
    public void The_selection_survives_an_update_and_goes_when_its_ref_is_gone()
    {
        var refs = GraphRefs();
        var panel = Updated(refs);
        panel.Activate(Item(panel, "v0.2", RefItemKind.Tag));

        panel.Update(refs);
        Assert.Equal(["v0.2"], SelectedTexts(panel));

        panel.Update(refs with { Refs = [.. refs.Refs.Where(r => r.Name != "v0.2")] });
        Assert.Empty(SelectedTexts(panel));

        // Coming back later does not select it again.
        panel.Update(refs);
        Assert.Empty(SelectedTexts(panel));
    }

    [Fact]
    public void Remotes_with_a_slash_in_their_name_and_remotes_without_branches_have_their_own_nodes()
    {
        var refs = new RepoRefs(
            new HeadState("main", Sha('1')),
            [
                Local("main", '1', isHead: true),
                Remote("team/fork", "team/fork/main", '2'),
                Remote("team/fork", "team/fork/topic/x", '3'),
                Remote("origin", "origin/main", '4'),
            ],
            [],
            ["empty", "origin", "team/fork"]);

        var panel = Updated(refs);

        Assert.Equal(
            ["v Remotes 3", "  v empty", "  v origin", "    main", "  v team/fork", "    > topic", "    main"],
            Describe(panel).Skip(2).Take(7));
        Assert.Equal("team/fork/main", panel.Rows.OfType<RefItemRow>().Last(row => row.Text == "main").ToolTip);
    }

    [Fact]
    public void An_empty_repository_shows_open_sections_with_the_count_0()
    {
        var panel = Updated(new RepoRefs(new HeadState("main", null), [], [], []));

        Assert.Equal(
            ["v Local branches 0", "v Remotes 0", "> Pull requests 0", "v Tags 0", "v Stashes 0"],
            Describe(panel));
    }

    private static LeftPanelViewModel Updated(RepoRefs refs)
    {
        var panel = new LeftPanelViewModel();
        panel.Update(refs);
        return panel;
    }

    /// <summary>
    /// The graph scenario's refs, built by hand: what <see cref="GitRepository.ReadRefsAsync"/>
    /// reads from it, with made-up commit ids (one letter repeated) that the tests can name.
    /// </summary>
    internal static RepoRefs GraphRefs(string head = "main") => new(
        new HeadState(head, Sha('a')),
        [
            Local("bugfix/crash-on-start", 'd', isHead: head == "bugfix/crash-on-start"),
            Local("feature/login", 'e', isHead: head == "feature/login"),
            Local("feature/search", 'f', upstream: "origin/feature/search", ahead: 1, isHead: head == "feature/search"),
            Local("main", 'a', upstream: "origin/main", ahead: 1, behind: 1, isHead: head == "main"),
            Remote("origin", "origin/feature/search", 'g'),
            Remote("origin", "origin/main", 'r'),
            Tag("v0.1", 'b'),
            Tag("v0.2", 'c'),
        ],
        [new StashEntry(0, Sha('s'), Sha('a'), "On main: Work in progress on README", "Test Author", "author@example.com", default, default)],
        ["origin"]);

    internal static string Sha(char letter) => new(letter, 40);

    private static GitRef Local(string name, char sha, string? upstream = null, int ahead = 0, int behind = 0, bool isHead = false) =>
        new("refs/heads/" + name, name, RefKind.LocalBranch, Sha(sha), Upstream: upstream, Ahead: ahead, Behind: behind, IsHead: isHead);

    private static GitRef Remote(string remote, string name, char sha) =>
        new("refs/remotes/" + name, name, RefKind.RemoteBranch, Sha(sha), RemoteName: remote);

    private static GitRef Tag(string name, char sha) => new("refs/tags/" + name, name, RefKind.Tag, Sha(sha));

    private static RefSectionRow Section(LeftPanelViewModel panel, RefSection section) =>
        panel.Rows.OfType<RefSectionRow>().Single(row => row.Section == section);

    private static RefItemRow Item(LeftPanelViewModel panel, string text, RefItemKind kind, int? depth = null) =>
        panel.Rows.OfType<RefItemRow>().First(row => row.Text == text && row.Kind == kind && (depth is null || row.Depth == depth));

    private static List<string> SelectedTexts(LeftPanelViewModel panel) =>
        panel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Text).ToList();

    /// <summary>The rows as the report draws the tree: indentation, "v" open, "&gt;" closed, counts.</summary>
    private static List<string> Describe(LeftPanelViewModel panel) => panel.Rows.Select(row => row switch
    {
        RefSectionRow section => $"{(section.IsOpen ? "v" : ">")} {section.Text} {section.CountText}",
        RefItemRow item => new string(' ', 2 * item.Depth)
            + (item.IsContainer ? (item.IsOpen ? "v " : "> ") : string.Empty)
            + item.Text
            + (item.ShowsAhead ? string.Create(CultureInfo.InvariantCulture, $" +{item.Ahead}") : string.Empty)
            + (item.ShowsBehind ? string.Create(CultureInfo.InvariantCulture, $" -{item.Behind}") : string.Empty)
            + (item.IsHead ? " (head)" : string.Empty),
        _ => throw new InvalidOperationException($"Unexpected row {row}."),
    }).ToList();
}
