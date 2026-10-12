using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.Views.Conflicts;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The resolver on the conflicts scenario's stops as git leaves them (docs/phase-4-design.md 13.1;
/// the report's FM, PM and "The kinds merge"), read with the real git layer (WP3's reads), shown
/// where the app shows it at 1100×700: across the graph and right-panel columns, under a one-line
/// banner. With <c>VISUALCOMMIT_TEST_PICTURES</c> set, its pictures are saved for a person to look at.
/// </summary>
public class ConflictResolverRealGitTests
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    [AvaloniaFact]
    public async Task The_merge_stop_s_notes_txt_reads_as_the_report_s_FM_and_PM()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        var (resolver, status) = await OpenAsync(repo, "notes.txt");

        Assert.Equal(Scenarios.ConflictsFiles.NotesMergeConflicted, resolver.Document!.Text);
        Assert.Equal(("Ours: main", "Theirs: feature/theirs", "2 conflicts left", "Conflict 1 of 2"), (resolver.OursTitle, resolver.TheirsTitle, resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal(("Both modified", "File 1 of 1"), (resolver.KindText, resolver.FilePositionText));

        using var shell = AppLikeWindow.Show(resolver, status);
        var shot = shell.Host.Capture();
        TestPictures.Save(shell.Host, "resolver-merge-1100x700");
        var panes = shell.View.ConflictPanes;
        Assert.Equal(
            ["line 1", string.Empty, "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9", string.Empty, "line 10 ours", "line 11", "line 12"],
            panes.RowsOf(ConflictPaneSide.Ours).Select(row => row.Text));
        Assert.Equal("line 10 theirs", panes.RowsOf(ConflictPaneSide.Theirs)[11].Text);

        // The resolver spans x 260 to 1100 under the banner.
        var bounds = shell.Host.ToWindow(shell.View, default);
        Assert.Equal(260, bounds.X, 0.5);
        Assert.Equal(840, shell.View.Bounds.Width, 0.5);
        Assert.Equal(700 - 26, bounds.Y + shell.View.Bounds.Height, 0.5);
        Assert.NotNull(shot);

        // Take theirs reads stage 3 in its working-tree form (blob 6216606).
        await resolver.TakeTheirsCommand.ExecuteAsync(null);
        Assert.Equal(Scenarios.ConflictsFiles.NotesTheirs, resolver.Document.Text);
        Assert.Equal("No conflicts left", resolver.ResultCountText);
    }

    [AvaloniaFact]
    public async Task The_kinds_merge_shows_the_deleted_by_them_and_the_binary_cards()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);
        var (resolver, status) = await OpenAsync(repo, "other.txt");

        Assert.Equal(ConflictResolverBody.Card, resolver.Body);
        Assert.Equal(("other.txt", "Deleted by them", "File 3 of 3"), (resolver.FileName, resolver.KindText, resolver.FilePositionText));
        Assert.Equal("main changed other.txt; feature/kinds deleted it.", resolver.CardText);

        using var shell = AppLikeWindow.Show(resolver, status);
        shell.Host.Capture();
        TestPictures.Save(shell.Host, "resolver-kinds-deleted-by-them-1100x700");

        await resolver.OpenAsync("image.png");
        shell.Host.Capture();
        TestPictures.Save(shell.Host, "resolver-kinds-binary-1100x700");
        Assert.Equal(("image.png", "Both modified", "File 1 of 3"), (resolver.FileName, resolver.KindText, resolver.FilePositionText));
        Assert.Equal("image.png is a binary file: its two versions cannot be merged line by line.", resolver.CardText);
        Assert.True(resolver.ShowsMergeTool);

        await resolver.OpenAsync("new.txt");
        shell.Host.Capture();
        TestPictures.Save(shell.Host, "resolver-kinds-both-added-1100x700");
        Assert.Equal(Scenarios.ConflictsFiles.NewKindsConflicted, resolver.Document!.Text);
        Assert.Equal(("Ours: main", "Theirs: feature/kinds", "1 conflict left"), (resolver.OursTitle, resolver.TheirsTitle, resolver.ResultCountText));
    }

    [AvaloniaFact]
    public async Task A_diff3_file_shows_its_base_sections_in_the_result_and_none_in_the_panes_in_the_light_theme()
    {
        using var repo = await Scenarios.ConflictsAsync();
        await repo.ConfigAsync("merge.conflictStyle", "diff3");
        await repo.GitAllowingFailureAsync("merge", "feature/theirs");
        var (resolver, status) = await OpenAsync(repo, "notes.txt");

        Assert.Equal(24, resolver.Document!.LineCount - 1);
        using var shell = AppLikeWindow.Show(resolver, status, 1920, 1080, ThemeVariant.Light);
        shell.Host.Capture();
        TestPictures.Save(shell.Host, "resolver-diff3-light-1920x1080");

        var editor = shell.View.ResultEditor;
        Assert.Equal(
            [ConflictTint.None, ConflictTint.Marker, ConflictTint.Ours, ConflictTint.Marker, ConflictTint.Base, ConflictTint.Marker, ConflictTint.Theirs, ConflictTint.Marker, ConflictTint.None],
            Enumerable.Range(1, 9).Select(editor.TintAt));
        Assert.Equal(ConflictTint.Base, editor.TintAt(19));
        Assert.Equal(
            ["line 1", string.Empty, "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9", string.Empty, "line 10 ours", "line 11", "line 12"],
            shell.View.ConflictPanes.RowsOf(ConflictPaneSide.Ours).Select(row => row.Text));
        Assert.Equal(("2 conflicts left", "Conflict 1 of 2"), (resolver.ResultCountText, resolver.ConflictPositionText));
    }

    [AvaloniaFact]
    public async Task A_file_git_moved_aside_for_a_folder_and_reports_as_modify_delete_shows_the_moved_aside_card()
    {
        // Ours changed the file a, theirs replaced it with the folder a/: git moves ours' file to
        // a~HEAD and reports it as deleted by them (14.2).
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("keep.txt", "keep\n").WriteFile("a", "base\n");
        await repo.CommitAsync("Base");
        await repo.CreateBranchAsync("feature");
        repo.WriteFile("a", "ours\n");
        await repo.CommitAsync("Ours");
        await repo.CheckoutAsync("feature");
        repo.DeleteFile("a").WriteFile("a/x", "theirs\n");
        await repo.CommitAsync("Theirs");
        await repo.CheckoutAsync("main");
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge", "--no-edit", "feature")).ExitCode);

        var (resolver, status) = await OpenAsync(repo, "a~HEAD");

        Assert.Equal(ConflictKind.DeletedByThem, Assert.Single(status.Conflicts).Kind);
        Assert.Equal((ConflictResolverBody.Card, ConflictCardKind.MovedAside), (resolver.Body, resolver.CardKind));
        Assert.Equal("Git moved a~HEAD aside because feature has a folder named a: rename or remove it, then mark it resolved.", resolver.CardText);
        Assert.Equal(["Mark resolved"], resolver.CardButtons.Select(button => button.Label));
    }

    private static async Task<(ConflictResolverViewModel Resolver, WorkingTreeStatus Status)> OpenAsync(TempRepo repo, string path)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var status = await repository.ReadStatusAsync(TestContext.Current.CancellationToken);
        var resolver = new ConflictResolverViewModel(new ReadingActions(repository));
        resolver.Update(status, "main", null);
        await resolver.OpenAsync(path);
        return (resolver, status);
    }

    /// <summary>The repository's reads of a conflict; this test writes nothing.</summary>
    private sealed class ReadingActions(IGitRepository repository) : IConflictActions
    {
        public Task<ConflictContent> ReadAsync(ConflictEntry entry) => repository.ReadConflictAsync(entry);

        public Task<byte[]?> ReadSideAsync(ConflictEntry entry, ConflictSide side) => repository.ReadConflictSideAsync(entry, side);

        public Task<bool> ConfirmAsync(ConfirmationRequest request) => Task.FromResult(false);

        public Task<MarkResolvedResult> MarkResolvedAsync(ConflictEntry entry, byte[] content, byte[]? expected) => throw new NotSupportedException();

        public MergeToolRun? RunningMergeTool => null;

        public string? MergedRemoteBranch(string branch, string? mergeHead) => null;

        public event EventHandler<MergeToolRun>? MergeToolStarted
        {
            add { }
            remove { }
        }

        public Task<bool> TakeSideAsync(string path, ConflictSide side) => throw new NotSupportedException();

        public Task<bool> RemoveAsync(string path) => throw new NotSupportedException();

        public Task<bool> RunMergeToolAsync(string path) => throw new NotSupportedException();
    }

    /// <summary>
    /// The resolver where the app shows it at 1100×700, dark: the tab strip (36), the toolbar (52)
    /// and the status bar (26) as plain strips, the left panel (260), and over the resolver the
    /// real banner for the status's operation (one line at 840).
    /// </summary>
    private sealed class AppLikeWindow : IDisposable
    {
        private AppLikeWindow(DiffHost host, ConflictResolverView view)
        {
            Host = host;
            View = view;
        }

        public DiffHost Host { get; }

        public ConflictResolverView View { get; }

        public static AppLikeWindow Show(ConflictResolverViewModel resolver, WorkingTreeStatus status, double width = 1100, double height = 700, ThemeVariant? theme = null)
        {
            var banner = new ConflictBannerViewModel(_ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask);
            banner.Show(status.InProgress, status.Conflicts.Count, null);
            var view = new ConflictResolverView { DataContext = resolver };
            var area = new DockPanel();
            var bannerView = new ConflictBanner { DataContext = banner };
            DockPanel.SetDock(bannerView, Dock.Top);
            area.Children.Add(bannerView);
            area.Children.Add(view);
            Grid.SetColumn(area, 1);

            var main = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*") };
            main.Children.Add(Strip("VcPanelBackgroundBrush"));
            main.Children.Add(area);
            Grid.SetRow(main, 2);

            var root = new Grid { RowDefinitions = new RowDefinitions("36,52,*,26") };
            root.Children.Add(Strip("VcChromeBackgroundBrush"));
            var toolbar = Strip("VcChromeBackgroundBrush");
            Grid.SetRow(toolbar, 1);
            root.Children.Add(toolbar);
            root.Children.Add(main);
            var statusBar = Strip("VcChromeBackgroundBrush");
            Grid.SetRow(statusBar, 3);
            root.Children.Add(statusBar);

            var host = DiffHost.Show(root, width, height, theme ?? ThemeVariant.Dark);
            return new AppLikeWindow(host, view);
        }

        public void Dispose() => Host.Dispose();

        private static Border Strip(string brush)
        {
            var border = new Border();
            border.Bind(Border.BackgroundProperty, border.GetResourceObservable(brush));
            return border;
        }
    }
}
