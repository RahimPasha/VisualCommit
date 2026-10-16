// Tests of Phase5Expectations.B.cs's data against itself and the report's formulas (the real-window
// pass leaves this file out: its name does not match Phase5Expectations*.cs).
using System.Globalization;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB;

namespace VisualCommit.VisualTests.Phase5;

public sealed class PartBExpectationTests
{
    [Fact]
    public void The_page_and_blame_points_are_the_conventions_numbers()
    {
        Assert.Equal(137, PageRowCentreY(0));
        Assert.Equal(345, PageRowCentreY(8));
        Assert.Equal(501, PageRowCentreY(AbortBackupRow));
        Assert.Equal(371, StashWordsPoint.Y);
        Assert.Equal(131.9, BlameLineCentreY(1), 1);
        Assert.Equal(179.4, BlameLineCentreY(4), 1);
        Assert.Equal(274.5, BlameLineCentreY(10), 1);
        Assert.Equal(322.0, BlameLineCentreY(13), 1);
        Assert.Equal(432.9, BlameLineCentreY(20), 1);
        Assert.Equal(464.6, BlameLineCentreY(22), 1);
        Assert.Equal(422.4, BlameColumnX(1), 1);
        Assert.Equal(480.0, BlameColumnX(9), 1);
        Assert.Equal(451.2, BlameColumnX(5), 1);
        Assert.Equal(GutterBounds.Right, BlameTextBounds.Left);
        Assert.True(GutterPadding + GutterShaWidth + GutterGap + GutterDateWidth + GutterPadding <= GutterBounds.Width, "BD12's parts do not fit the 130-wide gutter.");
        Assert.Equal(BackupTimeX.Right + 8, BackupWordsX.Left);
        Assert.Equal(BackupWordsX.Right + 8, BackupCommitX.Left);
        Assert.Equal(160, BackupCommitX.Right - BackupCommitX.Left);
        Assert.Equal(ReflogActionX.Right + 8, ReflogCommitX.Left);
        Assert.Equal(ReflogCommitX.Right + 8, ReflogLostMarkX.Left);
    }

    [Fact]
    public void The_menus_heights_follow_4a_from_their_items_and_separators()
    {
        Assert.Equal(StageFileMenuHeight, Height(StageFileMenu));
        Assert.Equal(FileMenuHeight, Height(CommitFileMenu("README.md")));
        Assert.Equal(FileMenuHeight, Height(ParserDiffMenu));
        Assert.Equal(HistoryRowMenuHeight, Height(HistoryRowMenu("1d5a7cf", "Add the parser")));
        Assert.Equal(UncommittedRunMenuHeight, Height(UncommittedRunMenu));
        Assert.Equal(RunMenuHeight, Height(RunMenu("0575c7f", "Rename the parser to reader")));
        Assert.Equal(LocalBranchesMenuHeight, Height(LocalBranchesMenu));
        Assert.Equal(EntryMenuHeight, Height(ShortcutEntryMenu));
        Assert.Equal(SourceMenuHeight, Height(SourceMenu));
        Assert.Equal(BackupMenuHeight, Height(BackupMenu("Before aborting", RestoreTheChanges)));
    }

    [Fact]
    public void The_history_graph_and_its_commits_agree()
    {
        Assert.Equal(14, H0.Count);
        Assert.Equal(H0.Count, H0.Select(row => row.ShortSha).Distinct().Count());
        Assert.Equal(("Read whole lines", "Explain the reader", "Rename the parser to reader", "Handle empty input", "Add the parser", "Initial commit"),
            (H0[ReadWholeLinesRow].Message, H0[ExplainTheReaderRow].Message, H0[RenameRow].Message, H0[HandleEmptyInputRow].Message, H0[AddTheParserRow].Message, H0[InitialCommitRow].Message));
        foreach (var (details, row) in new[] { (ReadWholeLines, ReadWholeLinesRow), (Rename, RenameRow), (HandleEmptyInput, HandleEmptyInputRow), (AddTheParser, AddTheParserRow), (InitialCommitAllFiles, InitialCommitRow), (ExplainTheReaderAllFiles, ExplainTheReaderRow) })
        {
            Assert.StartsWith(H0[row].ShortSha!, details.Sha, StringComparison.Ordinal);
            Assert.Equal(H0[row].Message, details.Subject);
            Assert.Equal(H0[row].Date, details.Date);
        }

        Assert.Equal(6, ReadWholeLinesAllFiles.Files.Count);
        Assert.Equal(["M Reader.cs src"], ReadWholeLinesAllFiles.Files.Where(file => file.StartsWith("M ", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_file_histories_follow_the_rename_newest_first()
    {
        Assert.Equal(["c0bc2dd", "0575c7f", "365482b", "1d5a7cf"], ReaderHistory.Select(row => row.ShortSha));
        Assert.Equal(ReaderHistory.Skip(1), ReaderHistoryFromRename);
        Assert.Equal(ReaderHistory.Skip(2), ParserHistory.Select((row, index) => index == 0 ? row with { Secondary = "as src/Parser.cs" } : row));
        Assert.All(ReaderHistory.Concat(ParserHistory), row => Assert.Contains(H0, graph => graph.ShortSha == row.ShortSha && graph.Message == row.Subject && graph.Date == row.Date));
        Assert.Equal("1d5a7cf Add the parser", ReaderHistory[3].Name);
    }

    [Fact]
    public void The_blames_runs_cover_every_line_once_in_order()
    {
        AssertCovers(ReaderWorkingTreeRuns, ReaderWorkingTree.Split('\n').Length);
        Assert.Equal(24, ReaderWorkingTree.Split('\n').Length);
        Assert.Equal([3, 22], ReaderWorkingTreeRuns.Where(run => run.ShortSha is null).Select(run => run.First));
        AssertCovers(ParserRuns, ParserLines);
        AssertCovers(LicenseRuns, License.Split('\n').Length);
        AssertCovers(ReadmeRuns, Readme.Split('\n').Length);
        Assert.Equal("        _text = text ?? string.Empty;", ReaderWorkingTree.Split('\n')[9]);
        Assert.Equal("    public string[] Lines()", ReaderWorkingTree.Split('\n')[12]);
        Assert.Equal(15, ReadWholeLinesHunk.Rows!.Count);
    }

    [Fact]
    public void Heads_reflog_has_36_entries_without_the_skipped_one_and_its_lost_marks()
    {
        Assert.Equal(36, HeadReflog.Count);
        Assert.Equal(Enumerable.Range(0, 37).Where(n => n != 5).Select(n => $"HEAD@{{{n}}}"), HeadReflog.Select(row => row.Selector));
        Assert.Equal(HeadLost, HeadReflog.Where(row => row.Lost).Select(row => row.Selector));
        Assert.Equal(HeadLost.Where(selector => selector != "HEAD@{9}"), HeadLostAfterShortcut);
        Assert.Equal("HEAD@{9}", HeadReflog[8].Selector);
        Assert.Equal(Enumerable.Range(0, 8).Select(n => $"main@{{{n}}}"), MainReflog.Select(row => row.Selector));
        Assert.Equal(["main@{3}"], MainReflog.Where(row => row.Lost).Select(row => row.Selector));
        Assert.Equal(["topic@{1}"], TopicReflog.Where(row => row.Lost).Select(row => row.Selector));
        Assert.All(HeadReflog, row => Assert.Matches(@"^2026-01-01 12:\d\d$", row.Time));
    }

    [Fact]
    public void The_backups_are_newest_first_and_their_times_come_from_their_names()
    {
        Assert.Equal(BackupsLeft, Backups.Count);
        var times = Backups.Select(row => BackupRef.Parse(BackupRef.Prefix + row.Name).Time).ToList();
        Assert.Equal(times.OrderDescending(), times);
        Assert.Equal(times.Select(time => time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)), Backups.Select(row => row.Time));
        Assert.Equal(FirstNewBackupTime, BackupRef.Parse(FirstNewBackup).Time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Assert.Equal(BackupRef.Prefix + FirstNewBackupName, FirstNewBackup);
        Assert.Equal(("branch", "remote-branch", "discard", "reset", "skip", "abort", "stash"),
            (Kind(DoomedBackupRow), Kind(RemoteBranchBackupRow), Kind(DiscardBackupRow), Kind(MixedResetBackupRow), Kind(SkipBackupRow), Kind(AbortBackupRow), Kind(StashBackupRow)));
        Assert.StartsWith(Backups[DiscardBackupRow].ShortSha, DiscardSnapshot.Sha, StringComparison.Ordinal);
        Assert.StartsWith(Backups[DoomedBackupRow].ShortSha, DoomedWorkSha, StringComparison.Ordinal);
    }

    [Fact]
    public void The_graphs_after_the_actions_keep_RL0s_rows()
    {
        Assert.Equal(RL0.Count + 1, RLB8.Count);
        Assert.Equal(RL0.Count + 1, RLB9a.Count);
        Assert.Equal(RL0.Select(row => row.Message), RLB8.Where((_, index) => index != 1).Select(row => row.Message));
        Assert.Equal(RL0.Select(row => row.Message), RLB9a.Skip(1).Select(row => row.Message));
        Assert.Equal(["origin/feature/gone"], RLB9b[8].Labels);
        Assert.Equal("Add the app", RLB9b[8].Message);
        Assert.StartsWith(RLB8[1].ShortSha!, ShortcutSha, StringComparison.Ordinal);
        Assert.StartsWith(RLB9a[0].ShortSha!, DoomedWorkSha, StringComparison.Ordinal);
    }

    private static string Kind(int row) => BackupRef.Parse(BackupRef.Prefix + Backups[row].Name).Kind;

    private static double Height(IReadOnlyList<string> lines) =>
        MenuHeight(lines.Skip(1).Count(line => line != "—"), lines.Count(line => line == "—"));

    private static void AssertCovers(IReadOnlyList<BlameRun> runs, int lines)
    {
        Assert.Equal(1, runs[0].First);
        Assert.Equal(lines, runs[^1].Last);
        for (var index = 1; index < runs.Count; index++)
        {
            Assert.Equal(runs[index - 1].Last + 1, runs[index].First);
            Assert.NotEqual(runs[index - 1].ShortSha, runs[index].ShortSha);
        }
    }
}
