using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The comparison's view model (docs/phase-5-design.md 6.3, 6.4, D177, report part F's FD9, FD10,
/// FD20) with a fake repository: what it asks git for each kind, the counts in both forms, the
/// names resolved once and the switch and the swap read by them, each list kept, what it says
/// about unrelated histories, several merge bases, skipped renames and one commit on both sides,
/// the links disabled for a commit the graph lacks, a file's diff from the side compared from, and
/// the flat or tree setting shared with the details panel.
/// </summary>
public class CompareViewModelTests
{
    private const string Main = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";
    private const string Feature = "245c395674db6ae89390be2db018db5356b446fe";
    private const string Base = "14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c";

    [Fact]
    public async Task Two_commits_compare_their_trees_without_counts_and_swap_reads_them_the_other_way()
    {
        var host = new FakeHost();
        host.Result = (from, to, _, _) => new CompareResult(from, to, CompareBase.Trees, from, [new ChangedFile("src/Reader.cs", FileChangeKind.Modified)], false, null);
        using var comparison = new CompareViewModel(host, new CompareSide(Feature, "245c395 Compare whole lines"), new CompareSide(Main, "c0bc2dd Read whole lines"), isBranches: false);
        Assert.True(comparison.IsLoading);
        Assert.Equal("Compare", comparison.Title);

        await comparison.LoadAsync();
        Assert.Equal((Feature, Main, false, false), Assert.Single(host.Calls));
        Assert.False(comparison.IsLoading);
        Assert.Equal("From 245c395 Compare whole lines", comparison.FromText);
        Assert.Equal("To c0bc2dd Read whole lines", comparison.ToText);
        Assert.False(comparison.HasCounts);
        Assert.Equal("Changed files (1)", comparison.ChangedFilesTitle);
        Assert.True(comparison.Compares(Main, Feature));
        Assert.True(comparison.CanRevealFrom);
        comparison.RevealFromCommand.Execute(null);
        Assert.Equal([Feature], host.Revealed);

        await comparison.SwapCommand.ExecuteAsync(null);
        Assert.Equal((Main, Feature, false, false), host.Calls[1]);
        Assert.Equal("From c0bc2dd Read whole lines", comparison.FromText);
        Assert.Equal("To 245c395 Compare whole lines", comparison.ToText);
        Assert.True(comparison.Compares(Main, Feature));

        // Swapping back reads nothing again.
        await comparison.SwapCommand.ExecuteAsync(null);
        Assert.Equal(2, host.Calls.Count);
    }

    [Fact]
    public async Task Branches_compare_three_dot_with_counts_and_the_switch_reads_the_trees_by_the_resolved_commits()
    {
        var host = new FakeHost();
        host.Result = (from, to, threeDot, count) => new CompareResult(
            Main,
            Feature,
            threeDot ? CompareBase.MergeBase : CompareBase.Trees,
            threeDot ? Base : Main,
            threeDot ? [new ChangedFile("README.md", FileChangeKind.Modified)] : [new ChangedFile("README.md", FileChangeKind.Modified), new ChangedFile("src/Config.cs", FileChangeKind.Modified)],
            false,
            count ? new CompareCounts(OnlyOnFrom: 7, OnlyOnTo: 2) : null);
        using var comparison = new CompareViewModel(host, new CompareSide("refs/heads/main", "main"), new CompareSide("refs/heads/feature/compare", "feature/compare"), isBranches: true);
        Assert.False(comparison.AllDifferences);

        await comparison.LoadAsync();
        Assert.Equal(("refs/heads/main", "refs/heads/feature/compare", true, true), Assert.Single(host.Calls));
        Assert.Equal("2 commits only on feature/compare, 7 only on main", comparison.CountsText);
        Assert.Equal(Base, comparison.Old);
        Assert.Equal(["README.md"], comparison.ChangedFiles.Select(file => file.Path));

        comparison.AllDifferences = true;
        await host.WhenCalledAsync(2);
        await WaitAsync(() => comparison.ChangedFiles.Count == 2);
        Assert.Equal((Main, Feature, false, false), host.Calls[1]);
        Assert.Equal("2 commits only on feature/compare, 7 only on main", comparison.CountsText);
        Assert.Equal(Main, comparison.Old);

        comparison.AllDifferences = false;
        await WaitAsync(() => comparison.ChangedFiles.Count == 1);
        Assert.Equal(2, host.Calls.Count);

        // A file's diff from the side the list was computed from (6.4).
        comparison.ActivateFileCommand.Execute(comparison.Files.Single(row => row.IsFile));
        Assert.Equal(("README.md", Base, Feature), Assert.Single(host.Diffs));

        comparison.CloseCommand.Execute(null);
        Assert.True(host.Closed);
    }

    [Fact]
    public void The_counts_use_the_singular_for_one()
    {
        Assert.Equal("1 commit only on feature/alpha, 3 only on main", CompareViewModel.Counts(new CompareCounts(3, 1), "feature/alpha", "main"));
        Assert.Equal("0 commits only on a, 1 only on b", CompareViewModel.Counts(new CompareCounts(1, 0), "a", "b"));
    }

    [Theory]
    [InlineData(CompareBase.NoCommonHistory, false, "No common history: showing all differences")]
    [InlineData(CompareBase.SeveralMergeBases, false, "Several merge bases: changes since 14a5c6c")]
    [InlineData(CompareBase.MergeBase, true, "Renames are shown as removed and added files.")]
    [InlineData(CompareBase.SeveralMergeBases, true, "Several merge bases: changes since 14a5c6c\nRenames are shown as removed and added files.")]
    public async Task What_the_comparison_says_about_itself(CompareBase kind, bool renamesSkipped, string notice)
    {
        var host = new FakeHost { Result = (_, _, _, _) => new CompareResult(Main, Feature, kind, Base, [], renamesSkipped, new CompareCounts(1, 1)) };
        using var comparison = new CompareViewModel(host, new CompareSide("refs/heads/main", "main"), new CompareSide("refs/heads/x", "x"), isBranches: true);
        await comparison.LoadAsync();
        Assert.Equal(notice, comparison.NoticeText);
        Assert.True(comparison.ShowsNoDifferences);

        // With no common history the list is the two trees: the switch is not offered (6.3, 15.9).
        Assert.Equal(kind != CompareBase.NoCommonHistory, comparison.OffersAllDifferences);
    }

    [Fact]
    public async Task The_counts_stay_through_the_switch_and_the_swaps()
    {
        var host = new FakeHost { Result = BranchResult };
        using var comparison = new CompareViewModel(host, new CompareSide("refs/heads/main", "main"), new CompareSide("refs/heads/feature/compare", "feature/compare"), isBranches: true);
        await comparison.LoadAsync();
        Assert.Equal(MainToFeature, comparison.CountsText);

        // All differences, then a swap and a swap back: each direction's counts (FD9, F13; 15.9).
        comparison.AllDifferences = true;
        await host.WhenCalledAsync(2);
        await WaitAsync(() => comparison.ChangedFiles.Count == 2);
        Assert.Equal(MainToFeature, comparison.CountsText);
        await comparison.SwapCommand.ExecuteAsync(null);
        Assert.Equal("7 commits only on main, 2 only on feature/compare", comparison.CountsText);
        await comparison.SwapCommand.ExecuteAsync(null);
        Assert.Equal(MainToFeature, comparison.CountsText);
    }

    [Fact]
    public async Task The_switch_ticked_while_the_first_read_runs_reads_the_counts_that_read_carried()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new FakeHost { Result = BranchResult, Hold = call => call == 1 ? first.Task : null };
        using var comparison = new CompareViewModel(host, new CompareSide("refs/heads/main", "main"), new CompareSide("refs/heads/feature/compare", "feature/compare"), isBranches: true);
        var loading = comparison.LoadAsync();
        await host.WhenCalledAsync(1);

        // The switch cancels the first read, which carried the counts: its own read asks for them.
        comparison.AllDifferences = true;
        await loading;
        await host.WhenCalledAsync(2);
        await WaitAsync(() => !comparison.IsLoading);
        Assert.Equal(("refs/heads/main", "refs/heads/feature/compare", false, true), host.Calls[1]);
        Assert.Equal(MainToFeature, comparison.CountsText);

        comparison.AllDifferences = false;
        await host.WhenCalledAsync(3);
        await WaitAsync(() => !comparison.IsLoading && comparison.ChangedFiles.Count == 1);
        Assert.Equal(MainToFeature, comparison.CountsText);
    }

    /// <summary>The counts' line of main compared with feature/compare.</summary>
    private const string MainToFeature = "2 commits only on feature/compare, 7 only on main";

    /// <summary>main and feature/compare compared either way, by name or by commit: three-dot lists one file, the trees two; the counts when asked.</summary>
    private static CompareResult BranchResult(string from, string to, bool threeDot, bool count)
    {
        var fromMain = from is Main or "refs/heads/main";
        return new CompareResult(
            fromMain ? Main : Feature,
            fromMain ? Feature : Main,
            threeDot ? CompareBase.MergeBase : CompareBase.Trees,
            threeDot ? Base : (fromMain ? Main : Feature),
            threeDot ? [new ChangedFile("README.md", FileChangeKind.Modified)] : [new ChangedFile("README.md", FileChangeKind.Modified), new ChangedFile("src/Config.cs", FileChangeKind.Modified)],
            false,
            count ? (fromMain ? new CompareCounts(OnlyOnFrom: 7, OnlyOnTo: 2) : new CompareCounts(OnlyOnFrom: 2, OnlyOnTo: 7)) : null);
    }

    [Fact]
    public async Task One_commit_on_both_sides_has_nothing_to_compare_and_a_commit_the_graph_lacks_has_no_link()
    {
        var host = new FakeHost { Result = (_, _, _, _) => new CompareResult(Main, Main, CompareBase.MergeBase, Main, [], false, new CompareCounts(0, 0)) };
        host.Rows.Remove(Main);
        using var comparison = new CompareViewModel(host, new CompareSide("refs/heads/main", "main"), new CompareSide("refs/tags/v1", "v1"), isBranches: true);
        await comparison.LoadAsync();
        Assert.Equal("Both are c0bc2dd: nothing to compare", comparison.NoticeText);
        Assert.False(comparison.HasFiles);
        Assert.False(comparison.ShowsNoDifferences);
        Assert.False(comparison.CanRevealFrom);
        Assert.Equal(CompareViewModel.NotInGraphToolTip, comparison.FromLinkToolTip);

        // The graph has it after a reload.
        host.Rows.Add(Main);
        comparison.UpdateLinks();
        Assert.True(comparison.CanRevealTo);
        Assert.Null(comparison.ToLinkToolTip);
    }

    [Fact]
    public async Task A_failed_read_says_why_and_the_list_follows_the_shared_flat_or_tree_setting()
    {
        var host = new FakeHost { Result = (_, _, _, _) => throw new GitException("git rev-parse", 128, "fatal: bad revision 'nothing'") };
        using var comparison = new CompareViewModel(host, new CompareSide("refs/heads/main", "main"), new CompareSide("nothing", "nothing"), isBranches: true);
        await comparison.LoadAsync();
        Assert.Equal("The comparison could not be read. fatal: bad revision 'nothing'", comparison.ErrorText);
        Assert.False(comparison.IsLoading);

        Assert.True(comparison.IsFlat);
        comparison.ShowTreeCommand.Execute(null);
        Assert.Equal(FileListMode.Tree, host.Settings.Current.FileList);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var tries = 0; !condition(); tries++)
        {
            Assert.True(tries < 500, "The condition did not come true.");
            await Task.Delay(10);
        }
    }

    private sealed class FakeHost : ICompareHost
    {
        public CancellationToken Lifetime => CancellationToken.None;

        public IAppLog Log { get; } = new RecordingLog();

        public ISettingsStore Settings { get; } = new MainWindowViewModelTests.FakeSettings();

        public Func<string, string, bool, bool, CompareResult> Result { get; set; } = (from, to, _, _) => new CompareResult(from, to, CompareBase.Trees, from, [], false, null);

        public List<(string From, string To, bool ThreeDot, bool Count)> Calls { get; } = [];

        public HashSet<string> Rows { get; } = new(StringComparer.Ordinal) { Main, Feature, Base };

        public List<string> Revealed { get; } = [];

        public List<(string Path, string Old, string To)> Diffs { get; } = [];

        public bool Closed { get; private set; }

        /// <summary>What a call waits for before it answers, by its number from 1; null answers at once.</summary>
        public Func<int, Task?>? Hold { get; set; }

        public async Task<CompareResult> CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken)
        {
            Calls.Add((from, to, threeDot, countCommits));
            if (Hold?.Invoke(Calls.Count) is { } hold)
            {
                await hold.WaitAsync(cancellationToken);
            }

            return Result(from, to, threeDot, countCommits);
        }

        public async Task WhenCalledAsync(int calls) => await WaitAsync(() => Calls.Count >= calls);

        public bool HasRow(string sha) => Rows.Contains(sha);

        public void Reveal(string sha) => Revealed.Add(sha);

        public void OpenDiff(CompareViewModel comparison, ChangedFile file, string old, string to) => Diffs.Add((file.Path, old, to));

        public void Close(CompareViewModel comparison) => Closed = true;
    }
}
