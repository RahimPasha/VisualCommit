using VisualCommit.App.Services;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

public class JsonSessionStoreTests
{
    [Fact]
    public void A_missing_file_is_a_first_start()
    {
        using var data = new TempDirectory("data");

        var store = new JsonSessionStore(Path.Combine(data.Path, "session.json"));

        Assert.Empty(store.Current.Tabs);
        Assert.Empty(store.Current.Recent);
        Assert.Null(store.Current.Window);
        Assert.Null(store.Current.LeftPanelWidth);
    }

    [Fact]
    public void Everything_saved_is_read_back_by_a_new_store()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");
        var opened = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        new JsonSessionStore(file).Update(state => state with
        {
            Tabs = [new TabState(@"D:\repos\graph"), new TabState(null)],
            ActiveTab = 1,
            Recent = [new RecentRepository(@"D:\repos\graph", "graph", opened)],
            Window = new WindowPlacement(40, 30, 1300, 800, IsMaximized: false),
            LeftPanelWidth = 320,
            RightPanelWidth = 460,
        });

        var read = new JsonSessionStore(file).Current;
        Assert.Equal([@"D:\repos\graph", null], read.Tabs.Select(tab => tab.RepositoryPath));
        Assert.Equal(1, read.ActiveTab);
        Assert.Equal(new RecentRepository(@"D:\repos\graph", "graph", opened), Assert.Single(read.Recent));
        Assert.Equal(new WindowPlacement(40, 30, 1300, 800, false), read.Window);
        Assert.Equal(320, read.LeftPanelWidth);
        Assert.Equal(460, read.RightPanelWidth);
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public void Write_produces_a_file_the_app_reads()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");

        JsonSessionStore.Write(file, new SessionState { Tabs = [new TabState("/repos/linear")] });

        Assert.Equal("/repos/linear", Assert.Single(new JsonSessionStore(file).Current.Tabs).RepositoryPath);
    }

    [Fact]
    public void An_unreadable_file_is_set_aside_and_the_app_starts_empty()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");
        File.WriteAllText(file, "{ this is not json");
        var log = new RecordingLog();

        var store = new JsonSessionStore(file, log);

        Assert.Empty(store.Current.Tabs);
        Assert.False(File.Exists(file));
        Assert.True(File.Exists(Path.Combine(data.Path, "session.unreadable.json")));
        Assert.Contains(log.Messages, message => message.Contains("could not be read", StringComparison.Ordinal));
    }

    [Fact]
    public void A_tab_with_a_blank_folder_is_read_as_an_empty_tab()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");
        File.WriteAllText(file, """{ "tabs": [ { "repositoryPath": "" }, { "repositoryPath": "   " }, { "repositoryPath": "/repos/x" } ] }""");

        var store = new JsonSessionStore(file);

        Assert.Equal([null, null, "/repos/x"], store.Current.Tabs.Select(tab => tab.RepositoryPath));
    }

    [Fact]
    public void A_workspaces_dashboard_is_saved_by_its_id_and_read_back()
    {
        // docs/phase-6-design.md 1.11: a dashboard is TabState(null, workspaceId).
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");

        new JsonSessionStore(file).Update(state => state with
        {
            Tabs = [new TabState(null, "3f2c9a"), new TabState("/repos/api"), new TabState(null)],
            ActiveTab = 0,
        });

        Assert.Equal(
            [new TabState(null, "3f2c9a"), new TabState("/repos/api"), new TabState(null)],
            new JsonSessionStore(file).Current.Tabs);
        Assert.Contains("\"workspaceId\": \"3f2c9a\"", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void An_older_file_without_workspace_ids_loads_with_none()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");
        File.WriteAllText(file, """{ "tabs": [ { "repositoryPath": "/repos/x" }, { "repositoryPath": null } ], "activeTab": 1 }""");

        var store = new JsonSessionStore(file);

        Assert.Equal([new TabState("/repos/x"), new TabState(null)], store.Current.Tabs);
        Assert.Equal(1, store.Current.ActiveTab);
    }

    [Fact]
    public void A_blank_id_is_none_and_a_tab_is_empty_only_when_both_are_blank()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");
        File.WriteAllText(
            file,
            """
            { "tabs": [
                { "repositoryPath": null, "workspaceId": "  " },
                { "repositoryPath": "", "workspaceId": "w1" },
                { "repositoryPath": "/repos/x", "workspaceId": "" },
                { "workspaceId": "w2" },
                { "repositoryPath": "/repos/y", "workspaceId": "w3" }
            ] }
            """);

        var store = new JsonSessionStore(file);

        // A folder beside an id is read as written; the restore ignores it (1.11).
        Assert.Equal(
            [new TabState(null), new TabState(null, "w1"), new TabState("/repos/x"), new TabState(null, "w2"), new TabState("/repos/y", "w3")],
            store.Current.Tabs);
    }

    [Fact]
    public void Lists_left_out_of_a_hand_edited_file_are_read_as_empty()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");
        File.WriteAllText(file, """{ "tabs": null, "activeTab": 0 }""");

        var store = new JsonSessionStore(file);

        Assert.Empty(store.Current.Tabs);
        Assert.Empty(store.Current.Recent);
    }
}
