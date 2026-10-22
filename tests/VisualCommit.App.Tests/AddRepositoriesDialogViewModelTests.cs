using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.WorkspaceTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The "Add repositories" checklist (docs/phase-6-design.md 1.8, 1.9): the scan of a folder, each
/// candidate confirmed by git (4 at a time), the rows by relative path with their texts, the
/// refusals and the limit, the confirm button's count and the answer, Cancel during the search,
/// the searching state the scripted app cannot hold for a picture, and the card as drawn.
/// </summary>
public class AddRepositoriesDialogViewModelTests
{
    [Fact]
    public async Task A_tree_of_repositories_is_listed_by_relative_path_with_the_refused_ones_counted()
    {
        using var tree = await TreeAsync();
        await tree.InitAsync("web-app");
        await tree.InitAsync("group/docs");
        await tree.Api.GitAsync("worktree", "add", "--quiet", "-b", "wt", tree.PathOf("wt"));
        Directory.CreateDirectory(Path.Combine(tree.Folder("empty"), ".git"));
        File.WriteAllText(Path.Combine(tree.Folder("broken"), ".git"), "gitdir: ./nowhere\n");
        await tree.InitAsync("node_modules/left-out");
        await tree.InitAsync("a/b/c/d/too-deep");
        await tree.Api.GitAsync("init", "--quiet", "--bare", tree.PathOf("remotes/api.git"));
        var session = NewSession();
        var held = await GitTopAsync(tree.Api.Path);

        var dialog = new AddRepositoriesDialogViewModel("Web", tree.Root, session.Services.Repositories, path => WorkspacePaths.Same(path, held), log: session.Log);
        await dialog.Start();

        Assert.Equal("Add repositories to Web", dialog.Title);
        Assert.False(dialog.IsSearching);
        Assert.Equal($"Found 4 repositories in {tree.Root}.", dialog.FolderText);
        Assert.False(dialog.ShowsProgress);
        Assert.True(dialog.ShowsList);
        Assert.Equal(
            [("api", "Already in this workspace", true, false), ("docs", Path.Combine("group", "docs"), true, true), ("web-app", "web-app", true, true), ("wt", "wt", true, true)],
            dialog.Rows.Select(row => (row.Name, row.Detail, row.IsChecked, row.IsEnabled)));
        Assert.Equal("2 more folders look like repositories, but git could not open them.", dialog.FailedText);
        Assert.Equal("broken\nempty", dialog.FailedToolTip);
        Assert.Null(dialog.StoppedText);
        Assert.Equal(("Add 3 repositories", true), (dialog.ConfirmText, dialog.CanConfirm));

        // Each candidate git opened was logged as a tab's open is (1.8).
        Assert.Equal(4, session.Log.Messages.Count(message => message.StartsWith("Opened the repository at ", StringComparison.Ordinal)));

        dialog.Rows[1].IsChecked = false;
        Assert.Equal("Add 2 repositories", dialog.ConfirmText);
        dialog.ConfirmCommand.Execute(null);
        var answer = await dialog.Result;
        Assert.NotNull(answer);
        Assert.Equal(2, answer.Count);
        // The working trees as git reports them (on macOS /private/var/…, where the scan saw /var/…).
        Assert.True(WorkspacePaths.Same(await GitTopAsync(tree.PathOf("web-app")), answer[0]));
        Assert.True(WorkspacePaths.Same(await GitTopAsync(tree.PathOf("wt")), answer[1]));
    }

    [Fact]
    public async Task A_folder_inside_a_working_tree_lists_its_repository_first_and_unchecked_and_an_empty_git_inside_it_is_refused()
    {
        using var tree = await TreeAsync();
        var inside = Directory.CreateDirectory(Path.Combine(tree.Api.Path, "tools")).FullName;
        Directory.CreateDirectory(Path.Combine(inside, "emptygit", ".git"));
        var session = NewSession();
        var enclosing = await session.Services.Repositories.OpenAsync(inside, TestContext.Current.CancellationToken);
        Assert.False(await enclosing.OpenedAtTopAsync(TestContext.Current.CancellationToken));

        var dialog = new AddRepositoriesDialogViewModel("Web", inside, session.Services.Repositories, _ => false, enclosing);
        await dialog.Start();

        Assert.Equal($"No other repositories were found in {inside}.", dialog.FolderText);
        var row = Assert.Single(dialog.Rows);
        Assert.Equal(("api", "Contains tools", false, true, true), (row.Name, row.Detail, row.IsChecked, row.IsEnabled, row.IsEnclosing));
        Assert.Equal("1 more folder looks like a repository, but git could not open it.", dialog.FailedText);
        Assert.Equal("emptygit", dialog.FailedToolTip);
        Assert.Equal(("Add repositories", false), (dialog.ConfirmText, dialog.CanConfirm));

        // The repository holding the folder counts once checked.
        row.IsChecked = true;
        Assert.Equal(("Add 1 repository", true), (dialog.ConfirmText, dialog.CanConfirm));
    }

    [Fact]
    public async Task A_tree_whose_repositories_are_all_in_the_workspace_gives_the_disabled_add_repositories()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        files.Add(root, "api", repository: true);
        files.Add(root, "docs", repository: true);

        var dialog = new AddRepositoriesDialogViewModel("Web", root, new GatedRepositories(), _ => true, fileSystem: files);
        await dialog.Start();

        Assert.Equal($"Found 2 repositories in {root}.", dialog.FolderText);
        Assert.All(dialog.Rows, row => Assert.Equal(("Already in this workspace", true, false), (row.Detail, row.IsChecked, row.IsEnabled)));
        Assert.Equal(("Add repositories", false), (dialog.ConfirmText, dialog.CanConfirm));
    }

    [Fact]
    public async Task A_single_addable_row_says_add_1_repository_and_the_answer_leaves_out_the_rows_already_in()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        var api = files.Add(root, "api", repository: true);
        var docs = files.Add(root, "docs", repository: true);

        var dialog = new AddRepositoriesDialogViewModel("Web", root, new GatedRepositories(), path => path == api, fileSystem: files);
        await dialog.Start();

        Assert.Equal(("Add 1 repository", true), (dialog.ConfirmText, dialog.CanConfirm));
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal([docs], await dialog.Result);
    }

    [Fact]
    public async Task One_repository_and_none_have_their_own_texts()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        files.Add(root, "api", repository: true);
        var one = new AddRepositoriesDialogViewModel("Web", root, new GatedRepositories(), _ => false, fileSystem: files);
        await one.Start();
        Assert.Equal($"Found 1 repository in {root}.", one.FolderText);

        var none = new AddRepositoriesDialogViewModel("Web", Path.Combine(root, "api"), new GatedRepositories(), _ => false, fileSystem: files);
        await none.Start();
        Assert.Equal($"No repositories were found in {Path.Combine(root, "api")}.", none.FolderText);
        Assert.False(none.ShowsList);
        Assert.Null(none.FailedText);
    }

    [Fact]
    public async Task The_searching_state_says_where_it_looks_and_how_far_it_has_come()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        var api = files.Add(root, "api", repository: true);
        var docs = files.Add(root, "docs", repository: true);
        files.SlowAt(api);
        files.HoldAt(docs);

        var dialog = new AddRepositoriesDialogViewModel("Web", root, new GatedRepositories(), _ => false, fileSystem: files);
        Assert.Equal($"Looking for repositories in {root}…", dialog.FolderText);
        Assert.Equal("Searched 0 folders, found 0 repositories", dialog.ProgressText);
        var search = dialog.Start();
        try
        {
            await WaitUntilAsync(() => dialog.ProgressText == "Searched 2 folders, found 1 repository", "the scan's progress");
            Assert.True(dialog.IsSearching);
            Assert.True(dialog.ShowsProgress);
            Assert.False(dialog.ShowsList);
            Assert.Equal($"Looking for repositories in {root}…", dialog.FolderText);
            Assert.Equal(("Add repositories", false), (dialog.ConfirmText, dialog.CanConfirm));
        }
        finally
        {
            files.Release();
        }

        await search;
        Assert.False(dialog.ShowsProgress);
        Assert.Equal($"Found 2 repositories in {root}.", dialog.FolderText);
    }

    [Fact]
    public async Task Cancel_during_the_search_stops_it_opens_nothing_and_adds_nothing()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        var api = files.Add(root, "api", repository: true);
        files.Add(root, "docs", repository: true);
        files.HoldAt(api);
        var provider = new GatedRepositories();

        var dialog = new AddRepositoriesDialogViewModel("Web", root, provider, _ => false, fileSystem: files);
        var search = dialog.Start();
        await WaitUntilAsync(() => files.Waiting == 1, "the scan to reach api");
        dialog.CancelCommand.Execute(null);
        files.Release();
        await search;

        Assert.Null(await dialog.Result);
        Assert.True(dialog.IsClosed);
        Assert.Equal(0, provider.Opens);
        Assert.Empty(dialog.Rows);
    }

    [Fact]
    public async Task At_most_four_candidates_are_opened_at_once()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        for (var i = 0; i < 10; i++)
        {
            files.Add(root, $"repo{i}", repository: true);
        }

        var provider = new GatedRepositories { Hold = true };
        var dialog = new AddRepositoriesDialogViewModel("Web", root, provider, _ => false, fileSystem: files);
        var search = dialog.Start();
        await WaitUntilAsync(() => provider.Waiting == 4, "four opens");
        Assert.Equal(4, provider.Opens);

        // Each that ends lets the next start, never a fifth beside four.
        provider.Release(1);
        await WaitUntilAsync(() => provider.Opens == 5 && provider.Waiting == 4, "the fifth open");
        provider.Release(3);
        await WaitUntilAsync(() => provider.Opens == 8 && provider.Waiting == 4, "the eighth open");
        provider.Release();
        await WaitUntilAsync(() => provider.Opens == 10, "every open");
        provider.Release();
        await search;

        Assert.Equal(4, provider.MostAtOnce);
        Assert.Equal(10, dialog.Rows.Count);
        Assert.Equal("Add 10 repositories", dialog.ConfirmText);
    }

    [Fact]
    public async Task A_search_that_stops_at_10000_folders_says_so()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        for (var i = 0; i < 120; i++)
        {
            for (var j = 0; j < 100; j++)
            {
                files.Add(root, $"f{i:000}/g{j:000}");
            }
        }

        files.Add(root, "aaa/repo", repository: true);
        var dialog = new AddRepositoriesDialogViewModel("Web", root, new GatedRepositories(), _ => false, fileSystem: files);
        await dialog.Start();

        Assert.Equal("Stopped after 10,000 folders: choose a folder further down to search the rest.", dialog.StoppedText);
        Assert.Equal(["repo"], dialog.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task The_refusals_tooltip_lists_at_most_ten_then_an_ellipsis()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");
        var files = new FakeScanFileSystem();
        var provider = new GatedRepositories();
        for (var i = 0; i < 12; i++)
        {
            provider.Refused.Add(files.Add(root, $"r{i:00}", repository: true));
        }

        var dialog = new AddRepositoriesDialogViewModel("Web", root, provider, _ => false, fileSystem: files);
        await dialog.Start();

        Assert.Equal("12 more folders look like repositories, but git could not open them.", dialog.FailedText);
        Assert.Equal(string.Join('\n', Enumerable.Range(0, 10).Select(i => $"r{i:00}").Append("…")), dialog.FailedToolTip);
        Assert.False(dialog.ShowsList);
        Assert.Equal($"No repositories were found in {root}.", dialog.FolderText);
    }

    [AvaloniaFact]
    public async Task The_checklist_is_drawn_on_the_wide_card_with_eight_rows_in_view()
    {
        var root = @"C:\Users\someone\AppData\Local\Temp\VisualCommit.Tests\workspace-0123456789ab";
        if (!OperatingSystem.IsWindows())
        {
            root = "/tmp/VisualCommit.Tests/workspace-0123456789ab";
        }

        var files = new FakeScanFileSystem();
        foreach (var name in new[] { "api", "billing", "design-system", "docs", "infra", "mobile", "notes", "scratch", "web-app" })
        {
            files.Add(root, name, repository: true);
        }

        var dialog = new AddRepositoriesDialogViewModel("Web", root, new GatedRepositories(), path => path.EndsWith("api", StringComparison.Ordinal), fileSystem: files);
        await dialog.Start();
        var view = new AddRepositoriesDialogView { DataContext = dialog, FontSize = 12.5 };
        using var host = DiffHost.Show(view, 1100, 700, ThemeVariant.Dark);

        var card = Named<Border>(view, "AddRepositoriesCard");
        Assert.Equal(460, card.Bounds.Width, 0.5);
        Assert.Equal(320, host.ToWindow(card, default).X, 0.5);
        var list = view.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.Equal(232, list.Bounds.Height, 0.5);
        var rows = view.GetVisualDescendants().OfType<DialogRow>().ToList();
        Assert.Equal(9, rows.Count);
        Assert.All(rows, row => Assert.Equal(28, row.Bounds.Height, 0.5));
        var listTop = host.ToWindow(list, default).Y;
        Assert.Equal(8, rows.Count(row => host.ToWindow(row, default).Y + 28 <= listTop + 232 + 0.5));
        Assert.Equal("api", Avalonia.Automation.AutomationProperties.GetName(rows[0]));

        // The folder's line breaks only after a separator (13.2).
        var folderLines = DrawnLines.Of(Named<TextBlock>(view, "ScanFolderText"));
        Assert.All(folderLines.Take(folderLines.Count - 1), line => Assert.True(line.EndsWith('\\') || line.EndsWith('/') || line.EndsWith(' '), $"The line \"{line}\" ends inside a name."));
        Assert.Equal($"Found 9 repositories in {root}.", string.Concat(folderLines));
        Assert.Equal("Add 8 repositories", Named<Button>(view, "ConfirmButton").Content);
        TestPictures.Save(host, "add-repositories");
    }

    private static T Named<T>(Visual scope, string name)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    /// <summary>Waits until <paramref name="condition"/> holds, looking every 10 ms, for at most the harness's wait.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > ShellDriver.DefaultWaitTimeout)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(10);
        }
    }
}
