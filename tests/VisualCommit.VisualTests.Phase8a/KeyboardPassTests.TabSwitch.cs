using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Where the focus goes when the active tab changes while it lies in a region of the window
/// (docs/phase-6-design.md 6.6) and the switch leaves it on nothing, which docs/phase-8a-design.md
/// 1.17 recorded for a stage row: to that same region's target in the tab now shown, the region
/// being the panel that held the focus (the left panel, the graph area, the commit's details, the
/// stage panel); to the graph area's target when the tab now shown does not show that panel, or has
/// no repository and so shows only that panel's empty stand-in (a new tab's welcome page, a tab
/// still opening); with the keyboard's frame when the element that had the focus had it. A focus
/// that the switch leaves where it was (the graph, the summary of a stage panel still shown) stays
/// there, and a tab still opening, which has no graph area's target yet, gives nothing.
/// </summary>
public sealed partial class KeyboardPassTests
{
    private const string GraphFramedFact = GraphFact + ", framed";
    private const string ReadmesRowUnframedFact = "Button StageFileRow \"README.md\" of README.md in UnstagedFileList [Right]";
    private const string MainBranchFact = "Button RefItem \"main\" [Left], framed";
    private const string WelcomeOpenFact = "Button OpenRepoButton \"Open\" [GraphArea], framed";

    /// <summary>
    /// The left panel's first section, "Local branches", reached by Shift+F6 from the graph: Ctrl+Tab
    /// drops the first tab's rows from the shared list, and the focus goes to the left panel's
    /// target in the second tab, its first section, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_the_left_panels_first_section_gives_the_focus_to_the_other_tabs_first_section_with_the_frame()
    {
        var facts = await CtrlTabFromAsync(FocusLeftSectionAsync);

        Assert.Equal(LeftSectionFact, facts.Placed);
        Assert.False(facts.SameAfterSwitch, "The first tab's section kept the focus after Ctrl+Tab.");
        Assert.Equal(LeftSectionFact, facts.AfterSwitch);
    }

    /// <summary>A branch row of the left panel, Tab from its first section: Ctrl+Tab gives the second tab's first section the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_branch_row_of_the_left_panel_gives_the_focus_to_the_other_tabs_first_section_with_the_frame()
    {
        var facts = await CtrlTabFromAsync(async app =>
        {
            await FocusLeftSectionAsync(app);
            await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "RefItem" && AutomationProperties.GetName(control) == "main", "the branch main");
        });

        Assert.Equal(MainBranchFact, facts.Placed);
        Assert.False(facts.SameAfterSwitch, "The first tab's branch row kept the focus after Ctrl+Tab.");
        Assert.Equal(LeftSectionFact, facts.AfterSwitch);
    }

    /// <summary>
    /// A changed file of HEAD's commit in the details, reached by F6 from the graph, both tabs showing
    /// that commit: Ctrl+Tab gives the second tab's first changed file the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_commits_file_row_to_a_tab_showing_a_commit_gives_the_focus_to_its_first_file_row_with_the_frame()
    {
        var facts = await CtrlTabFromAsync(FocusFirstChangedFileAsync, firstShows: TabShows.Commit, secondShows: TabShows.Commit);

        Assert.StartsWith("Button ChangedFile ", facts.Placed, StringComparison.Ordinal);
        Assert.EndsWith(" [Right], framed", facts.Placed, StringComparison.Ordinal);
        Assert.False(facts.SameAfterSwitch, "The first tab's file row kept the focus after Ctrl+Tab.");
        Assert.Equal(facts.Placed, facts.AfterSwitch);
    }

    /// <summary>
    /// A changed file of HEAD's commit in the details, the second tab showing its stage panel, which
    /// stands where the details were: the details' panel is not shown, so the graph area's target, the
    /// graph, takes the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_commits_file_row_to_a_tab_showing_its_stage_panel_gives_the_focus_to_the_graph_with_the_frame()
    {
        var facts = await CtrlTabFromAsync(FocusFirstChangedFileAsync, firstShows: TabShows.Commit, secondShows: TabShows.StagePanel);

        Assert.StartsWith("Button ChangedFile ", facts.Placed, StringComparison.Ordinal);
        Assert.Equal(GraphFramedFact, facts.AfterSwitch);
    }

    /// <summary>
    /// The commit summary, focused by Ctrl+Shift+M, the second tab showing a commit: the stage panel
    /// goes with the summary, and the graph area's target, the graph, takes the focus, without a
    /// frame, as the summary had none.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_the_commit_summary_to_a_tab_showing_a_commit_gives_the_focus_to_the_graph_without_the_frame()
    {
        var facts = await CtrlTabFromAsync(
            async app =>
            {
                app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
                await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary");
            },
            secondShows: TabShows.Commit);

        Assert.Equal(SummaryFact, facts.Placed);
        Assert.Equal(GraphFact, facts.AfterSwitch);
    }

    /// <summary>
    /// README.md's row given the focus back without the frame (F6, then the palette opened and closed
    /// by Esc: D104 gives the focus back unframed): Ctrl+Tab gives the second tab's first row the
    /// focus without the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_stage_row_without_the_frame_gives_the_other_tabs_first_row_without_it()
    {
        var facts = await CtrlTabFromAsync(async app =>
        {
            await FocusReadmesRowAsync(app);
            app.PressPalette();
            await app.WaitForAsync(() => app.IsPaletteOpen, "the palette");
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => !app.IsPaletteOpen && app.FocusedRowPath() == Phase8aExpectations.K.Readme, $"the palette closed and the focus back on README.md's row (it is on {app.DescribeFocus()})");
        });

        Assert.Equal(ReadmesRowUnframedFact, facts.Placed);
        Assert.Equal(ReadmesRowUnframedFact, facts.AfterSwitch);
    }

    /// <summary>
    /// Ctrl+Tab from README.md's row to a new tab, whose welcome page stands in the graph area and
    /// which shows no stage panel: the graph area's target, the welcome page's "Open", takes the
    /// focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_stage_row_to_a_new_tab_gives_the_focus_to_its_welcome_pages_Open_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var mine = app.Session.Shell.ActiveTab;
        app.PressKey(Key.T, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsWelcome, "the new tab's welcome page");
        var welcome = app.Session.Shell.ActiveTab;
        app.PressCtrlTab();
        await app.WaitForAsync(() => ReferenceEquals(app.Session.Shell.ActiveTab, mine), "the repository's tab");
        await ShowStagePanelAsync(app);
        await FocusReadmesRowAsync(app);

        app.PressCtrlTab();
        await app.WaitForAsync(() => ReferenceEquals(app.Session.Shell.ActiveTab, welcome), "the new tab");
        await app.WaitForIdleAsync(mine.Repository!);
        app.Settle();

        Assert.Equal(WelcomeOpenFact, FocusFact(app));
    }

    /// <summary>
    /// Ctrl+Tab from README.md's row to a tab whose repository is still opening (its first
    /// <c>rev-parse</c> held): that tab has no graph area's target yet, so the focus stays on nothing,
    /// and stays there once it has opened (passes before the fix too).
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_a_stage_row_to_a_tab_still_opening_leaves_the_focus_on_nothing()
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, first.Path, held.Probe);
        var mine = app.Session.Shell.ActiveTab;

        // A new tab opens the second repository by Ctrl+O, its first rev-parse held.
        app.PressKey(Key.T, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsWelcome, "the new tab's welcome page");
        var opening = app.Session.Shell.ActiveTab;
        held.HoldNext("rev-parse");
        app.Folders.Answer(second.Path);
        app.PressKey(Key.O, RawInputModifiers.Control);
        var call = await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        Assert.Equal(WorktreeKey.Of(second.Path), WorktreeKey.Of(call.WorkingDirectory!), WorktreeKey.Comparer);

        // Back to the first tab, README.md's row focused, and Ctrl+Tab to the tab still opening.
        app.PressCtrlTab();
        await app.WaitForAsync(() => ReferenceEquals(app.Session.Shell.ActiveTab, mine), "the first tab");
        await ShowStagePanelAsync(app);
        await FocusReadmesRowAsync(app);
        app.PressCtrlTab();
        await app.WaitForAsync(() => ReferenceEquals(app.Session.Shell.ActiveTab, opening), "the tab still opening");
        app.Settle();

        Assert.True(opening is { IsOpening: true, Repository: null }, "The second tab is no longer opening.");
        Assert.Equal(NothingFact, FocusFact(app));

        held.Release();
        await app.WaitForAsync(() => opening.Repository is { Graph.Count: > 0 }, "the second repository's history");
        await app.WaitForIdleAsync();
        app.Settle();
        Assert.Equal(NothingFact, FocusFact(app));
    }

    /// <summary>
    /// Ctrl+T from the left panel's first section, reached by Shift+F6 from the graph: the new tab has
    /// no repository, and its left panel shows only its empty sections and the filter; the graph
    /// area's target, the welcome page's "Open", takes the focus, with the frame, as from a stage row.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_T_from_the_left_panels_first_section_gives_the_focus_to_the_new_tabs_welcome_pages_Open_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var mine = app.Session.Shell.ActiveTab;
        await ShowStagePanelAsync(app);
        await FocusLeftSectionAsync(app);

        app.PressKey(Key.T, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab is { ShowsWelcome: true } shown && !ReferenceEquals(shown, mine), "the new tab's welcome page");
        await app.WaitForIdleAsync(mine.Repository!);
        app.Settle();

        Assert.Equal(WelcomeOpenFact, FocusFact(app));
    }

    /// <summary>
    /// Ctrl+Tab from the left panel's first section to a tab whose repository is still opening (its
    /// first <c>rev-parse</c> held): that tab has no repository yet and no graph area's target, so the
    /// focus stays on nothing, as from a stage row, and stays there once it has opened.
    /// </summary>
    [AvaloniaFact]
    public async Task Ctrl_Tab_from_the_left_panels_first_section_to_a_tab_still_opening_leaves_the_focus_on_nothing()
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, first.Path, held.Probe);
        var mine = app.Session.Shell.ActiveTab;

        // A new tab opens the second repository by Ctrl+O, its first rev-parse held.
        app.PressKey(Key.T, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsWelcome, "the new tab's welcome page");
        var opening = app.Session.Shell.ActiveTab;
        held.HoldNext("rev-parse");
        app.Folders.Answer(second.Path);
        app.PressKey(Key.O, RawInputModifiers.Control);
        var call = await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        Assert.Equal(WorktreeKey.Of(second.Path), WorktreeKey.Of(call.WorkingDirectory!), WorktreeKey.Comparer);

        // Back to the first tab, the left panel's first section focused, and Ctrl+Tab to the tab still opening.
        app.PressCtrlTab();
        await app.WaitForAsync(() => ReferenceEquals(app.Session.Shell.ActiveTab, mine), "the first tab");
        await ShowStagePanelAsync(app);
        await FocusLeftSectionAsync(app);
        app.PressCtrlTab();
        await app.WaitForAsync(() => ReferenceEquals(app.Session.Shell.ActiveTab, opening), "the tab still opening");
        app.Settle();

        Assert.True(opening is { IsOpening: true, Repository: null }, "The second tab is no longer opening.");
        Assert.Equal(NothingFact, FocusFact(app));

        held.Release();
        await app.WaitForAsync(() => opening.Repository is { Graph.Count: > 0 }, "the second repository's history");
        await app.WaitForIdleAsync();
        app.Settle();
        Assert.Equal(NothingFact, FocusFact(app));
    }

    /// <summary>Shift+F6 from the graph to the left panel's first section, "Local branches".</summary>
    private static async Task FocusLeftSectionAsync(ShellDriver app)
    {
        app.PressF6(shift: true);
        await app.WaitForAsync(() => FocusFact(app) == LeftSectionFact, $"the focus on the left panel's first section (it is on {app.DescribeFocus()})");
    }

    /// <summary>F6 from the graph to the details' first changed file.</summary>
    private static async Task FocusFirstChangedFileAsync(ShellDriver app)
    {
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedAutomationId() == "ChangedFile", $"the focus on the details' first changed file (it is on {app.DescribeFocus()})");
    }
}
