using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part K's expected values against the report and the app (docs/test-reports/phase-8a.md, part K's
/// conventions; docs/phase-8a-design.md 6.5): every picture name is of design 0.3's form and is named
/// once; the texts equal the app's constants; K1's <c>--numstat</c> lines and the index's file follow
/// from the "changes" scenario's files; K1's count of Tabs follows from its rows; the real window's
/// launches are the methods' twins.
/// </summary>
public sealed class Phase8aExpectationTestsK
{
    [Fact]
    public void The_picture_names_are_of_the_reports_form_and_each_is_named_once()
    {
        Assert.Equal(14, K.Pictures.Count);
        Assert.All(K.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.All(K.Pictures, name => Assert.StartsWith("k", name, StringComparison.Ordinal));
        Assert.Equal(K.Pictures.Count, K.Pictures.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_texts_are_the_apps()
    {
        Assert.Equal(K.CancelFetchOrigin, DashboardTexts.CancelTitle(K.FetchOrigin));
        Assert.Equal(K.FetchOriginCancelled, OperationTexts.Cancelled(K.FetchOrigin));
        Assert.Equal(K.NoLongerRunning, AppCommands.CancelNoLongerRunning(K.FetchOrigin));
        Assert.Equal(K.FetchOrigin, OperationTexts.MenuTexts.FetchRemote(K.Origin));
        Assert.Equal(K.StageSelectedLines, OperationTexts.MenuTexts.StageSelectedLines);
        Assert.Equal(K.DiscardSelectedLines, OperationTexts.MenuTexts.DiscardSelectedLines);
        Assert.Equal(K.FromGitHub, OperationTexts.CloneTexts.FromGitHubButton);
        Assert.Equal(K.Ready, OperationStatusViewModel.ReadyText);
        Assert.Equal(K.CancelPrefix, DashboardTexts.CancelTitle(string.Empty));

        // The counters: 72 less the summary's length.
        Assert.Equal(K.FirstCounter, (WorkingChangesViewModel.SummaryLimit - K.First.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(K.NextCounter, (WorkingChangesViewModel.SummaryLimit - K.Next.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void K1s_numstat_lines_and_the_index_follow_from_the_scenarios_files()
    {
        var committed = Lines(Scenarios.ChangesFiles.CalculatorCommitted);
        var working = Lines(Scenarios.ChangesFiles.CalculatorWorking);
        var afterHunk = Lines(K.IndexAfterHunk);
        var afterLines = Lines(K.IndexAfterLines);

        // After the hunk: one line changed against HEAD (1 1), the four added lines still unstaged (4 0).
        Assert.Equal(committed.Count, afterHunk.Count);
        Assert.Single(committed.Zip(afterHunk), pair => pair.First != pair.Second);
        Assert.Contains("        return a + b + _offset;", afterHunk);
        Assert.Equal(working.Count - 4, afterHunk.Count);
        Assert.Equal(K.StagedAfterHunk, Numstat(1, 1));
        Assert.Equal(K.UnstagedAfterHunk, Numstat(4, 0));

        // After the two lines: the empty line and Negate added (3 1 staged), the second empty line and Square left (2 0).
        Assert.Equal(["    }", string.Empty, "    public int Negate(int a) => -a;", "}"], afterLines.TakeLast(4));
        Assert.Equal(afterHunk.Count + 2, afterLines.Count);
        Assert.Equal(working.Count - 2, afterLines.Count);
        Assert.Equal(K.StagedAfterLines, Numstat(3, 1));
        Assert.Equal(K.UnstagedAfterLines, Numstat(2, 0));
        Assert.EndsWith("\n    public int Square(int a) => a * a;\n}\n", Scenarios.ChangesFiles.CalculatorWorking, StringComparison.Ordinal);

        static string Numstat(int added, int removed) => $"{added} {removed} {K.Calculator}";
    }

    [Fact]
    public void K1s_walk_and_K2s_downs_follow_from_their_rows()
    {
        // Each row: its own button, its Stage, its Discard; src/Calculator.cs is the seventh row.
        Assert.Equal(7, K.UnstagedRows.Count);
        Assert.Equal(K.Calculator, K.UnstagedRows[^1]);
        Assert.Equal(K.TabsToCalculator, 3 * (K.UnstagedRows.Count - 1));
        Assert.Equal(["README.md", "config/settings.json", "src/helpers.py"], K.StagedRows);

        // From line 1, each Down a line: lines 2, 3, 4, the runs 0, 1 and 2.
        Assert.Equal(K.DownsToRun2, K.BlameDowns.Count);
        Assert.Equal([0, 1, 2], K.BlameDowns.Select(down => down.Run));
        Assert.Equal([2, 3, 4], K.BlameDowns.Select(down => down.Line));
        Assert.StartsWith(K.Run2Sha + " ", K.RunMenuHeader, StringComparison.Ordinal);
    }

    [Fact]
    public void The_real_windows_launches_are_the_twins_of_methods_1_and_2()
    {
        Assert.Equal(("Check_K1_hunk_by_keys", "changes", true, (1100, 700)), (K.ChangesLaunch.Method, K.ChangesLaunch.Scenario, K.ChangesLaunch.Dark, K.ChangesLaunch.Size));
        Assert.Equal([K.K1aHunkButton, K.K1bHunkStaged, K.K1cToolbar, K.K1dLinesMenu], K.ChangesLaunch.Captures);
        Assert.Equal(("Check_K2_blame_by_arrows", "history", false, (1100, 700)), (K.HistoryLaunch.Method, K.HistoryLaunch.Scenario, K.HistoryLaunch.Dark, K.HistoryLaunch.Size));
        Assert.Equal([K.K2aBlameRun, K.K2bBlameMenu], K.HistoryLaunch.Captures);
        Assert.All(K.ChangesLaunch.Captures.Concat(K.HistoryLaunch.Captures), name => Assert.Contains(name, K.Pictures));
    }

    private static List<string> Lines(string text) => [.. text.TrimEnd('\n').Split('\n')];
}
