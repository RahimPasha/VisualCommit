// Check F14 of part F (docs/test-reports/phase-5.md): hiding and soloing branches (design 8, D159,
// D178): the checked-out branch's menu, feature/alpha hidden with its upstream and shown again, a
// click and the tooltips on the hidden row, a comparison whose To is no row; feature/beta soloed
// and the solo stopped from the section's menu; a hidden branch kept across a restart of the app
// and shown by its eye button. Three test methods: two with one start each, and a plain [Fact]
// with two starts on one data folder (D32).
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksF
{
    private static readonly string[] LocalAlpha = ["Local branches", "feature", "alpha"];
    private static readonly string[] RemoteAlpha = ["Remotes", "origin", "feature", "alpha"];
    private static readonly string[] LocalBeta = ["Local branches", "feature", "beta"];
    private static readonly string[] RemoteBeta = ["Remotes", "origin", "feature", "beta"];

    /// <summary>
    /// Check F14, part A: the current branch's menu (Hide disabled); feature/alpha hidden with its
    /// upstream, its row clicked and its tooltips, compared with main, then shown; dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F14_hide_and_show()
    {
        using var repo = await Scenarios.FiltersAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var palette = Dark;
        var refs = await RefsAsync(repo);
        var config = ConfigFile(repo);

        // Step 1: both feature folders opened: FL0.
        await OpenFeatureFoldersAsync(app);
        Shot(app, PartF.F14FoldersOpen);
        AssertGraph(app, PartF.FL0, PartF.FiltersLanes, working: null, showsDate: false);
        AssertNothingSelected(app);
        AssertLeftPanel(app, PartF.FL0LeftPanel);
        AssertBranch(app, "main");

        // Step 2: main's menu: Hide disabled with its reason; the highlight stays on Pull.
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "main");
        AssertMenuAsReported(app, menu, PartF.MainMenu, PartF.PullFromOriginMain, PartF.MainMenuHeight);
        Assert.Equal(PartF.CheckedOutAlwaysShown, await app.RestPointerOnMenuItemAsync(PartF.HideMain));
        Shot(app, PartF.F14CurrentBranchMenu, keepPointer: true);
        AssertToolTipInsideWindow(app, PartF.CheckedOutAlwaysShown);
        Assert.Equal(PartF.PullFromOriginMain, app.Menu().Highlighted?.Label);
        AssertNothingSelected(app);
        AssertNoRefHighlighted(app);
        await CloseMenuAsync(app);

        // Step 3: feature/alpha's menu; "Hide feature/alpha": the left panel at once, then the graph FL1.
        (menu, _) = await RightClickRefAsync(app, LocalAlpha);
        Shot(app, PartF.F14AlphaMenu);
        AssertMenuAsReported(app, menu, PartF.FeatureMenu("alpha", hidden: false), "Check out feature/alpha", PartF.BranchMenuHeight);
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartF.HideAlpha);
        AssertLeftPanel(app, PartF.FL1LeftPanel);
        await app.WaitForGraphReloadAsync();
        Shot(app, PartF.F14AlphaHidden);
        await AssertAlphaHiddenAsync(app, data, repo, calls);
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(config, ConfigFile(repo));
        AssertNoClippedText(app);

        // Step 4: a click on the hidden row selects nothing; the tooltips of its name and its eye.
        app.Click(await app.RefRowAsync(LocalAlpha));
        await app.WaitForIdleAsync();
        AssertNothingSelected(app);
        AssertNoRefHighlighted(app);
        Assert.Equal(PartF.HiddenNameToolTip, await app.RestPointerAtAsync(app.RefRowNamePoint(LocalAlpha)));
        Shot(app, PartF.F14HiddenRowTooltip, keepPointer: true);
        AssertToolTipInsideWindow(app, PartF.HiddenNameToolTip);
        Assert.Equal(PartF.HiddenToggleToolTip, await app.RestPointerAtAsync(app.RefRowMarks(LocalAlpha).ToggleBounds!.Value.Center));
        app.MoveToNeutral();

        // Step 5: compared with main: To is no row, so its link is disabled; the counts' singular.
        (menu, _) = await RightClickRefAsync(app, LocalAlpha);
        AssertMenuAsReported(app, menu, PartF.FeatureMenu("alpha", hidden: true), "Check out feature/alpha", PartF.BranchMenuHeight);
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartF.CompareAlphaWithMain);
        await app.WaitForAsync(() => app.Compare() is not null, "the comparison of feature/alpha with main");
        await app.WaitForCompareAsync();
        Shot(app, PartF.F14CompareHidden);
        AssertComparison(app, PartF.F14From, PartF.F14To, PartF.F14Counts, branches: true, allDifferences: false, PartF.F14Files, toEnabled: false);
        var revParse = AssertCall(app, calls, PartF.RevParseCall(PartF.MainRef, PartF.AlphaRef));
        Assert.Equal([PartF.FiltersMerge, PartF.AlphaOne, "--"], OutputLines(revParse));
        var mergeBase = AssertCall(app, calls, PartF.MergeBaseCall(PartF.FiltersMerge, PartF.AlphaOne));
        Assert.Equal([PartF.AddTheCore], OutputLines(mergeBase));
        AssertCall(app, calls, PartF.DiffTreeCall(PartF.AddTheCore, PartF.AlphaOne));
        var revList = AssertCall(app, calls, PartF.RevListCall(PartF.FiltersMerge, PartF.AlphaOne));
        Assert.Equal([PartF.F14CountsOutput], OutputLines(revList));
        app.Click(app.FindByAutomationId("CompareClose"));
        await app.WaitForAsync(() => app.Compare() is null, "the comparison to close");
        AssertNothingSelected(app);

        // Step 6: "Show feature/alpha": the graph without a filter, FL0 again; no entry left (FD14).
        calls = app.GitCalls().Count;
        await RightClickRefAsync(app, LocalAlpha);
        app.ClickMenuItem(PartF.ShowAlpha);
        await app.WaitForGraphReloadAsync();
        var shot = Shot(app, PartF.F14AlphaShown);
        AssertNothingFiltered(app, data, calls, shot);
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(config, ConfigFile(repo));
        Assert.Equal(palette.TextPrimary, app.RefRowMarks(LocalAlpha).NameColor);
        await AssertNoDialogAsync(app);
    }

    /// <summary>
    /// Check F14, part B: feature/beta soloed with its upstream (the graph FL2, the labels left
    /// out, the left panel), then "Stop soloing" from the Local branches section's menu; dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F14_solo_and_stop_soloing()
    {
        using var repo = await Scenarios.FiltersAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var palette = Dark;
        var refs = await RefsAsync(repo);
        var config = ConfigFile(repo);

        // Step 7: "Solo feature/beta".
        await OpenFeatureFoldersAsync(app);
        var (menu, _) = await RightClickRefAsync(app, LocalBeta);
        AssertMenuAsReported(app, menu, PartF.FeatureMenu("beta", hidden: false), "Check out feature/beta", PartF.BranchMenuHeight);
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartF.SoloBeta);
        await app.WaitForGraphReloadAsync();
        var shot = Shot(app, PartF.F14BetaSoloed);
        AssertCall(app, calls, PartF.GraphLogCall(PartF.SoloRevisions));
        Assert.Equal(PartF.GraphLogCall(PartF.SoloRevisions), LastGraphLoad(app, calls).CommandText);
        AssertGraph(app, PartF.FL2, PartF.FilteredLanes, working: null, showsDate: true, picture: shot);
        AssertDashedLines(app, PartF.FL1Dashed);
        AssertNothingSelected(app);
        AssertLeftPanel(app, PartF.FL2LeftPanel);
        await AssertRefMarksAsync(app, palette.TextPrimary, "solo", LocalBeta);
        await AssertRefMarksAsync(app, palette.TextPrimary, "solo", RemoteBeta);
        await AssertRefMarksAsync(app, palette.TextDisabled, null, LocalAlpha);
        await AssertRefMarksAsync(app, palette.TextDisabled, null, "Local branches", "feature", "merged");
        await AssertRefMarksAsync(app, palette.TextDisabled, null, RemoteAlpha);
        await AssertRefMarksAsync(app, palette.TextDisabled, null, "Remotes", "origin", "main");
        await AssertRefMarksAsync(app, palette.Accent, null, "Local branches", "main");
        await AssertSavedFilterAsync(data, repo, hidden: [], soloed: [PartF.BetaRef, PartF.OriginBetaRef], upstreams: [new BranchUpstream(PartF.BetaRef, PartF.OriginBetaRef)]);
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(config, ConfigFile(repo));
        AssertNoClippedText(app);

        // Step 8: the section's menu; "Stop soloing"; the menu again with both items disabled.
        (menu, _) = await RightClickRefAsync(app, "Local branches");
        Shot(app, PartF.F14SectionMenu);
        AssertMenuAsReported(app, menu, PartF.SectionMenu(soloed: true), "Create branch…", PartF.SectionMenuHeight);
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartF.StopSoloingItem);
        await app.WaitForGraphReloadAsync();
        AssertNothingFiltered(app, data, calls, picture: null);
        Assert.DoesNotContain(LeftPanelLines(app), line => line.Contains("[solo]", StringComparison.Ordinal) || line.Contains("(dimmed)", StringComparison.Ordinal));
        (menu, _) = await RightClickRefAsync(app, "Local branches");
        AssertMenuAsReported(app, menu, PartF.SectionMenu(soloed: false), "Create branch…", PartF.SectionMenuHeight);
        await CloseMenuAsync(app);
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(config, ConfigFile(repo));
        await AssertNoDialogAsync(app);
    }

    /// <summary>
    /// Check F14, part C: feature/alpha hidden, the app closed and started again on the same data
    /// folder: the graph loads hidden from the start; its eye button shows it again. A plain
    /// [Fact] with two app starts, each with a new application (D32).
    /// </summary>
    [Fact]
    public async Task Check_F14_hidden_after_a_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase5ChecksF).Assembly;
        using var repo = await Scenarios.FiltersAsync();
        using var data = new TempDirectory("data");
        var refs = await RefsAsync(repo);

        // Step 9: feature/alpha hidden; the app closes.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenAsync(data, repo);
                await OpenFolderAsync(app, "Local branches", "feature");
                var calls = app.GitCalls().Count;
                await RightClickRefAsync(app, LocalAlpha);
                app.ClickMenuItem(PartF.HideAlpha);
                await app.WaitForGraphReloadAsync();
                AssertCall(app, calls, PartF.GraphLogCall(PartF.HideRevisions));
                AssertGraph(app, PartF.FL1, PartF.FilteredLanes, working: null, showsDate: true);
                AssertDashedLines(app, PartF.FL1Dashed);
                await AssertAlphaFilterSavedAsync(data, repo);
                return true;
            },
            cancellationToken);

        await AssertAlphaFilterSavedAsync(data, repo);

        // Steps 10 and 11: a new instance on the same data folder, then the eye button.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = ShellDriver.Start(data.Path);
                await app.WaitForRepositoryAsync();
                app.MoveToNeutral();
                await OpenFeatureFoldersAsync(app);
                var shot = Shot(app, PartF.F14AfterRestart);
                var loads = app.GitCalls().Where(call => call.CommandText.StartsWith(PartF.GraphLogStart, StringComparison.Ordinal)).ToList();
                Assert.NotEmpty(loads);
                Assert.All(loads, load => Assert.Equal(PartF.GraphLogCall(PartF.HideRevisions), load.CommandText));
                AssertGraph(app, PartF.FL1, PartF.FilteredLanes, working: null, showsDate: true, picture: shot);
                AssertDashedLines(app, PartF.FL1Dashed);
                AssertLeftPanel(app, PartF.FL1LeftPanel);
                await AssertRefMarksAsync(app, Dark.TextDisabled, "eye", LocalAlpha);
                await AssertRefMarksAsync(app, Dark.TextDisabled, "eye", RemoteAlpha);
                await AssertAlphaFilterSavedAsync(data, repo);

                // Step 11: the eye button shows the branch with its upstream; the press starts no drag.
                var calls = app.GitCalls().Count;
                var toggle = app.RefRowMarks(LocalAlpha).ToggleBounds!.Value.Center;
                app.PressMouse(toggle);
                Assert.False(app.BranchDrag.IsDragging, "The press on the eye button started a drag.");
                app.ReleaseMouse(toggle);
                Assert.False(app.BranchDrag.IsDragging, "The press on the eye button started a drag.");
                await app.WaitForGraphReloadAsync();
                shot = Shot(app, PartF.F14ShownByToggle);
                AssertNothingFiltered(app, data, calls, shot);
                AssertNoRefHighlighted(app);
                await AssertNoDialogAsync(app);
                return true;
            },
            cancellationToken);

        Assert.Empty(SavedFilters(data));
        Assert.Equal(refs, await RefsAsync(repo));
    }

    /// <summary>Opens the folder feature under Local branches, then the one under Remotes › origin (F14 step 1).</summary>
    private static async Task OpenFeatureFoldersAsync(ShellDriver app)
    {
        await OpenFolderAsync(app, "Local branches", "feature");
        await OpenFolderAsync(app, "Remotes", "origin", "feature");
        app.MoveToNeutral();
    }

    /// <summary>
    /// F14 after "Hide feature/alpha" (step 3): the graph's load with the hide revisions, FL1 with
    /// the Date column and its dashed stash line, nothing selected, both alpha rows in the disabled
    /// colour with the eye (the local one's before its ↓1), the session's entry.
    /// </summary>
    private static async Task AssertAlphaHiddenAsync(ShellDriver app, TempDirectory data, TempRepo repo, int calls)
    {
        var palette = PaletteOf(app);
        AssertCall(app, calls, PartF.GraphLogCall(PartF.HideRevisions));
        Assert.Equal(PartF.GraphLogCall(PartF.HideRevisions), LastGraphLoad(app, calls).CommandText);
        Assert.False(app.Find<ProgressBar>("GraphReloading").IsVisible, "GraphReloading still shows.");
        AssertGraph(app, PartF.FL1, PartF.FilteredLanes, working: null, showsDate: true);
        AssertDashedLines(app, PartF.FL1Dashed);
        AssertNothingSelected(app);
        AssertLeftPanel(app, PartF.FL1LeftPanel);
        await AssertRefMarksAsync(app, palette.TextDisabled, "eye", LocalAlpha);
        await AssertRefMarksAsync(app, palette.TextDisabled, "eye", RemoteAlpha);
        var behind = (await app.RefRowAsync(LocalAlpha)).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "BehindCount" && text.IsEffectivelyVisible);
        Assert.True(app.RefRowMarks(LocalAlpha).ToggleBounds!.Value.Right <= app.BoundsOf(behind).Left, "The eye button is not before the ↓1.");
        await AssertRefMarksAsync(app, palette.TextPrimary, null, LocalBeta);
        await AssertRefMarksAsync(app, palette.Accent, null, "Local branches", "main");
        await AssertAlphaFilterSavedAsync(data, repo);
    }

    /// <summary>The session's entry with feature/alpha and its upstream hidden (F14 steps 3, 9, 10).</summary>
    private static Task AssertAlphaFilterSavedAsync(TempDirectory data, TempRepo repo) =>
        AssertSavedFilterAsync(data, repo, hidden: [PartF.AlphaRef, PartF.OriginAlphaRef], soloed: [], upstreams: [new BranchUpstream(PartF.AlphaRef, PartF.OriginAlphaRef)]);

    /// <summary>
    /// Nothing hidden or soloed any more (F14 steps 6, 8, 11): the graph loaded with the unfiltered
    /// revisions, FL0 (with <paramref name="picture"/>, its nodes sampled), no toggle and no row in
    /// the disabled colour, and no session entry (FD14).
    /// </summary>
    private static void AssertNothingFiltered(ShellDriver app, TempDirectory data, int calls, Screenshot? picture)
    {
        Assert.Equal(PartF.GraphLogCall(PartF.UnfilteredRevisions), LastGraphLoad(app, calls).CommandText);
        Assert.Equal(0, LastGraphLoad(app, calls).ExitCode);
        AssertGraph(app, PartF.FL0, PartF.FiltersLanes, working: null, showsDate: false, picture: picture);
        AssertDashedLines(app, PartF.FL0Dashed);
        AssertNothingSelected(app);
        AssertLeftPanel(app, PartF.FL0LeftPanel);
        Assert.Empty(SavedFilters(data));
    }
}
