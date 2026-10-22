using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The welcome page's Workspaces section (docs/phase-6-design.md 13.1, D188): always there,
/// between Init and the recent list; its heading and New workspace…; the store's notices; No
/// workspaces or the workspaces by name, each opening its dashboard from the page's tab, each with
/// its menu; hidden with the clone form.
/// </summary>
public class WelcomeWorkspacesTests
{
    /// <summary>The page's texts in reading order with no workspace (13.1).</summary>
    private static readonly string[] EmptyPageTexts =
    [
        "No repository open", "Open, clone or init a repository to see its history.", "Open", "Clone", "Init",
        "Workspaces", "New workspace…", "No workspaces", "Recent repositories", "No recent repositories",
    ];

    // ---- The page in the window. ----

    [AvaloniaFact]
    public async Task With_no_workspace_the_section_lies_between_init_and_the_recent_list()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        Assert.Equal(EmptyPageTexts, VisibleTexts.In(app.FindByAutomationId("CommitGraph")));
        var section = app.WelcomeWorkspaces();
        Assert.Equal("Workspaces", section.Heading);
        Assert.True(section.NewWorkspaceEnabled);
        Assert.Equal("Create a workspace: repositories you check and update together", section.NewWorkspaceToolTip);
        Assert.Equal(section.NewWorkspaceToolTip, section.NewWorkspaceHelpText);
        Assert.Equal("New workspace", section.NewWorkspaceName);
        Assert.Null(section.Notice);
        Assert.True(section.ShowsNoWorkspaces);
        Assert.Empty(section.Items);

        // The heading row: the button's height, 24 below the buttons' row (the stack's 8 and its
        // own 16), the heading at the stack's left and the button at its right; the section adds
        // 8 + 16 + the row + 8 + a line between the buttons and "Recent repositories". A line of
        // the window's 12.5-px text is 16 high, as every line of the page today, so the row is 20
        // (2 + 16 + 2) and the section 68, where 13.1 estimated 19 and 66 from a 15-px line.
        var view = app.ActiveWelcomeView;
        var stack = Stack(view);
        var stackBounds = app.BoundsOf(stack);
        var buttons = app.BoundsOf(app.Find<Button>("InitRepoButton").GetVisualParent<StackPanel>()!);
        var row = app.BoundsOf(Named<DockPanel>(view, "WorkspacesHeadingRow"));
        var recent = app.BoundsOf(view.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Recent repositories"));
        Assert.Equal(buttons.Bottom + 24, row.Top, 0.5);
        Assert.Equal(Line + 4, row.Height, 0.5);
        Assert.Equal(stackBounds.Left, section.HeadingBounds.Left, 0.5);
        Assert.Equal(stackBounds.Right, section.NewWorkspaceBounds.Right, 0.5);
        Assert.Equal(Line + 4, section.NewWorkspaceBounds.Height, 0.5);

        // The heading's 11-px text and the button's 12.5-px text are centred on one line of the row.
        var buttonText = app.BoundsOf(Named<Button>(view, "NewWorkspaceButton").GetVisualDescendants().OfType<TextBlock>().Single());
        Assert.Equal(row.Center.Y, section.HeadingBounds.Center.Y, 1);
        Assert.Equal(buttonText.Center.Y, section.HeadingBounds.Center.Y, 1);
        Assert.InRange(section.NewWorkspaceBounds.Width, 120, 150);
        Assert.Equal(buttons.Bottom + 24 + 8 + 16 + (Line + 4) + 8 + Line, recent.Top, 1);
        DiffColors.AssertBrush(Color.Parse("#9BA3B0"), section.HeadingForeground, "the heading");

        // The stack: 392 wide in the graph area at 1100×700, centred vertically.
        Assert.Equal(new Rect(284, 0, 392, 0).Width, stackBounds.Width, 0.5);
        Assert.Equal(284, stackBounds.Left, 0.5);
        var area = app.BoundsOf(app.FindByAutomationId("CommitGraph"));
        Assert.Equal(stackBounds.Top - area.Top, area.Bottom - stackBounds.Bottom, 1);
        TestPictures.Save(app, "welcome-workspaces-none");
    }

    [AvaloniaTheory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task Two_workspaces_are_listed_by_name_with_their_counts_and_folder_names(string theme)
    {
        using var data = new TempDirectory("data");
        if (theme == "light")
        {
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.Light });
        }

        using var app = ShellDriver.StartWithWorkspace(data.Path, TwoWorkspaces(data.Path));
        await app.WaitUntilReadyAsync();
        Assert.Equal(theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark, app.Window.ActualThemeVariant);

        Assert.Equal(
            ["No repository open", "Open, clone or init a repository to see its history.", "Open", "Clone", "Init",
             "Workspaces", "New workspace…", "Docs", "1 repository", "web team", "10 repositories", "Recent repositories", "No recent repositories"],
            VisibleTexts.In(app.FindByAutomationId("CommitGraph")));
        var section = app.WelcomeWorkspaces();
        Assert.False(section.ShowsNoWorkspaces);
        Assert.Equal(["Docs", "web team"], section.Items.Select(item => item.Name));
        Assert.Equal(["Docs", "web team"], section.Items.Select(item => item.AutomationName));
        Assert.Equal(["1 repository", "10 repositories"], section.Items.Select(item => item.CountText));
        Assert.Equal(
            ["handbook", "api, web-app, docs, mobile, design-system, billing, infra, scratch, …"],
            section.Items.Select(item => item.ToolTip));

        // Each item as high as a recent item (5 + a line + 5: 26, where 13.1 estimated 25), the
        // stack's width, the name at its left (primary) and the count 12 or more from it at its
        // right (secondary); the section 8 + 16 + the heading row + 8 + 26 × 2 high.
        var view = app.ActiveWelcomeView;
        var stack = app.BoundsOf(Stack(view));
        var primary = theme == "light" ? Color.Parse("#1B1F27") : Color.Parse("#E4E7EC");
        var secondary = theme == "light" ? Color.Parse("#5C6572") : Color.Parse("#9BA3B0");
        Assert.All(section.Items, item =>
        {
            Assert.Equal(Line + 10, item.Bounds.Height, 0.5);
            Assert.Equal(stack.Width, item.Bounds.Width, 0.5);
            Assert.Equal(item.Bounds.Left + 8, item.NameBounds.Left, 0.5);
            Assert.Equal(item.Bounds.Right - 8, item.CountBounds.Right, 0.5);
            Assert.True(item.CountBounds.Left - item.NameBounds.Right >= 12 - 0.5);
            Assert.Equal(item.Name, item.DrawnName);
            DiffColors.AssertBrush(primary, item.NameForeground, "a workspace's name");
            DiffColors.AssertBrush(secondary, item.CountForeground, "a workspace's count");
        });
        Assert.Equal(section.Items[0].Bounds.Bottom, section.Items[1].Bounds.Top, 0.5);
        var buttons = app.BoundsOf(app.Find<Button>("InitRepoButton").GetVisualParent<StackPanel>()!);
        var recent = app.BoundsOf(view.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Recent repositories"));
        Assert.Equal(buttons.Bottom + 24 + 8 + 16 + (Line + 4) + 8 + ((Line + 10) * 2), recent.Top, 1);
        TestPictures.Save(app, $"welcome-workspaces-{theme}");
    }

    [AvaloniaFact]
    public async Task A_right_click_on_an_item_opens_its_menu_and_a_click_opens_the_workspace_from_the_pages_tab()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithWorkspace(data.Path, TwoWorkspaces(data.Path));
        await app.WaitUntilReadyAsync();
        var host = new RecordingHost();
        app.Session.Services.WorkspaceHost = host;
        var item = ItemButton(app, "web team");

        app.RightClick(item);
        var menu = app.Menu();
        Assert.Equal(["web team", "Open", "—", "Rename workspace…", "Delete workspace… (danger)"], menu.Lines());
        Assert.Equal("Open", menu.Highlighted?.Label);
        Assert.Contains(app.LogLines(), line => line.EndsWith("Context menu opened: web team by pointer", StringComparison.Ordinal));
        app.PressKey(Key.Escape);

        // Shift+F10 on the focused item opens the same menu.
        item.Focus(NavigationMethod.Tab);
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        Assert.Equal("web team", app.Menu().Header);
        app.ClickMenuItem("Open");
        await app.WaitForAsync(() => host.Shown.Count == 1, "the menu's Open");
        Assert.Equal(("web", app.Session.Shell.ActiveTab), host.Shown[0]);

        // A click, and Enter on the focused item, open it too, from the page's tab.
        app.Click(ItemButton(app, "Docs"));
        Assert.Equal(("docs", app.Session.Shell.ActiveTab), host.Shown[1]);
        ItemButton(app, "Docs").Focus(NavigationMethod.Tab);
        app.PressKey(Key.Enter);
        Assert.Equal(("docs", app.Session.Shell.ActiveTab), host.Shown[2]);
    }

    [AvaloniaFact]
    public async Task The_clone_form_hides_the_section()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithWorkspace(data.Path, TwoWorkspaces(data.Path));
        await app.WaitUntilReadyAsync();

        app.Click(app.Find<Button>("CloneRepoButton"));
        var view = app.ActiveWelcomeView;
        Assert.False(Named<TextBlock>(view, "WorkspacesHeading").IsEffectivelyVisible);
        Assert.False(Named<Button>(view, "NewWorkspaceButton").IsEffectivelyVisible);
        Assert.False(Named<ItemsControl>(view, "WorkspaceList").IsEffectivelyVisible);
        Assert.DoesNotContain("Workspaces", VisibleTexts.In(app.FindByAutomationId("CommitGraph")));

        app.Click(app.Find<Button>("CloneCloseButton"));
        Assert.True(Named<ItemsControl>(view, "WorkspaceList").IsEffectivelyVisible);
    }

    // ---- The view model, with the store's states. ----

    [AvaloniaFact]
    public void A_set_aside_file_says_so_in_the_secondary_tone_and_editing_stays_possible()
    {
        var store = new FakeWorkspaceStore { Notice = "workspaces.json could not be read, so it was set aside as workspaces.unreadable.json." };
        using var page = ShowPage(store);
        var (welcome, view, host) = (page.Welcome, page.View, page.Host);
        {
            Assert.True(welcome.HasWorkspacesNotice);
            Assert.False(welcome.IsWorkspacesNoticeWarning);
            var notice = Named<TextBlock>(view, "WorkspacesNotice");
            Assert.True(notice.IsEffectivelyVisible);
            Assert.Equal(store.Notice, notice.Text);
            Assert.Equal(TextWrapping.Wrap, notice.TextWrapping);
            DiffColors.AssertBrush(Color.Parse("#9BA3B0"), notice.Foreground, "the notice");
            Assert.True(welcome.CanEditWorkspaces);
            Assert.True(welcome.NewWorkspaceCommand.CanExecute(null));
        }
    }

    [AvaloniaTheory]
    [InlineData(WorkspaceReadOnlyCause.NewerVersion, "workspaces.json was made by a newer version of VisualCommit, so workspaces cannot be changed in this version.", "workspaces.json was made by a newer version of VisualCommit")]
    [InlineData(WorkspaceReadOnlyCause.Unreadable, "workspaces.json could not be read, so changes to workspaces are not saved this session.", "workspaces.json could not be read")]
    public void A_read_only_store_disables_new_workspace_with_its_reason(WorkspaceReadOnlyCause cause, string notice, string reason)
    {
        var store = new FakeWorkspaceStore { ReadOnlyCause = cause, Notice = notice };
        store.Seed(new Workspace("web", "Web", []));
        using var page = ShowPage(store);
        var (welcome, view, host) = (page.Welcome, page.View, page.Host);
        {
            var button = Named<Button>(view, "NewWorkspaceButton");
            Assert.False(button.IsEffectivelyEnabled);
            Assert.Equal(reason, ToolTip.GetTip(button));
            Assert.Equal(reason, Avalonia.Automation.AutomationProperties.GetHelpText(button));
            Assert.True(ToolTip.GetShowOnDisabled(button));
            Assert.Equal(reason, welcome.WorkspacesEditReason);
            Assert.Equal(notice, Named<TextBlock>(view, "WorkspacesNotice").Text);
            DiffColors.AssertBrush(Color.Parse("#9BA3B0"), Named<TextBlock>(view, "WorkspacesNotice").Foreground, "the notice");

            // The item's menu: Open stays, Rename and Delete are disabled with the reason.
            var menu = welcome.MenuFor(welcome.Workspaces[0]);
            var items = menu.Items.ToList();
            Assert.Equal("Web", menu.HeaderText);
            Assert.True(items[0].IsEnabled);
            Assert.All(items.Skip(1), item => Assert.Equal((false, reason), (item.IsEnabled, item.DisabledReason)));
            TestPictures.Save(host, $"welcome-read-only-{cause}");
        }
    }

    [AvaloniaFact]
    public void A_failed_save_is_said_in_the_warning_tone_until_a_save_succeeds()
    {
        var store = new FakeWorkspaceStore();
        using var page = ShowPage(store);
        var (welcome, view, host) = (page.Welcome, page.View, page.Host);
        {
            var notice = Named<TextBlock>(view, "WorkspacesNotice");
            Assert.False(notice.IsEffectivelyVisible);

            store.FailNextSave("workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.");
            store.Update(list => WorkspaceEdits.Create(list, "a", "A"));
            DiffHost.Settle();
            Assert.True(notice.IsEffectivelyVisible);
            Assert.True(welcome.IsWorkspacesNoticeWarning);
            Assert.Equal("workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.", notice.Text);
            DiffColors.AssertBrush(Color.Parse("#E0A23B"), notice.Foreground, "a failed save's notice");
            TestPictures.Save(host, "welcome-save-failed");

            store.Update(list => WorkspaceEdits.Create(list, "b", "B"));
            DiffHost.Settle();
            Assert.False(notice.IsEffectivelyVisible);
            Assert.False(welcome.IsWorkspacesNoticeWarning);
        }
    }

    [AvaloniaFact]
    public void The_list_follows_the_store_and_a_disposed_page_stops_following_it()
    {
        var store = new FakeWorkspaceStore();
        using var page = ShowPage(store);
        var (welcome, view, host) = (page.Welcome, page.View, page.Host);
        {
            Assert.False(welcome.HasWorkspaces);
            Assert.True(Named<TextBlock>(view, "NoWorkspacesText").IsEffectivelyVisible);

            store.Update(list => WorkspaceEdits.Create(list, "z", "zeta"));
            store.Update(list => WorkspaceEdits.Create(list, "a", "Alpha"));
            DiffHost.Settle();
            Assert.True(welcome.HasWorkspaces);
            Assert.False(Named<TextBlock>(view, "NoWorkspacesText").IsEffectivelyVisible);
            Assert.Equal(["Alpha", "zeta"], welcome.Workspaces.Select(item => item.Name));
            Assert.Equal(["No repositories", "No repositories"], welcome.Workspaces.Select(item => item.CountText));
            Assert.Equal(["No repositories yet", "No repositories yet"], welcome.Workspaces.Select(item => item.ToolTipText));

            var changes = new List<string?>();
            welcome.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            welcome.Dispose();
            store.Update(list => WorkspaceEdits.Delete(list, "a"));
            Assert.Empty(changes);
        }
    }

    [AvaloniaFact]
    public void New_workspace_runs_the_sessions_create_from_the_pages_tab()
    {
        var store = new FakeWorkspaceStore();
        using var page = ShowPage(store);
        var welcome = page.Welcome;
        {
            var recording = new RecordingHost();
            page.Services.WorkspaceHost = recording;
            welcome.NewWorkspaceCommand.Execute(null);
            var dialog = Assert.IsType<VisualCommit.App.ViewModels.Dialogs.WorkspaceNameDialogViewModel>(page.Services.DialogHost.Current);
            dialog.Name = "Web";
            dialog.ConfirmCommand.Execute(null);
            DiffHost.Settle();

            var created = Assert.Single(store.Current.Workspaces);
            Assert.Equal("Web", created.Name);
            Assert.Equal((created.Id, page.Tab), Assert.Single(recording.Shown));
        }
    }

    /// <summary>The height of a line of the window's 12.5-px text, as the page lays it out.</summary>
    private const double Line = 16;

    private static WorkspaceList TwoWorkspaces(string root) => new()
    {
        Workspaces =
        [
            new Workspace("web", "web team", [.. new[] { "api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes", "old-site" }.Select(name => new WorkspaceRepository(Path.Combine(root, "src", name)))]),
            new Workspace("docs", "Docs", [new WorkspaceRepository(Path.Combine(root, "src", "handbook"))]),
        ],
    };

    private static Button ItemButton(ShellDriver app, string name) =>
        app.ActiveWelcomeView.GetVisualDescendants().OfType<Button>()
            .First(button => button.Name == "WorkspaceItem" && Avalonia.Automation.AutomationProperties.GetName(button) == name);

    private static StackPanel Stack(WelcomeView view) => view.GetVisualDescendants().OfType<StackPanel>().First();

    private static T Named<T>(Visual scope, string name)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    /// <summary>A tab's welcome page alone in a window at 440 × 700, on a session with <paramref name="store"/>.</summary>
    private static Page ShowPage(FakeWorkspaceStore store)
    {
        var settings = new MainWindowViewModelTests.FakeSettings();
        var services = new TabServices(new NoRepositories(), new NoFolders(), settings, new MemorySession(), new DateDisplay(TimeZoneInfo.Utc), workspaces: store);
        var tab = new RepoTabViewModel(services);
        var view = new WelcomeView { DataContext = tab.Welcome, FontSize = 12.5 };
        var host = DiffHost.Show(view, 440, 700, ThemeVariant.Dark);
        return new Page(tab.Welcome, tab, services, view, host);
    }

    /// <summary>A tab's welcome page shown alone: its view model, its tab, the session's services, the view and its window.</summary>
    private sealed record Page(WelcomeViewModel Welcome, RepoTabViewModel Tab, TabServices Services, WelcomeView View, DiffHost Host) : IDisposable
    {
        public void Dispose() => Host.Dispose();
    }

    /// <summary>The window as the welcome page sees it: records each workspace it is asked to show.</summary>
    private sealed class RecordingHost : IWorkspaceHost
    {
        public List<(string Id, RepoTabViewModel? From)> Shown { get; } = [];

        public void ShowWorkspace(string workspaceId, RepoTabViewModel? from) => Shown.Add((workspaceId, from));

        public IReadOnlyList<string> OpenRepositoryFolders() => [];
    }

    /// <summary>A store whose read-only cause, notice and failed saves the test sets.</summary>
    internal sealed class FakeWorkspaceStore : IWorkspaceStore
    {
        private string? _failNextSave;
        private string? _loadNotice;
        private bool _loadNoticeSet;

        public WorkspaceList Current { get; private set; } = new();

        public bool IsReadOnly => ReadOnlyCause is not null;

        public WorkspaceReadOnlyCause? ReadOnlyCause { get; init; }

        public string? Notice { get; set; }

        public bool SaveFailed { get; private set; }

        public event EventHandler? Changed;

        public void Seed(params Workspace[] workspaces) => Current = Current with { Workspaces = [.. Current.Workspaces, .. workspaces] };

        /// <summary>The next save fails with <paramref name="notice"/>; the one after succeeds and brings back the load's notice.</summary>
        public void FailNextSave(string notice) => _failNextSave = notice;

        public void Update(Func<WorkspaceList, WorkspaceList> change)
        {
            var next = change(Current);
            if (ReferenceEquals(next, Current))
            {
                return;
            }

            Current = next;
            if (!_loadNoticeSet)
            {
                _loadNotice = Notice;
                _loadNoticeSet = true;
            }

            if (_failNextSave is { } failed)
            {
                SaveFailed = true;
                Notice = failed;
                _failNextSave = null;
            }
            else if (SaveFailed)
            {
                SaveFailed = false;
                Notice = _loadNotice;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotARepositoryException(path);

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
