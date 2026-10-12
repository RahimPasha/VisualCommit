// Check C17 of phase 4's walk-through (docs/test-reports/phase-4.md, the row C17 of "Checks"): a
// rebase started in a terminal while the app is open, resolved outside the app, and finished by
// the app's Continue. What checks C14 to C17 share is in Phase4Checks.C.EditingSupport.cs; the
// expected values in Phase4Expectations.C2.cs (PartC2).
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C17: the banner of a rebase started in a terminal offers Continue and Skip (no
    /// "terminal" text), the stage panel's <c>Continue rebase</c>, the conflict resolved outside
    /// the app, Continue from the banner finishes the rebase; dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C17_terminal_rebase()
    {
        using var repo = await Scenarios.ConflictsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: GC0, main checked out, no banner.
        AssertGraph(app, PartC2.Gc0, PartC2.ConflictsLanes, working: null, showsDate: false);
        AssertBranch(app, "main");
        Assert.Null(app.BannerBounds);

        // Step 2: outside the app, git switch feature/picks and git rebase main stop at Pick conflicting.
        await repo.GitAsync("switch", "feature/picks");
        var rebase = await repo.GitAllowingFailureAsync("rebase", "main");
        Assert.Equal(1, rebase.ExitCode);
        await app.WaitForAsync(
            () => app.BannerBounds is not null && app.Find<TextBlock>("BannerText").Text == PartC2.PicksRebaseBanner
                && Graph(app).ShowsWorkingRow && Graph(app).WorkingRowText == PartC2.WorkingConflicts(1),
            "the rebase's banner and the working-changes row with its conflict",
            OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        Shot(app, "c17a-terminal-rebase");
        await AssertNoDialogAsync(app);
        AssertBanner(app, PartC2.PicksRebaseBanner, PartC2.AbortRebase, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), BannerButton.Skip, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        Assert.Equal(PartC2.PicksRebaseToolTip, C14To17.BannerToolTip(app));
        foreach (var text in new[] { app.Find<TextBlock>("BannerText").Text, C14To17.BannerToolTip(app), ToolTip.GetTip(app.BannerContinueButton) as string, ToolTip.GetTip(app.BannerSkipButton) as string, ToolTip.GetTip(app.FindByAutomationId("BannerAbortButton")) as string })
        {
            Assert.DoesNotContain("terminal", text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        AssertGraph(app, PartC2.K1, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.WorkingConflicts(1)), showsDate: false);
        AssertBranch(app, "Detached at d71cdc4");
        C17Steps.AssertNoLocalBranchAccented(app);
        await C17Steps.AssertStoppedAsync(repo);

        // Step 3: the pointer rests on the disabled Continue: its reason.
        var tip = await app.RestPointerOnAsync(app.BannerContinueButton);
        Shot(app, "c17b-continue-tooltip", keepPointer: true);
        Assert.Equal(PartC2.ResolveFirst(1), tip);
        Assert.False(app.BannerContinueButton.IsEffectivelyEnabled);
        C17Steps.AssertToolTipInWindow(app);

        // Step 4: the pointer at the neutral point, the tooltip closed; the working-changes row: the stage panel.
        app.MoveToNeutral();
        await app.WaitForAsync(() => !ToolTip.GetIsOpen(app.BannerContinueButton), "Continue's tooltip to close");
        app.Click(app.WorkingRowPoint());
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(1, 1), PartC2.StagedTitle(0));
        await app.WaitForIdleAsync();
        Shot(app, "c17c-stage-panel");
        Assert.Equal(["U notes.txt"], C14To17.FileRows(app, "UnstagedFileList"));
        await C14To17.AssertConflictRowAsync(app, "notes.txt");
        C14To17.AssertEmptyLists(app, unstagedEmpty: false, stagedEmpty: true);
        await C14To17.AssertMessageAsync(app, PartC2.PickConflictingSummary, string.Empty);
        C14To17.AssertCommitButton(app, PartC2.ContinueRebaseButton, enabled: false, PartC2.ResolveFirst(1));
        var amend = (CheckBox)app.FindByAutomationId("AmendCheckBox");
        Assert.False(amend.IsEffectivelyEnabled, "Amend is enabled during the rebase's stop.");
        AssertTip(amend, PartC2.FinishRebaseFirst);

        // Step 5: outside the app, notes.txt resolved and staged.
        repo.WriteFile("notes.txt", PartC2.PicksResolvedNotes);
        await repo.GitAsync("add", "notes.txt");
        await app.WaitForAsync(
            () => app.BannerBounds is not null && app.Find<TextBlock>("BannerText").Text == PartC2.PicksRebaseResolvedBanner,
            "the banner to say the conflicts are resolved",
            OutsideChangeTimeout);
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(0), PartC2.StagedTitle(1));
        await app.WaitForIdleAsync();
        Shot(app, "c17d-resolved-outside");
        Assert.Equal(["M  notes.txt"], await C14To17.PorcelainAsync(repo));
        AssertBanner(app, PartC2.PicksRebaseResolvedBanner, PartC2.AbortRebase, BannerButton.Continue, BannerButton.Skip, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        AssertTip(app.BannerContinueButton, null);
        AssertGraph(app, PartC2.K1, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.Working), showsDate: false);
        C14To17.AssertEmptyLists(app, unstagedEmpty: true, stagedEmpty: false);
        Assert.Equal(["M notes.txt"], C14To17.FileRows(app, "StagedFileList"));
        C14To17.AssertCommitButton(app, PartC2.ContinueRebaseButton, enabled: true);
        await C14To17.AssertMessageAsync(app, PartC2.PickConflictingSummary, string.Empty);

        // Step 6: the banner's Continue: the staged file checked for markers (none: no question), then Continue the rebase.
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        using var statusTexts = app.RecordStatusTexts();
        app.ClickBannerContinue();
        await WaitForOperationAsync(app, repo, PartC2.ContinueRebaseTitle, mark, OperationChainTimeout);
        Assert.Contains(PartC2.ContinueRebaseRunning, statusTexts.Texts);
        Assert.Equal(PartC2.RebasedPicks, statusTexts.Texts[^1]);
        await app.WaitForAsync(() => app.BannerBounds is null && !Graph(app).ShowsWorkingRow, "the banner and the working-changes row to go", OperationChainTimeout);
        await app.WaitForIdleAsync(timeout: OperationChainTimeout);
        Shot(app, "c17e-rebased");
        await AssertNoDialogAsync(app);
        var check = AssertCalled(app, calls, PartC2.CheckStagedMarkers);
        Assert.Equal((0, string.Empty), (check.ExitCode, check.StandardOutput));
        AssertCalledInOrder(app, calls, PartC2.CheckStagedMarkers, "git commit --no-verify --cleanup=whitespace --file=-", PartC2.RebaseContinue);
        var continued = AssertCalled(app, calls, PartC2.RebaseContinue);
        Assert.Equal(0, continued.ExitCode);
        if (await IsReportGitAsync())
        {
            Assert.Contains("Successfully rebased and updated refs/heads/feature/picks.", continued.StandardError + continued.StandardOutput, StringComparison.Ordinal);
        }

        Assert.Null(app.BannerBounds);
        AssertGraph(app, PartC2.K2, PartC2.K2Lanes, working: null, showsDate: false);
        AssertBranch(app, "feature/picks");
        AssertStatus(app, PartC2.RebasedPicks);
        Assert.Equal(-1, Graph(app).SelectedIndex);
        Assert.False(Graph(app).IsWorkingRowSelected, "The working-changes row is still selected.");
        Assert.Empty(Graph(app).Selection.Shas);
        await C17Steps.AssertRebasedAsync(repo);
        AssertNoClippedText(app);
    }

    /// <summary>What check C17 asserts of git and the window.</summary>
    private static class C17Steps
    {
        /// <summary>No local branch row of the left panel is the one HEAD is on (HEAD is detached): none in the accent colour.</summary>
        public static void AssertNoLocalBranchAccented(ShellDriver app)
        {
            var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
            var start = rows.FindIndex(row => row is RefSectionRow && row.Text == "Local branches");
            var local = rows.Skip(start + 1).TakeWhile(row => row is RefItemRow).OfType<RefItemRow>().ToList();
            Assert.NotEmpty(local);
            Assert.All(local, row => Assert.False(row.IsHead, $"{row.Text} is shown as the branch HEAD is on."));
            var accent = PaletteOf(app).Accent;
            var names = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name == "RefName" && text.IsEffectivelyVisible).ToList();
            Assert.All(names, text => Assert.NotEqual(accent, (text.Foreground as ISolidColorBrush)?.Color));
        }

        /// <summary>The tooltip that is open lies inside the window.</summary>
        public static void AssertToolTipInWindow(ShellDriver app)
        {
            var tip = app.Window.GetVisualDescendants().OfType<ToolTip>().Single(control => control.IsEffectivelyVisible);
            var bounds = app.BoundsOf(tip);
            var window = app.Window.ClientSize;
            Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.Width && bounds.Bottom <= window.Height, $"The tooltip at {bounds} is not inside the window.");
        }

        /// <summary>Git at the terminal rebase's stop (C17 step 2).</summary>
        public static async Task AssertStoppedAsync(TempRepo repo)
        {
            var head = C14To17.Lines((await repo.GitAsync("log", "-1", "--format=%H%n%P%n%aI%n%cI", "HEAD")).StandardOutput).ToList();
            Assert.Equal(PartC2.PickCleanReplayed, head[0]);
            Assert.StartsWith(PartC2.MainSha, head[1], StringComparison.Ordinal);
            Assert.DoesNotContain(' ', head[1]);
            AssertGitDate(ScenarioTime(5), head[2]);
            AssertGitDate(ScenarioTime(10), head[3]);
            Assert.Equal("HEAD", (await repo.GitAsync("rev-parse", "--abbrev-ref", "HEAD")).StandardOutput.Trim());
            Assert.Equal(PartC2.MergeStopStatus, await C14To17.PorcelainAsync(repo));
            Assert.Equal("refs/heads/feature/picks", C14To17.GitFile(repo, "rebase-merge/head-name"));
            Assert.StartsWith(PartC2.MainSha, C14To17.GitFile(repo, "rebase-merge/onto"), StringComparison.Ordinal);
            Assert.StartsWith(PartC2.PickLastSha, C14To17.GitFile(repo, "rebase-merge/orig-head"), StringComparison.Ordinal);
            Assert.Equal(("2", "3"), (C14To17.GitFile(repo, "rebase-merge/msgnum"), C14To17.GitFile(repo, "rebase-merge/end")));
            Assert.StartsWith(PartC2.PickConflictingSha, C14To17.GitFile(repo, "rebase-merge/stopped-sha"), StringComparison.Ordinal);
            Assert.StartsWith(PartC2.PickConflictingSha, C14To17.GitFile(repo, "REBASE_HEAD"), StringComparison.Ordinal);
            var done = Commands(repo, "rebase-merge/done");
            Assert.Equal(2, done.Count);
            // A newer git (CI's 2.55) writes each subject as a comment ("# Pick clean") and adds
            // its "# Conflicts:" block to the message (design 0.1); 2.36 does neither.
            Assert.Matches("^pick " + PartC2.PickCleanSha + "[0-9a-f]* (# )?Pick clean$", done[0]);
            Assert.Matches("^pick " + PartC2.PickConflictingSha + "[0-9a-f]* (# )?Pick conflicting$", done[1]);
            var todo = Assert.Single(Commands(repo, "rebase-merge/git-rebase-todo"));
            Assert.Matches("^pick " + PartC2.PickLastSha + "[0-9a-f]* (# )?Pick last$", todo);
            var message = File.ReadAllText(Path.Combine(repo.Path, ".git", "rebase-merge", "message")).Replace("\r", string.Empty, StringComparison.Ordinal);
            if (await IsReportGitAsync())
            {
                Assert.Equal("Pick conflicting\n\n", message);
            }
            else
            {
                Assert.StartsWith("Pick conflicting\n\n", message, StringComparison.Ordinal);
            }
            Assert.Contains(PartC2.PickConflictingAuthorScriptDate, File.ReadAllLines(Path.Combine(repo.Path, ".git", "rebase-merge", "author-script")));
            Assert.Equal(["Pick conflicting", string.Empty, "# Conflicts:", "#\tnotes.txt"], File.ReadAllLines(Path.Combine(repo.Path, ".git", "MERGE_MSG")));
            Assert.Equal(C14To17.Bytes(PartC2.PicksStopNotes), C14To17.WorkingFile(repo, "notes.txt"));
        }

        /// <summary>Git after the app's Continue (C17 step 6): K2, the messages, dates and tree, no backup.</summary>
        public static async Task AssertRebasedAsync(TempRepo repo)
        {
            Assert.Empty(await C14To17.PorcelainAsync(repo));
            Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-merge")), "rebase-merge/ is still there.");
            Assert.Equal("refs/heads/feature/picks", (await repo.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
            Assert.Equal(PartC2.PickLastRebased, (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim());
            Assert.StartsWith(PartC2.MainSha, (await repo.GitAsync("rev-parse", "refs/heads/main")).StandardOutput.Trim(), StringComparison.Ordinal);
            Assert.Equal(PartC2.PickConflictingRebased, (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim());
            Assert.Equal(PartC2.PickCleanReplayed, (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim());
            Assert.StartsWith(PartC2.MainSha, (await repo.GitAsync("rev-parse", "HEAD~3")).StandardOutput.Trim(), StringComparison.Ordinal);
            Assert.Equal("Pick last\n", await MessageAsync(repo, "HEAD"));
            Assert.Equal("Pick conflicting\n", await MessageAsync(repo, "HEAD~1"));
            Assert.Equal("Pick clean\n", await MessageAsync(repo, "HEAD~2"));

            var conflicting = C14To17.Lines((await repo.GitAsync("log", "-1", "--format=%an <%ae>%n%aI%n%cn <%ce>%n%cI", "HEAD~1")).StandardOutput).ToList();
            Assert.Equal("Test Author <author@example.com>", conflicting[0]);
            AssertGitDate(ScenarioTime(6), conflicting[1]);
            AssertGitDate(AppCommitTime, conflicting[3]);
            var last = C14To17.Lines((await repo.GitAsync("log", "-1", "--format=%an <%ae>%n%aI%n%cI", "HEAD")).StandardOutput).ToList();
            Assert.Equal("Test Author <author@example.com>", last[0]);
            AssertGitDate(ScenarioTime(7), last[1]);
            AssertGitDate(AppCommitTime, last[2]);

            Assert.Equal(PartC2.PicksResolvedNotes, (await repo.GitAsync("cat-file", "-p", PartC2.PickConflictingRebased + ":notes.txt")).StandardOutput);
            Assert.Equal(
                ["README.md", "image.png", "last.txt", "new.txt", "notes.txt", "other.txt", "pick.txt"],
                C14To17.Lines((await repo.GitAsync("ls-tree", "--name-only", "refs/heads/feature/picks")).StandardOutput));
            Assert.Empty(C14To17.Lines((await repo.GitAsync("for-each-ref", "refs/visualcommit/")).StandardOutput));
        }

        /// <summary>A commit's message exactly as stored: what follows the header's empty line.</summary>
        private static async Task<string> MessageAsync(TempRepo repo, string revision)
        {
            var raw = (await repo.GitAsync("cat-file", "commit", revision)).StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal);
            return raw[(raw.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];
        }

        /// <summary>The command lines of a todo file of git's (no comments, no empty lines).</summary>
        private static List<string> Commands(TempRepo repo, string name) =>
            [.. File.ReadAllLines(Path.Combine(repo.Path, ".git", name)).Where(line => line.Length > 0 && !line.StartsWith('#'))];
    }
}
