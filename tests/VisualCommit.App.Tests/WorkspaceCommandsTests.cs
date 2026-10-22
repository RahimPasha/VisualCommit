using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.WorkspaceTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// Every workspace flow, text and reason of docs/phase-6-design.md 1.5 to 1.10, run with real git
/// behind the provider and a fake folder dialog: create, open, rename, delete; Add repository… on a
/// top, on a folder that is none, on git's refusal; Add open tabs; Remove; Move up and down;
/// Locate…; every edit disabled with its reason while the store is read-only.
/// </summary>
public class WorkspaceCommandsTests
{
    private static readonly CancellationToken Cancelled = TestContext.Current.CancellationToken;

    // ---- Create, open, rename, delete (1.6). ----

    [Fact]
    public async Task New_workspace_creates_it_from_the_name_dialog_and_opens_it_from_the_tab()
    {
        var session = NewSession();
        var tab = new RepoTabViewModel(session.Services);
        Assert.Null(session.Commands.CreateReason);

        var creating = session.Commands.CreateAsync(tab);
        var dialog = await session.DialogAsync<WorkspaceNameDialogViewModel>();
        Assert.Equal("New workspace", dialog.Title);
        Assert.Null(dialog.RepositoryName);
        dialog.Name = "  Web  ";
        dialog.ConfirmCommand.Execute(null);
        var id = await creating;

        var created = Assert.Single(session.Store.Current.Workspaces);
        Assert.Equal(id, created.Id);
        Assert.Matches("^[0-9a-f]{32}$", created.Id);
        Assert.Equal("Web", created.Name);
        Assert.Empty(created.Repositories);
        Assert.Equal((created.Id, tab), Assert.Single(session.Host.Shown));
        Assert.Equal(["Web"], session.Services.WorkspaceItems.Select(item => item.Name));
    }

    [Fact]
    public async Task A_cancelled_name_dialog_creates_nothing()
    {
        var session = NewSession();

        var creating = session.Commands.CreateAsync(null);
        session.Services.DialogHost.Current!.Cancel();

        Assert.Null(await creating);
        Assert.Empty(session.Store.Current.Workspaces);
        Assert.Empty(session.Host.Shown);
    }

    [Fact]
    public void Open_asks_the_host_also_while_the_store_is_read_only()
    {
        var session = NewSession(new ReadOnlyStore(WorkspaceReadOnlyCause.NewerVersion, List(new Workspace("web", "Web", []))));
        var tab = new RepoTabViewModel(session.Services);

        session.Commands.Open("web", tab);

        Assert.Equal(("web", tab), Assert.Single(session.Host.Shown));
    }

    [Fact]
    public async Task Rename_fills_in_the_name_and_saves_the_new_one()
    {
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", []), new Workspace("tools", "Tools", []))));

        var renaming = session.Commands.RenameAsync("web");
        var dialog = await session.DialogAsync<WorkspaceNameDialogViewModel>();
        Assert.Equal(("Rename workspace", "Rename Web", "Web"), (dialog.Title, dialog.Context, dialog.Name));
        dialog.Name = "tools";
        Assert.Equal("A workspace named Tools exists.", dialog.NameError);
        dialog.Name = "Web team";
        dialog.ConfirmCommand.Execute(null);
        await renaming;

        Assert.Equal("Web team", session.Workspace("web").Name);
        Assert.Equal(["Tools", "Web team"], session.Services.WorkspaceItems.Select(item => item.Name));
    }

    [Theory]
    [InlineData(0, "Delete the workspace Web? Nothing on disk changes.")]
    [InlineData(1, "Delete the workspace Web? Its repository stays where it is: nothing on disk changes.")]
    [InlineData(10, "Delete the workspace Web? Its 10 repositories stay where they are: nothing on disk changes.")]
    public async Task Delete_asks_first_with_the_count_of_its_repositories(int count, string question)
    {
        var repositories = Enumerable.Range(0, count).Select(i => new WorkspaceRepository(Path.Combine(Path.GetTempPath(), $"r{i}"))).ToList();
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", repositories))));

        var deleting = session.Commands.DeleteAsync("web");
        var confirmation = await session.DialogAsync<ConfirmationViewModel>();
        Assert.Equal("Delete workspace?", confirmation.Title);
        Assert.Equal(question, confirmation.Question);
        Assert.False(confirmation.HasDetail);
        Assert.Equal("Delete", confirmation.ConfirmText);
        Assert.True(confirmation.IsDestructive);
        Assert.Null(confirmation.RepositoryName);
        confirmation.ConfirmCommand.Execute(null);
        await deleting;

        Assert.Empty(session.Store.Current.Workspaces);
        Assert.Empty(session.Services.WorkspaceItems);
    }

    [Fact]
    public async Task Delete_says_the_dashboard_closes_while_it_is_open_and_cancel_keeps_the_workspace()
    {
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", []))));
        session.Host.OpenDashboards.Add("web");

        var deleting = session.Commands.DeleteAsync("web");
        var confirmation = await session.DialogAsync<ConfirmationViewModel>();
        Assert.Equal("Its dashboard tab closes.", confirmation.Detail);
        confirmation.Cancel();
        await deleting;

        Assert.NotNull(session.Store.Current.Find("web"));
    }

    // ---- Add repository… (1.7). ----

    [Fact]
    public async Task Add_repository_on_a_top_appends_its_working_directory_at_once_and_returns_it()
    {
        using var tree = await TreeAsync();
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", []))));
        session.Folders.Answer(tree.Api.Path);

        var added = await session.Commands.AddRepositoryAsync("web");

        Assert.Equal(["Add a repository to Web"], session.Folders.Titles);
        Assert.Null(session.Services.DialogHost.Current);
        var path = Assert.Single(session.PathsOf("web"));
        Assert.Equal(path, added);
        Assert.True(WorkspacePaths.Same(tree.Api.Path, path));

        // A second time: held already, so nothing is appended, and the held path is returned.
        session.Folders.Answer(tree.Api.Path + Path.DirectorySeparatorChar);
        Assert.Equal(path, await session.Commands.AddRepositoryAsync("web"));
        Assert.Single(session.PathsOf("web"));

        // Cancelled: nothing.
        Assert.Null(await session.Commands.AddRepositoryAsync("web"));
        Assert.Single(session.PathsOf("web"));
    }

    [Fact]
    public async Task A_top_through_a_link_counts_as_held_by_the_open_rows_working_directory()
    {
        using var tree = await TreeAsync();
        var link = tree.PathOf("link-to-api");
        LinkFolder(link, tree.Api.Path);
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", [new WorkspaceRepository(link)]))));

        // What the open dashboard's row reports: git's own working directory behind the link.
        var opened = (await session.Services.Repositories.OpenAsync(link, Cancelled)).WorkingDirectory;
        Assert.False(WorkspacePaths.Same(link, opened));
        session.Host.OpenedRows[("web", link)] = opened;
        session.Folders.Answer(tree.Api.Path);

        var added = await session.Commands.AddRepositoryAsync("web");

        Assert.Equal(link, added);
        Assert.Equal([link], session.PathsOf("web"));

        // Without the open row's answer the link is not the top's spelling, so the top is appended beside it.
        session.Host.OpenedRows.Clear();
        session.Folders.Answer(tree.Api.Path);
        await session.Commands.AddRepositoryAsync("web");
        Assert.Equal(2, session.PathsOf("web").Count);
    }

    [Fact]
    public async Task Add_repository_on_a_folder_that_is_no_repository_offers_the_checklist_and_appends_its_answer()
    {
        using var tree = await TreeAsync();
        await tree.InitAsync("web-app");
        await tree.InitAsync("group/docs");
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", []))));
        session.Folders.Answer(tree.Root);

        var adding = session.Commands.AddRepositoryAsync("web");
        var dialog = await session.DialogAsync<AddRepositoriesDialogViewModel>();
        await dialog.Search;
        Assert.Equal("Add repositories to Web", dialog.Title);
        Assert.Equal(["api", "docs", "web-app"], dialog.Rows.Select(row => row.Name));
        Assert.Equal("Add 3 repositories", dialog.ConfirmText);
        dialog.Rows[1].IsChecked = false;
        dialog.ConfirmCommand.Execute(null);

        Assert.Null(await adding);
        Assert.Equal(2, session.PathsOf("web").Count);
        Assert.True(WorkspacePaths.Same(tree.Api.Path, session.PathsOf("web")[0]));
        Assert.True(WorkspacePaths.Same(tree.PathOf("web-app"), session.PathsOf("web")[1]));
    }

    [Fact]
    public async Task Add_repository_on_a_folder_inside_a_working_tree_scans_it_with_the_repository_first_and_unchecked()
    {
        using var tree = await TreeAsync();
        var inside = tree.Api.Path + Path.DirectorySeparatorChar + "src";
        Directory.CreateDirectory(inside);
        await tree.InitAsync(Path.Combine("api", "src", "nested"));
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", []))));
        session.Folders.Answer(inside);

        var adding = session.Commands.AddRepositoryAsync("web");
        var dialog = await session.DialogAsync<AddRepositoriesDialogViewModel>();
        await dialog.Search;
        Assert.Equal(["api", "nested"], dialog.Rows.Select(row => row.Name));
        Assert.Equal(("Contains src", false, true), (dialog.Rows[0].Detail, dialog.Rows[0].IsChecked, dialog.Rows[0].IsEnabled));
        Assert.Equal(("nested", true), (dialog.Rows[1].Detail, dialog.Rows[1].IsChecked));
        dialog.Rows[0].IsChecked = true;
        dialog.ConfirmCommand.Execute(null);
        await adding;

        Assert.Equal(2, session.PathsOf("web").Count);
        Assert.True(WorkspacePaths.Same(tree.Api.Path, session.PathsOf("web")[0]));
    }

    [Fact]
    public async Task Gits_refusal_shows_the_error_dialog_with_gits_output_and_the_workspaces_name()
    {
        var failure = new GitException(
            "git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir",
            128,
            "fatal: detected dubious ownership in repository at 'D:/src/api'\nTo add an exception for this directory, call:\n");
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", []))), new FailingRepositories(failure));
        var folder = Path.Combine(Path.GetTempPath(), "api");
        session.Folders.Answer(folder);

        var adding = session.Commands.AddRepositoryAsync("web");
        var error = await session.DialogAsync<ErrorDialogViewModel>();
        Assert.Equal("Adding a repository failed", error.Title);
        Assert.Equal($"Git could not open {folder}.", error.Summary);
        Assert.Equal("In Web", error.RepositoryText);
        Assert.Equal("fatal: detected dubious ownership in repository at 'D:/src/api'\nTo add an exception for this directory, call:", error.Output);
        Assert.Equal(failure.CommandText, error.Command);
        error.Cancel();
        Assert.Null(await adding);
        Assert.Empty(session.PathsOf("web"));
    }

    [Fact]
    public async Task No_usable_git_shows_its_message_as_the_summary()
    {
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", []))), new FailingRepositories(new GitUnavailableException("Git was not found on this computer.")));
        session.Folders.Answer(Path.GetTempPath());

        var adding = session.Commands.AddRepositoryAsync("web");
        var error = await session.DialogAsync<ErrorDialogViewModel>();
        Assert.Equal(("Adding a repository failed", "Git was not found on this computer.", "In Web", false), (error.Title, error.Summary, error.RepositoryText, error.HasOutput));
        error.Cancel();
        await adding;
    }

    // ---- Add open tabs (1.7). ----

    [Fact]
    public void Add_open_tabs_appends_the_tabs_repositories_the_workspace_lacks_in_tab_order()
    {
        var api = Path.Combine(Path.GetTempPath(), "w", "api");
        var docs = Path.Combine(Path.GetTempPath(), "w", "docs");
        var web = Path.Combine(Path.GetTempPath(), "w", "web-app");
        var linked = Path.Combine(Path.GetTempPath(), "w", "link");
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", [new WorkspaceRepository(api), new WorkspaceRepository(linked)]))));

        Assert.Equal("No tab has a repository open", session.Commands.AddOpenTabsReason("web"));

        // A tab on the linked row's own repository counts as held; a second tab on one repository adds it once.
        session.Host.OpenedRows[("web", linked)] = Path.Combine(Path.GetTempPath(), "w", "physical");
        session.Host.OpenFolders.AddRange([web, api, Path.Combine(Path.GetTempPath(), "w", "physical"), docs, web]);
        Assert.Null(session.Commands.AddOpenTabsReason("web"));
        session.Commands.AddOpenTabs("web");

        Assert.Equal([api, linked, web, docs], session.PathsOf("web"));
        Assert.Equal("Every open repository is in this workspace", session.Commands.AddOpenTabsReason("web"));
    }

    // ---- Remove, Move, Locate (1.10). ----

    [Fact]
    public void Remove_takes_the_row_out_at_once_and_move_up_and_down_have_their_ends()
    {
        var a = Path.Combine(Path.GetTempPath(), "w", "a");
        var b = Path.Combine(Path.GetTempPath(), "w", "b");
        var c = Path.Combine(Path.GetTempPath(), "w", "c");
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", [new(a), new(b), new(c)]))));

        Assert.Equal("It is the first repository", session.Commands.MoveUpReason("web", a));
        Assert.Null(session.Commands.MoveDownReason("web", a));
        Assert.Equal("It is the last repository", session.Commands.MoveDownReason("web", c));
        Assert.Null(session.Commands.MoveUpReason("web", c));
        Assert.Null(session.Commands.RemoveReason("web", b));
        Assert.Null(session.Commands.LocateReason("web", b));

        session.Commands.MoveDown("web", a);
        Assert.Equal([b, a, c], session.PathsOf("web"));
        session.Commands.MoveUp("web", c);
        Assert.Equal([b, c, a], session.PathsOf("web"));
        session.Commands.MoveUp("web", b);
        Assert.Equal([b, c, a], session.PathsOf("web"));

        session.Commands.Remove("web", c);
        Assert.Equal([b, a], session.PathsOf("web"));
        Assert.Empty(session.Services.DialogHost.Open);
    }

    [Fact]
    public async Task Locate_puts_the_chosen_folders_top_in_the_saved_paths_place()
    {
        using var tree = await TreeAsync();
        var inside = Directory.CreateDirectory(Path.Combine(tree.Api.Path, "src")).FullName;
        var other = Path.Combine(tree.Root, "other");
        var gone = tree.PathOf("old-site");
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", [new(other), new(gone)]))));
        session.Folders.Answer(inside);

        await session.Commands.LocateAsync("web", gone);

        Assert.Equal(["Locate old-site"], session.Folders.Titles);
        Assert.Equal(other, session.PathsOf("web")[0]);
        Assert.True(WorkspacePaths.Same(tree.Api.Path, session.PathsOf("web")[1]));
        Assert.Null(session.Services.DialogHost.Current);
    }

    [Fact]
    public async Task Locate_refuses_a_folder_that_is_no_repository_and_a_top_the_workspace_holds()
    {
        using var tree = await TreeAsync();
        var plain = tree.Folder("plain");
        var gone = tree.PathOf("old-site");
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", [new(tree.Api.Path), new(gone)]))));

        session.Folders.Answer(plain);
        var locating = session.Commands.LocateAsync("web", gone);
        var error = await session.DialogAsync<ErrorDialogViewModel>();
        Assert.Equal(("Locating the repository failed", $"{plain} is not a git repository.", "In Web", false), (error.Title, error.Summary, error.RepositoryText, error.HasOutput));
        error.Cancel();
        await locating;

        session.Folders.Answer(tree.Api.Path);
        locating = session.Commands.LocateAsync("web", gone);
        error = await session.DialogAsync<ErrorDialogViewModel>();
        var top = (await session.Services.Repositories.OpenAsync(tree.Api.Path, Cancelled)).WorkingDirectory;
        Assert.Equal(("Locating the repository failed", $"{top} is already in Web."), (error.Title, error.Summary));
        error.Cancel();
        await locating;

        Assert.Equal([tree.Api.Path, gone], session.PathsOf("web"));
    }

    [Fact]
    public async Task Locate_on_gits_refusal_shows_its_output()
    {
        var failure = new GitException("git rev-parse --is-inside-work-tree", 128, "fatal: detected dubious ownership in repository at 'D:/x'\n");
        var gone = Path.Combine(Path.GetTempPath(), "w", "old-site");
        var session = NewSession(new InMemoryWorkspaceStore(List(new Workspace("web", "Web", [new(gone)]))), new FailingRepositories(failure));
        var folder = Path.Combine(Path.GetTempPath(), "w", "x");
        session.Folders.Answer(folder);

        var locating = session.Commands.LocateAsync("web", gone);
        var error = await session.DialogAsync<ErrorDialogViewModel>();
        Assert.Equal(("Locating the repository failed", $"Git could not open {folder}.", "fatal: detected dubious ownership in repository at 'D:/x'"), (error.Title, error.Summary, error.Output));
        error.Cancel();
        await locating;
        Assert.Equal([gone], session.PathsOf("web"));
    }

    // ---- Read-only (1.3, 1.5). ----

    [Theory]
    [InlineData(WorkspaceReadOnlyCause.NewerVersion, "workspaces.json was made by a newer version of VisualCommit")]
    [InlineData(WorkspaceReadOnlyCause.Unreadable, "workspaces.json could not be read")]
    public async Task While_the_store_is_read_only_every_edit_is_disabled_with_its_reason_and_does_nothing(WorkspaceReadOnlyCause cause, string reason)
    {
        var path = Path.Combine(Path.GetTempPath(), "w", "a");
        var store = new ReadOnlyStore(cause, List(new Workspace("web", "Web", [new(path), new(path + "2")])));
        var session = NewSession(store);
        session.Host.OpenFolders.Add(Path.Combine(Path.GetTempPath(), "w", "b"));
        session.Folders.Answer(path).Answer(path).Answer(path);

        Assert.Equal(reason, session.Commands.EditReason);
        Assert.All(
            new[]
            {
                session.Commands.CreateReason,
                session.Commands.RenameReason("web"),
                session.Commands.DeleteReason("web"),
                session.Commands.AddRepositoryReason("web"),
                session.Commands.AddOpenTabsReason("web"),
                session.Commands.RemoveReason("web", path),
                session.Commands.MoveUpReason("web", path + "2"),
                session.Commands.MoveDownReason("web", path),
                session.Commands.LocateReason("web", path),
            },
            actual => Assert.Equal(reason, actual));

        Assert.Null(await session.Commands.CreateAsync(null));
        await session.Commands.RenameAsync("web");
        await session.Commands.DeleteAsync("web");
        Assert.Null(await session.Commands.AddRepositoryAsync("web"));
        session.Commands.AddOpenTabs("web");
        session.Commands.Remove("web", path);
        session.Commands.MoveDown("web", path);
        await session.Commands.LocateAsync("web", path);

        Assert.Empty(session.Services.DialogHost.Open);
        Assert.Empty(session.Folders.Titles);
        Assert.Equal(0, store.Updates);
    }

    [Fact]
    public void The_welcome_pages_list_follows_the_store_by_name_then_id()
    {
        var store = new InMemoryWorkspaceStore(List(new Workspace("b", "web", []), new Workspace("a", "Web", []), new Workspace("c", "Alpha", [new(Path.Combine(Path.GetTempPath(), "x", "api"))])));
        var session = NewSession(store);

        Assert.Equal([("c", "Alpha", "1 repository", "api"), ("a", "Web", "No repositories", "No repositories yet"), ("b", "web", "No repositories", "No repositories yet")],
            session.Services.WorkspaceItems.Select(item => (item.Id, item.Name, item.CountText, item.ToolTipText)));

        store.Update(list => WorkspaceEdits.Rename(list, "c", "zeta"));
        Assert.Equal(["Web", "web", "zeta"], session.Services.WorkspaceItems.Select(item => item.Name));
    }

    private static WorkspaceList List(params Workspace[] workspaces) => new() { Workspaces = workspaces };

    /// <summary>A provider whose every open fails with <paramref name="failure"/>.</summary>
    private sealed class FailingRepositories(Exception failure) : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => Task.FromException<IGitRepository>(failure);

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }
}
