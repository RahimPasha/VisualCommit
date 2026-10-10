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
