using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The dashboard's menus (docs/phase-6-design.md 2.5, 2.7, 14.10, 14.5; D77): pure builders of the state they are given.</summary>
public class DashboardMenusTests
{
    private const string Folder = @"C:\src\web-app";
    private const string GoneFolder = @"C:\src\old-site";

    [Fact]
    public void A_ready_rows_menu_offers_every_item()
    {
        var (row, actions) = Row();

        var menu = DashboardMenus.ForRow(row, Ready(), actions);

        Assert.Equal(
            ["web-app (" + Folder + ")", "Open", "—", "Fetch", "Pull", "—", "Show in folder", "Copy path", "—", "Move up", "Move down", "Remove from workspace"],
            Lines(menu));
        Assert.All(menu.Items, item => Assert.True(item.IsEnabled, item.Label));
        Assert.DoesNotContain(menu.Items, item => item.IsDestructive);
    }

    [Fact]
    public void The_items_the_lists_own_keys_run_show_those_keys_in_each_platforms_words()
    {
        // 6.7 (the lead's ruling at status step 22, D192, D215): Open, Move up, Move down and
        // Remove from workspace act on the selected row as Enter, Alt+Up, Alt+Down and Delete do;
        // a disabled item keeps its key, as a keyed button does (6.8). Never the global KeyTexts.
        var (row, actions) = Row();
        var held = Ready() with { ShowsLocate = true, RemoveReason = "Wait for Fetch all to finish" };

        var windows = DashboardMenus.ForRow(row, Ready(), actions, new KeyTexts(CommandKey.Ctrl));
        var mac = DashboardMenus.ForRow(row, held, actions, new KeyTexts(CommandKey.Cmd));

        Assert.Equal(
            ["Open · Enter", "Fetch", "Pull", "Show in folder", "Copy path", "Move up · Alt+Up", "Move down · Alt+Down", "Remove from workspace · Delete"],
            Keyed(windows));
        Assert.Equal(
            ["Open · Enter", "Fetch", "Pull", "Show in folder", "Copy path", "Move up · Option+Up", "Move down · Option+Down", "Locate…", "Remove from workspace · Cmd+Delete"],
            Keyed(mac));
        Assert.False(mac.Items.Single(item => item.Label == "Remove from workspace").IsEnabled);
        Assert.All(windows.Items.Concat(mac.Items), item => Assert.Null(item.Command));

        // The settings list names the same keys (6.11), the shown one first.
        Assert.Equal(
            [("Open the repository", "Enter"), ("Remove from workspace", "Cmd+Delete, Forward Delete"), ("Move the repository", "Option+Up, Option+Down")],
            AppShortcuts.Groups(CommandKey.Cmd).Single(group => group.Title == "Dashboard").Rows.Select(entry => (entry.Action, entry.Keys)));
        Assert.Equal(
            [("Open the repository", "Enter"), ("Remove from workspace", "Delete"), ("Move the repository", "Alt+Up, Alt+Down")],
            AppShortcuts.Groups(CommandKey.Ctrl).Single(group => group.Title == "Dashboard").Rows.Select(entry => (entry.Action, entry.Keys)));
    }

    [Fact]
    public void A_gone_folders_menu_disables_what_needs_it_and_offers_locate()
    {
        var (row, actions) = Row();
        var reason = $"{GoneFolder} does not exist";
        var state = Ready() with
        {
            Name = "old-site",
            Folder = GoneFolder,
            OpenReason = reason,
            FetchReason = reason,
            PullReason = reason,
            ShowInFolderReason = reason,
            MoveDownReason = DashboardTexts.LastRepositoryText,
            ShowsLocate = true,
        };

        var menu = DashboardMenus.ForRow(row, state, actions);

        Assert.Equal(
            [
                "old-site (" + GoneFolder + ")", $"Open (disabled: {reason})", "—", $"Fetch (disabled: {reason})", $"Pull (disabled: {reason})", "—",
                $"Show in folder (disabled: {reason})", "Copy path", "—", "Move up", "Move down (disabled: It is the last repository)", "Locate…", "Remove from workspace",
            ],
            Lines(menu));
    }

    [Fact]
    public void A_row_not_read_yet_waits_and_the_toolbars_reasons_disable_fetch_and_pull()
    {
        var (row, actions) = Row();
        var wait = DashboardTexts.WaitToBeRead("web-app");

        var unread = DashboardMenus.ForRow(row, Ready() with { OpenReason = wait, FetchReason = wait, PullReason = wait }, actions);
        Assert.Equal(wait, unread.Items.Single(item => item.Label == "Open").DisabledReason);

        var notes = DashboardMenus.ForRow(row, Ready() with { FetchReason = "This repository has no remotes", PullReason = "main has no upstream: set one first" }, actions);
        Assert.Equal(("This repository has no remotes", "main has no upstream: set one first"), (Reason(notes, "Fetch"), Reason(notes, "Pull")));
        Assert.True(notes.Items.Single(item => item.Label == "Open").IsEnabled);
    }

    [Fact]
    public void Cancel_shows_for_an_operation_that_could_be_cancelled_and_is_disabled_past_its_window()
    {
        var (row, actions) = Row();

        var fetching = DashboardMenus.ForRow(row, Ready() with { RunningTitle = "Fetch", RunningIsCancellable = true, CanCancel = true }, actions);
        Assert.Equal(["Fetch", "Pull", "Cancel Fetch"], fetching.Items.Select(item => item.Label).Skip(1).Take(3));
        Assert.True(fetching.Items.Single(item => item.Label == "Cancel Fetch").IsEnabled);

        var pastFetch = DashboardMenus.ForRow(row, Ready() with { RunningTitle = "Pull", RunningIsCancellable = true, CanCancel = false }, actions);
        Assert.Equal("Pull can no longer be cancelled", Reason(pastFetch, "Cancel Pull"));

        var mergeTool = DashboardMenus.ForRow(row, Ready() with { RunningTitle = "Merge tool for notes.txt", RunningIsCancellable = true, CanCancel = true }, actions);
        Assert.True(mergeTool.Items.Single(item => item.Label == "Cancel Merge tool for notes.txt").IsEnabled);

        // A checkout never could be cancelled: no item at all.
        var checkout = DashboardMenus.ForRow(row, Ready() with { RunningTitle = "Check out main", RunningIsCancellable = false }, actions);
        Assert.DoesNotContain(checkout.Items, item => item.Label.StartsWith("Cancel", StringComparison.Ordinal));
    }

    [Fact]
    public void Error_details_shows_while_a_dialog_is_kept()
    {
        var (row, actions) = Row();

        var menu = DashboardMenus.ForRow(row, Ready() with { HasKeptDialog = true }, actions);

        Assert.Equal(["Open", "Fetch", "Pull", "Error details…", "Show in folder"], menu.Items.Select(item => item.Label).Take(5));
        menu.Items.Single(item => item.Label == "Error details…").Invoke();
        Assert.Equal(["ShowErrorDetails"], actions.Calls);
    }

    [Fact]
    public void At_the_top_and_the_bottom_moving_further_is_disabled()
    {
        var (row, actions) = Row();

        var top = DashboardMenus.ForRow(row, Ready() with { MoveUpReason = DashboardTexts.FirstRepositoryText }, actions);
        Assert.Equal("It is the first repository", Reason(top, "Move up"));
        Assert.True(top.Items.Single(item => item.Label == "Move down").IsEnabled);

        var bottom = DashboardMenus.ForRow(row, Ready() with { MoveDownReason = DashboardTexts.LastRepositoryText }, actions);
        Assert.Equal("It is the last repository", Reason(bottom, "Move down"));
    }

    [Fact]
    public void A_read_only_store_disables_every_edit()
    {
        var (row, actions) = Row();
        const string ReadOnly = "workspaces.json was made by a newer version of VisualCommit";

        var menu = DashboardMenus.ForRow(row, Ready() with { MoveUpReason = ReadOnly, MoveDownReason = ReadOnly, ShowsLocate = true, LocateReason = ReadOnly, RemoveReason = ReadOnly }, actions);

        Assert.All(new[] { "Move up", "Move down", "Locate…", "Remove from workspace" }, label => Assert.Equal(ReadOnly, Reason(menu, label)));
        Assert.True(menu.Items.Single(item => item.Label == "Open").IsEnabled);
        Assert.True(menu.Items.Single(item => item.Label == "Copy path").IsEnabled);
    }

    [Theory]
    [InlineData("Wait for Fetch all to finish")]
    [InlineData("Wait for Pull all to finish")]
    public void A_row_a_running_run_holds_cannot_be_fetched_pulled_or_removed(string reason)
    {
        var (row, actions) = Row();

        var held = DashboardMenus.ForRow(row, Ready() with { FetchReason = reason, PullReason = reason, RemoveReason = reason }, actions);
        Assert.Equal((reason, reason, reason), (Reason(held, "Fetch"), Reason(held, "Pull"), Reason(held, "Remove from workspace")));

        // A skipped or ended row of the run is free again.
        var ended = DashboardMenus.ForRow(row, Ready(), actions);
        Assert.True(ended.Items.Single(item => item.Label == "Remove from workspace").IsEnabled);
    }

    [Fact]
    public void Each_item_runs_its_action()
    {
        var (row, actions) = Row();
        var menu = DashboardMenus.ForRow(row, Ready() with { RunningTitle = "Fetch", RunningIsCancellable = true, CanCancel = true, ShowsLocate = true }, actions);

        foreach (var item in menu.Items)
        {
            item.Invoke();
        }

        Assert.Equal(["Open", "Fetch", "Pull", "Cancel", "ShowInFolder", "CopyPath", "MoveUp", "MoveDown", "Locate", "Remove"], actions.Calls);
        Assert.All(actions.Rows, called => Assert.Same(row, called));
    }

    [Fact]
    public void The_headers_menu()
    {
        var actions = new RecordingDashboardActions();

        var menu = DashboardMenus.ForHeader("Web", null, null, null, actions);
        Assert.Equal(["Web", "Add open tabs", "—", "Rename workspace…", "Delete workspace…"], Lines(menu));
        Assert.True(menu.Items.Single(item => item.Label == "Delete workspace…").IsDestructive);
        foreach (var item in menu.Items)
        {
            item.Invoke();
        }

        Assert.Equal(["AddOpenTabs", "Rename", "Delete"], actions.Calls);

        var full = DashboardMenus.ForHeader("Web", "Every open repository is in this workspace", null, null, actions);
        Assert.Equal("Every open repository is in this workspace", Reason(full, "Add open tabs"));
    }

    private static DashboardRowMenuState Ready() => new(
        "web-app",
        Folder,
        OpenReason: null,
        FetchReason: null,
        PullReason: null,
        RunningTitle: null,
        RunningIsCancellable: false,
        CanCancel: false,
        HasKeptDialog: false,
        ShowInFolderReason: null,
        MoveUpReason: null,
        MoveDownReason: null,
        ShowsLocate: false,
        LocateReason: null,
        RemoveReason: null);

    private static (DashboardRowViewModel Row, RecordingDashboardActions Actions) Row()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(Folder));
        var dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services);
        return (dashboard.Rows[0], new RecordingDashboardActions());
    }

    private static string? Reason(MenuModel menu, string label) => menu.Items.Single(item => item.Label == label).DisabledReason;

    /// <summary>The items as the menu draws them: the label, then its key when it has one.</summary>
    private static List<string> Keyed(MenuModel menu) => [.. menu.Items.Select(item => item.KeyText is { } key ? $"{item.Label} · {key}" : item.Label)];

    private static List<string> Lines(MenuModel menu) =>
    [
        .. menu.Entries.Select(entry => entry switch
        {
            MenuHeader header => header.ToolTip is { } tip ? $"{header.Text} ({tip})" : header.Text,
            MenuSeparator => "—",
            MenuItem { IsEnabled: false } item => $"{item.Label} (disabled: {item.DisabledReason})",
            MenuItem item => item.Label,
            _ => entry.ToString()!,
        }),
    ];

    private sealed class RecordingDashboardActions : IDashboardRowActions, IDashboardHeaderActions
    {
        public List<string> Calls { get; } = [];

        public List<DashboardRowViewModel> Rows { get; } = [];

        public IAppLog Log => NullAppLog.Instance;

        public void Open(DashboardRowViewModel row) => Record("Open", row);

        public Task FetchAsync(DashboardRowViewModel row) => Done("Fetch", row);

        public Task PullAsync(DashboardRowViewModel row) => Done("Pull", row);

        public void Cancel(DashboardRowViewModel row) => Record("Cancel", row);

        public void ShowErrorDetails(DashboardRowViewModel row) => Record("ShowErrorDetails", row);

        public Task ShowInFolderAsync(DashboardRowViewModel row) => Done("ShowInFolder", row);

        public Task CopyPathAsync(DashboardRowViewModel row) => Done("CopyPath", row);

        public void MoveUp(DashboardRowViewModel row) => Record("MoveUp", row);

        public void MoveDown(DashboardRowViewModel row) => Record("MoveDown", row);

        public Task LocateAsync(DashboardRowViewModel row) => Done("Locate", row);

        public void Remove(DashboardRowViewModel row) => Record("Remove", row);

        public void AddOpenTabs() => Calls.Add("AddOpenTabs");

        public Task RenameAsync()
        {
            Calls.Add("Rename");
            return Task.CompletedTask;
        }

        public Task DeleteAsync()
        {
            Calls.Add("Delete");
            return Task.CompletedTask;
        }

        private void Record(string call, DashboardRowViewModel row)
        {
            Calls.Add(call);
            Rows.Add(row);
        }

        private Task Done(string call, DashboardRowViewModel row)
        {
            Record(call, row);
            return Task.CompletedTask;
        }
    }
}
