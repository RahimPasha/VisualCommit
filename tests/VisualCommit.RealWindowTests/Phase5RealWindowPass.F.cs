// Part F's real-window steps (docs/test-reports/phase-5.md, part F, the "Real window" column of F10
// to F15): F10 steps 1 to 3 and Esc (real typing and a real Enter); F12 steps 1 and 2 (a real click
// and Ctrl+click, a real click on a comparison's file) and Esc; F14 part C (a real hide, the window
// closed by its close button, the app launched again on the same data folder, a real press on the
// eye button). Each launch on a fresh copy at 1100×700, dark. F11, F13 and F15 are "n/a" (the
// report gives the reasons); F10's part B is at 1920×1080, which does not fit the development
// machine's screen.
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase5RealWindowPass
{
    /// <summary>
    /// Check F10, steps 1 to 3 and Esc, with the real mouse and keyboard: a real click on Search (the
    /// bar with the focus in its box), <c>tokenizer</c> typed key by key until the count reads "2
    /// matches" (<c>f10b-tokenizer</c>), a real Enter selecting the first match with the focus left
    /// in the box (<c>f10c-first-match</c>), and a real Esc: no search bar, the match still shown.
    /// </summary>
    [Fact]
    public async Task Check_F10_search_with_real_typing()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var app = Launch(repo);
        var refs = await RefsAsync(repo);
        WaitForNothingSelected(app);
        Assert.False(app.IsSearchBarShown, "The search bar shows at the start.");

        // Step 1: a real click on the toolbar's Search: the bar, the focus in its empty box.
        app.ClickWithMouse(app.Find("SearchButton"));
        app.WaitFor(() => app.IsSearchBarShown && app.HasFocusOnScreen("SearchBox"), "the search bar with the keyboard focus in its box", StepTimeout);
        Assert.Equal(PartF.CommitsMode, app.Find("SearchModeButton").Name);
        Assert.Equal(string.Empty, app.SearchCountText());
        Assert.Equal(PartF.RowsTopOpen, app.GraphRowsBounds().Top, PartF.PositionTolerance);

        // Step 2: "tokenizer" typed key by key: the box's delay, then two matches, nothing selected.
        app.TypeText(PartF.Tokenizer);
        app.WaitForSearchCount("2 matches", StepTimeout);
        Assert.Equal(PartF.Tokenizer, BoxText(app, "SearchBox"));
        Assert.True(app.Find("SearchPrevious").IsEnabled && app.Find("SearchNext").IsEnabled, "Previous or Next is disabled with two matches.");
        Assert.True(app.HasFocusOnScreen("SearchBox"), "The search box lost the keyboard focus.");
        WaitForNothingSelected(app);
        Compare(app, PartF.F10Tokenizer);

        // Step 3: a real Enter selects the first match, as a click selects it; the focus stays in the box.
        app.PressKey(VirtualKeyShort.RETURN);
        app.WaitForSearchCount("1 of 2", StepTimeout);
        WaitForDetails(app, PartF.CompareWholeLinesDetails.Subject, PartF.CompareWholeLines);
        app.WaitFor(() => app.HasFocusOnScreen("SearchBox"), "the keyboard focus to stay in the search box after Enter", RealApp.DefaultWaitTimeout);
        Compare(app, PartF.F10FirstMatch);

        // Esc: no search bar remains; the match stays selected and shown; the graph takes the focus.
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => !app.IsSearchBarShown && app.ShownElements(ShellSearchBarId).Count == 0, "the search bar to close", StepTimeout);
        app.WaitFor(app.GraphHasKeyboardFocus, "the graph to take the keyboard focus", StepTimeout);
        Assert.Equal(PartF.RowsTopClosed, app.GraphRowsBounds().Top, PartF.PositionTolerance);
        WaitForDetails(app, PartF.CompareWholeLinesDetails.Subject, PartF.CompareWholeLines);

        Assert.Equal(0, app.Close());
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(PartF.HistoryStatus, await GitLinesAsync(repo, "status", "--porcelain"));
    }

    /// <summary>
    /// Check F12, steps 1 and 2, with the real mouse: a real click on "Rename the parser to reader"
    /// and a real Ctrl+click on "Read whole lines" (the comparison in the right panel at once:
    /// <c>f12a-two-commits</c>), a real click on <c>Reader.cs</c> in the comparison's list (its diff,
    /// the comparison kept: <c>f12b-file-diff</c>), then Esc closing the diff only.
    /// </summary>
    [Fact]
    public async Task Check_F12_compare_two_commits()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var app = Launch(repo);
        var refs = await RefsAsync(repo);
        WaitForNothingSelected(app);

        // Step 1: rows 7 and 0 (display rows 8 and 1 below the working-changes row), the second with Ctrl.
        var rename = RowPoint(app, PartF.RenameParserRow + 1, PartF.HistoryLanes);
        app.ClickAt(rename.X, rename.Y);
        WaitForDetails(app, PartF.RenameParserDetails.Subject, PartF.RenameParser);
        var readWholeLines = RowPoint(app, PartF.ReadWholeLinesRow + 1, PartF.HistoryLanes);
        app.ClickAt(readWholeLines.X, readWholeLines.Y, VirtualKeyShort.CONTROL);
        app.WaitForComparison(PartF.F12From, PartF.F12To, StepTimeout);
        Assert.Equal([PartF.CompareTitle], app.NamesOnScreen("CompareTitle"));
        Assert.Equal([PartF.ChangedFiles(PartF.F12Files.Count)], app.NamesOnScreen("CompareFilesTitle"));
        Assert.Equal(PartF.F12Files.Select(file => file.Split(' ')[1]), CompareFileNames(app));
        Assert.Single(app.ShownElements("CompareSwap"));
        Assert.Empty(app.ShownElements("CompareClose"));
        Assert.Empty(app.ShownElements("CompareCounts"));
        Assert.Empty(app.ShownElements("CompareAllDifferences"));
        Assert.Empty(app.ShownElements("MultiSelectionTitle"));
        Compare(app, PartF.F12TwoCommits);

        // Step 2: a real click on Reader.cs in the comparison's list: its diff, From's tree against To's.
        app.ClickWithMouse(app.CompareFileRow("Reader.cs"));
        app.WaitForText("DiffSideLabel", PartF.ReaderSideLabel, StepTimeout);
        app.WaitForComparison(PartF.F12From, PartF.F12To, StepTimeout);
        Compare(app, PartF.F12FileDiff);

        // Esc closes the diff only: the comparison stays.
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitUntilGone("DiffSideLabel", StepTimeout);
        app.WaitForComparison(PartF.F12From, PartF.F12To, StepTimeout);

        Assert.Equal(0, app.Close());
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(PartF.HistoryStatus, await GitLinesAsync(repo, "status", "--porcelain"));
    }

    /// <summary>
    /// Check F14, part C, in two real processes on one data folder: the folder opened and
    /// "Hide feature/alpha" chosen with the real mouse, the graph reloaded, the window closed by its
    /// close button (the process ends and saves the session, read between the two processes), the
    /// app launched again on the same data folder (the graph hidden from its first rows, the eye
    /// buttons on both alpha rows: <c>f14j-after-restart</c>), and a real click on the eye button: the branch and its
    /// upstream shown again, nothing selected, no drag (<c>f14k-shown-by-toggle</c>).
    /// </summary>
    [Fact]
    public async Task Check_F14_hidden_branch_across_a_real_restart()
    {
        using var repo = await Scenarios.FiltersAsync();
        var refs = await RefsAsync(repo);
        var name = NameOf(repo);
        Assert.Equal(PartF.FiltersRepo, name);
        string data;

        // Step 9: feature/alpha hidden with the real mouse; the window closed by its close button.
        using (var app = Launch(repo))
        {
            data = app.DataDirectory;
            app.ClickWithMouse(app.RefRow("Local branches", "feature"));
            app.WaitFor(() => app.HasRefRow("Local branches", "feature", "alpha"), "the folder feature to open", StepTimeout);
            app.RightClickWithMouse(app.RefRow("Local branches", "feature", "alpha"));
            Assert.Contains(PartF.HideAlpha, app.MenuItemNames());
            var reload = ExpectGraphLoad(app, name);
            app.ClickMenuItem(PartF.HideAlpha);
            reload.Wait();
            app.WaitFor(() => app.RefRowToggle("RefHiddenToggle", "Local branches", "feature", "alpha") is not null, "the eye button of feature/alpha", StepTimeout);
            WaitForNothingSelected(app);
            app.WaitFor(() => SavedSession(data)?.GraphFilters.Count == 1, "the session to hold the filter", StepTimeout);
            app.ClickCloseButton();
            Assert.Equal(0, app.WaitForExit(TimeSpan.FromSeconds(15)));
        }

        // The session the first process saved, read between the two processes.
        await AssertAlphaHiddenInSessionAsync(data, repo);

        // Step 10: the app launched again on the same data folder: FL1 from its first rows, no reload.
        var loadsBefore = AppLogFile.ReadLines(data).Count(line => Loaded(name).IsMatch(line));
        using (var app = Relaunch(data, name))
        {
            app.ClickWithMouse(app.RefRow("Local branches", "feature"));
            app.WaitFor(() => app.HasRefRow("Local branches", "feature", "alpha"), "the folder Local branches › feature to open", StepTimeout);
            app.ClickWithMouse(app.RefRow("Remotes", "origin", "feature"));
            app.WaitFor(() => app.HasRefRow("Remotes", "origin", "feature", "alpha"), "the folder Remotes › origin › feature to open", StepTimeout);
            app.WaitFor(
                () => app.RefRowToggle("RefHiddenToggle", "Local branches", "feature", "alpha") is not null
                    && app.RefRowToggle("RefHiddenToggle", "Remotes", "origin", "feature", "alpha") is not null,
                "the eye buttons of both alpha rows",
                StepTimeout);
            Run.Note($"f14j-after-restart: the new process loaded the graph {app.CountLogLines(Loaded(name)) - loadsBefore} time(s) before the capture (the scripted run asserts each load's revisions).");
            Assert.Empty(app.ShownElements("GraphReloading"));
            WaitForNothingSelected(app);
            Compare(app, PartF.F14AfterRestart);
            await AssertAlphaHiddenInSessionAsync(data, repo);

            // Step 11: a real click on the eye button shows the branch with its upstream; the press starts no drag.
            var toggle = app.RefRowToggle("RefHiddenToggle", "Local branches", "feature", "alpha")!;
            var reload = ExpectGraphLoad(app, name);
            app.ClickWithMouse(toggle);
            reload.Wait();
            app.WaitFor(() => app.ShownElements("RefHiddenToggle").Count == 0, "no eye button left", StepTimeout);
            app.WaitFor(() => SavedSession(data)?.GraphFilters.Count == 0, "the session to hold no filter", StepTimeout);
            WaitForNothingSelected(app);
            Assert.Empty(app.FindAll("ContextMenu"));
            Compare(app, PartF.F14ShownByToggle);
            Assert.Equal(0, app.Close());
        }

        Assert.Empty(ReadSession(data).GraphFilters);
        Assert.Equal(refs, await RefsAsync(repo));
    }

    /// <summary>The search bar's automation id (design 13.4).</summary>
    private const string ShellSearchBarId = "SearchBar";

    /// <summary>The comparison's file rows on screen (<c>ChangedFile</c> inside <c>CompareFileList</c>), top to bottom, by name.</summary>
    private static IReadOnlyList<string> CompareFileNames(RealApp app) =>
        app.ShownElements("CompareFileList") is [var list]
            ? [.. list.FindAllDescendants(condition => condition.ByAutomationId("ChangedFile"))
                .Where(row => !row.IsOffscreen && row.BoundingRectangle is { Width: > 0, Height: > 0 })
                .OrderBy(row => row.BoundingRectangle.Top)
                .Select(row => row.Name)]
            : [];

    /// <summary>
    /// The session's filter entry with feature/alpha and its upstream hidden (F14 steps 9 and 10):
    /// the only one, for the working tree as git names it, nothing soloed, no hidden remote, the
    /// upstream that came with the branch, the remote origin (LastUsed not asserted).
    /// </summary>
    private static async Task AssertAlphaHiddenInSessionAsync(string data, TempRepo repo)
    {
        var filter = Assert.Single(ReadSession(data).GraphFilters);
        Assert.Equal(await WorkingTreeAsync(repo), filter.RepositoryPath);
        Assert.Equal([PartF.AlphaRef, PartF.OriginAlphaRef], filter.Hidden);
        Assert.Empty(filter.Soloed);
        Assert.Empty(filter.HiddenRemotes);
        Assert.Equal([new BranchUpstream(PartF.AlphaRef, PartF.OriginAlphaRef)], filter.Upstreams);
        Assert.Equal(["origin"], filter.Remotes);
    }
}
