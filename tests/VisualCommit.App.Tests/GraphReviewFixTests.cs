using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>Tests of what the review of phase 1's graph control and panels found.</summary>
public class GraphReviewFixTests
{
    private static readonly DateDisplay Utc = new(TimeZoneInfo.Utc);

    [AvaloniaFact]
    public void A_message_cut_short_shows_whole_in_a_tooltip_and_a_short_one_has_none()
    {
        var data = Linear(30, row => row == 1 ? "A very long subject that cannot possibly fit into the message column of a narrow graph" : $"Short {row}");
        var (window, control) = Show(data, 440, 400);

        Hover(window, control, control.Columns.Message.X + 20, control.RowBounds(1).Center.Y);
        Assert.Equal(data.CommitAt(1).Subject, ToolTip.GetTip(control));

        Hover(window, control, control.Columns.Message.X + 20, control.RowBounds(2).Center.Y);
        Assert.Null(ToolTip.GetTip(control));
        window.Close();
    }

    [AvaloniaFact]
    public void The_label_tooltip_follows_new_data_under_the_mouse()
    {
        var first = Linear(5, row => $"Row {row}", headLabel: "main");
        var second = Linear(5, row => $"Row {row}", headLabel: "renamed-branch");
        var (window, control) = Show(first, 900, 300);

        Hover(window, control, control.Columns.Refs.X + 12, control.RowBounds(0).Center.Y);
        Assert.Equal("main", ToolTip.GetTip(control));

        control.Data = second;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("renamed-branch", ToolTip.GetTip(control));
        window.Close();
    }

    [AvaloniaFact]
    public void A_row_revealed_near_the_end_of_what_has_loaded_is_centred_once_more_rows_arrive()
    {
        var all = Commits(400);
        var layout = new GraphLayout();
        var rows = all.Select(layout.Add).ToList();
        var data = new CommitGraphData(new RepoRefs(new HeadState("main", all[0].Sha), [], [], []));
        data.Append(all[..100], rows[..100]);
        var (window, control) = Show(data, 900, 300);

        control.Reveal(97);
        Dispatcher.UIThread.RunJobs();
        Assert.True(control.RowBounds(97).Center.Y > control.ViewportHeight / 2 + CommitGraphControl.RowHeight, "Row 97 cannot be centred yet.");

        data.Append(all[100..], rows[100..]);
        Dispatcher.UIThread.RunJobs();
        Assert.InRange(control.RowBounds(97).Center.Y - (control.ViewportHeight / 2), -CommitGraphControl.RowHeight, CommitGraphControl.RowHeight);
        window.Close();
    }

    [Fact]
    public async Task A_selected_stash_is_shown_under_its_new_name_after_another_stash_is_pushed()
    {
        using var repo = await Scenarios.GraphAsync();
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!);
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestContext.Current.CancellationToken);
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), Utc);
        await viewModel.StartAsync();
        viewModel.SelectedIndex = 0;
        await WaitForAsync(() => viewModel.Details.StashName == "stash@{0}");

        repo.WriteFile("README.md", "# Graph scenario\n\nAnother change.\n");
        await repo.StashAsync("A newer stash");
        await viewModel.RefreshAsync();
        await WaitForAsync(() => viewModel.Details.StashName == "stash@{1}");

        Assert.Equal("On main: Work in progress on README", viewModel.Graph.CommitAt(viewModel.SelectedIndex).Subject);
    }

    [AvaloniaFact]
    public async Task A_commit_landing_while_the_refs_are_read_never_shows_its_row_without_its_branchs_label()
    {
        // The phase 5 gate's f11f: a commit made in a terminal while a refresh reads the refs. The
        // reads run side by side; here for-each-ref lists main before the commit and rev-parse
        // HEAD reads HEAD after it, the order a busy machine can give them. Every graph swapped
        // in must draw main's label on main's commit: no row of the new commit without it, and
        // no label of before on the row below.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.CommitFileAsync("a.txt", "b\n", "Second");
        await repo.CreateBranchAsync("other", "HEAD~1");
        var armed = 0;
        var listed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        static bool IsRefList(GitCommand command) => command.Arguments is ["for-each-ref", _, "refs/heads", "refs/remotes", "refs/tags"];
        static bool IsHeadSha(GitCommand command) => command.Arguments is ["rev-parse", "-q", "--verify", "HEAD"];
        var runner = new OrderingRunner(
            new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!),
            before: async command =>
            {
                if (IsHeadSha(command) && Interlocked.CompareExchange(ref armed, 2, 1) == 1)
                {
                    await listed.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
                    await repo.CommitFileAsync("a.txt", "c\n", "Third");
                }
            },
            after: command =>
            {
                if (IsRefList(command) && Volatile.Read(ref armed) > 0)
                {
                    listed.TrySetResult();
                }
            });
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestContext.Current.CancellationToken);
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), Utc);
        await viewModel.StartAsync();
        var swapped = new List<CommitGraphData>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RepositoryViewModel.Graph))
            {
                swapped.Add(viewModel.Graph);
            }
        };

        Volatile.Write(ref armed, 1);
        await viewModel.RefreshAsync();
        Assert.Equal(2, Volatile.Read(ref armed));

        // The refresh the commit's own change brings (the watcher's, or this one).
        await viewModel.RefreshAsync();

        Assert.NotEmpty(swapped);
        Assert.Same(viewModel.Graph, swapped[^1]);
        var (window, control) = Show(swapped[0], 900, 300);
        foreach (var data in swapped)
        {
            control.Data = data;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            Assert.Equal(["Third", "Second", "First"], Enumerable.Range(0, data.Count).Select(row => data.CommitAt(row).Subject));
            Assert.Equal([("main", RefLabelKind.CurrentBranch)], control.LabelsInRow(0).Select(label => (label.FullText, label.Kind)));
            Assert.Empty(control.LabelsInRow(1));
            Assert.Equal([("other", RefLabelKind.LocalBranch)], control.LabelsInRow(2).Select(label => (label.FullText, label.Kind)));
        }

        window.Close();
    }

    [Fact]
    public void Opening_a_folder_inserts_its_rows_without_resetting_the_list()
    {
        var head = new string('a', 40);
        var refs = new RepoRefs(
            new HeadState("main", head),
            [
                new GitRef("refs/heads/main", "main", RefKind.LocalBranch, head, IsHead: true),
                .. Enumerable.Range(1, 40).Select(i => new GitRef($"refs/tags/group/v{i}", $"group/v{i}", RefKind.Tag, head)),
            ],
            [],
            []);
        var panel = new LeftPanelViewModel();
        panel.Update(refs);
        var changes = new List<NotifyCollectionChangedAction>();
        panel.Rows.CollectionChanged += (_, e) => changes.Add(e.Action);

        var folder = panel.Rows.OfType<RefItemRow>().Single(row => row.Text == "group");
        panel.Activate(folder);

        // The folder's own row changes (its chevron opens) and its 40 tags come in under it; the
        // rows above and below are left alone.
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, changes);
        Assert.Equal(1, changes.Count(action => action == NotifyCollectionChangedAction.Remove));
        Assert.Equal(41, changes.Count(action => action == NotifyCollectionChangedAction.Add));
        Assert.True(panel.Rows.OfType<RefItemRow>().Single(row => row.Text == "group").IsOpen);
    }

    private static (Window Window, CommitGraphControl Control) Show(CommitGraphData data, double width, double height)
    {
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        var window = new Window { Width = width, Height = height, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
        return (window, control);
    }

    private static void Hover(Window window, CommitGraphControl control, double x, double y)
    {
        window.MouseMove(control.TranslatePoint(new Point(x, y), window)!.Value);
        Dispatcher.UIThread.RunJobs();
    }

    private static CommitGraphData Linear(int count, Func<int, string> subject, string? headLabel = null)
    {
        var commits = Commits(count, subject);
        var layout = new GraphLayout();
        var rows = commits.Select(layout.Add).ToList();
        GitRef[] refs = headLabel is null ? [] : [new GitRef("refs/heads/" + headLabel, headLabel, RefKind.LocalBranch, commits[0].Sha, IsHead: true)];
        var data = new CommitGraphData(new RepoRefs(new HeadState(headLabel, commits[0].Sha), refs, [], []));
        data.Append(commits, rows);
        data.Complete();
        return data;
    }

    private static List<CommitInfo> Commits(int count, Func<int, string>? subject = null)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        return
        [
            .. Enumerable.Range(0, count).Select(row => new CommitInfo(
                Sha(row),
                row + 1 < count ? [Sha(row + 1)] : [],
                "Test Author",
                "author@example.com",
                date.AddMinutes(-row),
                date.AddMinutes(-row),
                subject?.Invoke(row) ?? $"Commit {row}")),
        ];

        static string Sha(int row) => row.ToString("x40", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 2400 && !condition(); i++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "The condition did not come true within 60 seconds.");
    }

    /// <summary>A runner that runs <paramref name="before"/> ahead of each call and <paramref name="after"/> once it has ended: the order of calls that run side by side, made certain.</summary>
    private sealed class OrderingRunner(IGitRunner real, Func<GitCommand, Task> before, Action<GitCommand> after) : IGitRunner
    {
        public string ExecutablePath => real.ExecutablePath;

        public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            await before(command);
            var result = await real.RunAsync(command, cancellationToken);
            after(command);
            return result;
        }
    }
}
