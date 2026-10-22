using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 6's workspaces in the harness (docs/phase-6-design.md 17.2, D189): the workspaces file
/// and the session's tabs, dashboards among them, written as the app writes them before the
/// start; a start on a workspace; the welcome page's Workspaces section and the "Add
/// repositories" checklist as snapshots; and New workspace… as a user does it.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// Writes the workspaces file of a data folder (<see cref="AppPaths.WorkspacesFile"/>) before the
    /// app starts on it, through the store's own <see cref="JsonWorkspaceStore.Write"/>, as
    /// <see cref="WriteSession"/> writes the session.
    /// </summary>
    public static void WriteWorkspaces(string dataDirectory, WorkspaceList workspaces) =>
        JsonWorkspaceStore.Write(new AppPaths(dataDirectory).WorkspacesFile, workspaces);

    /// <summary>
    /// Starts the app as <see cref="Start"/> does, after writing a session file with these tabs:
    /// <see cref="StartWithTabs(string, IReadOnlyList{string?}, int, int?, int?, IGitPromptHandler?, bool, TimeProvider?)"/>
    /// for any tab the session can hold, a workspace's dashboard (<c>new TabState(null, workspaceId)</c>,
    /// docs/phase-6-design.md 1.11) included. Write the workspaces first (<see cref="WriteWorkspaces"/>).
    /// </summary>
    public static ShellDriver StartWithTabs(
        string dataDirectory,
        IReadOnlyList<TabState> tabs,
        int activeTab = 0,
        int? width = 1100,
        int? height = 700,
        IGitPromptHandler? promptHandler = null,
        bool systemDark = true,
        TimeProvider? readClock = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        WriteSession(dataDirectory, new SessionState { Tabs = [.. tabs], ActiveTab = activeTab });
        return Start(dataDirectory, width, height, promptHandler, systemDark, readClock);
    }

    /// <summary>
    /// Starts the app on <paramref name="workspaces"/> (17.2): writes them, then a session whose
    /// first tab is the dashboard of <paramref name="openDashboardOf"/> (when given) followed by
    /// <paramref name="extraTabs"/>, the first tab active, and starts as <see cref="Start"/> does.
    /// With neither a dashboard nor extra tabs the session holds no tab, so the app starts on one
    /// empty tab whose welcome page lists the workspaces.
    /// </summary>
    public static ShellDriver StartWithWorkspace(
        string dataDirectory,
        WorkspaceList workspaces,
        string? openDashboardOf = null,
        IReadOnlyList<TabState>? extraTabs = null,
        int? width = 1100,
        int? height = 700,
        IGitPromptHandler? promptHandler = null,
        bool systemDark = true,
        TimeProvider? readClock = null)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        WriteWorkspaces(dataDirectory, workspaces);
        List<TabState> tabs = openDashboardOf is null ? [] : [new TabState(null, openDashboardOf)];
        tabs.AddRange(extraTabs ?? []);
        return StartWithTabs(dataDirectory, tabs, 0, width, height, promptHandler, systemDark, readClock);
    }

    /// <summary>The active tab's welcome page as the window draws it; throws when the active tab shows none.</summary>
    public WelcomeView ActiveWelcomeView
    {
        get
        {
            Settle();
            var welcome = Session.Shell.ActiveTab.Welcome;
            return Window.GetVisualDescendants().OfType<WelcomeView>()
                .FirstOrDefault(view => ReferenceEquals(view.DataContext, welcome) && view.IsEffectivelyVisible)
                ?? throw new InvalidOperationException("The active tab shows no welcome page.");
        }
    }

    /// <summary>
    /// The active tab's welcome page's Workspaces section (13.1) as drawn: the heading, the button,
    /// the notice, the empty text and the items, each with its bounds in the window.
    /// </summary>
    public WelcomeWorkspacesSnapshot WelcomeWorkspaces()
    {
        var view = ActiveWelcomeView;
        var heading = Named<TextBlock>(view, "WorkspacesHeading");
        var button = Named<Button>(view, "NewWorkspaceButton");
        var notice = Named<TextBlock>(view, "WorkspacesNotice");
        var none = Named<TextBlock>(view, "NoWorkspacesText");
        var list = Named<ItemsControl>(view, "WorkspaceList");
        var items = list.IsEffectivelyVisible
            ? list.GetVisualDescendants().OfType<Button>().Where(item => item.Name == "WorkspaceItem").Select(item =>
            {
                var name = Named<TextBlock>(item, "WorkspaceItemName");
                var count = Named<TextBlock>(item, "WorkspaceItemCount");
                return new WelcomeWorkspaceItemSnapshot(
                    name.Text ?? string.Empty,
                    count.Text ?? string.Empty,
                    ToolTip.GetTip(item) as string,
                    AutomationProperties.GetName(item),
                    BoundsOf(item),
                    BoundsOf(name),
                    BoundsOf(count),
                    name.Foreground,
                    count.Foreground,
                    DrawnLines.Of(name).FirstOrDefault() ?? string.Empty);
            }).ToList()
            : [];
        return new WelcomeWorkspacesSnapshot(
            heading.Text ?? string.Empty,
            BoundsOf(heading),
            heading.Foreground,
            button.IsEffectivelyEnabled,
            ToolTip.GetTip(button) as string,
            AutomationProperties.GetName(button),
            AutomationProperties.GetHelpText(button),
            BoundsOf(button),
            notice.IsEffectivelyVisible ? notice.Text : null,
            notice.IsEffectivelyVisible ? notice.Foreground : null,
            none.IsEffectivelyVisible,
            items);
    }

    /// <summary>
    /// The "Add repositories" checklist on top (1.9) as drawn: its texts, its rows in the list's
    /// order with whether each is in view, its buttons; throws when it is not the dialog on top.
    /// </summary>
    public AddRepositoriesSnapshot AddRepositories()
    {
        var dialog = Dialog<AddRepositoriesDialogViewModel>();
        var view = TopDialogView as AddRepositoriesDialogView
            ?? throw new InvalidOperationException("The checklist's view is not shown.");
        var card = Named<Border>(view, "AddRepositoriesCard");
        var folder = Named<TextBlock>(view, "ScanFolderText");
        var progress = Named<TextBlock>(view, "ScanProgressText");
        var list = Named<ItemsControl>(view, "FoundList");
        var stopped = Named<TextBlock>(view, "ScanStoppedText");
        var failed = Named<TextBlock>(view, "ScanFailedText");
        var confirm = Named<Button>(view, "ConfirmButton");
        var listShown = list.IsEffectivelyVisible;
        var listBounds = listShown ? BoundsOf(list.GetVisualAncestors().OfType<ScrollViewer>().First()) : (Rect?)null;
        var rows = listShown
            ? list.GetVisualDescendants().OfType<DialogRow>().Select(row =>
            {
                var model = (FoundRepositoryRow)row.DataContext!;
                var check = Named<CheckBox>(row, "FoundRepositoryCheck");
                var detail = Named<TextBlock>(row, "FoundRepositoryDetail");
                var bounds = BoundsOf(row);
                return new FoundRepositorySnapshot(
                    model.Name,
                    detail.Text ?? string.Empty,
                    check.IsChecked == true,
                    check.IsEffectivelyEnabled,
                    ToolTip.GetTip(detail) as string,
                    AutomationProperties.GetName(row),
                    bounds,
                    listBounds is { } shown && bounds.Top >= shown.Top - 0.5 && bounds.Bottom <= shown.Bottom + 0.5);
            }).ToList()
            : [];
        return new AddRepositoriesSnapshot(
            dialog.Title,
            folder.Text ?? string.Empty,
            DrawnLines.LinesOf(folder),
            progress.IsEffectivelyVisible ? progress.Text : null,
            rows,
            stopped.IsEffectivelyVisible ? stopped.Text : null,
            failed.IsEffectivelyVisible ? failed.Text : null,
            failed.IsEffectivelyVisible ? ToolTip.GetTip(failed) as string : null,
            confirm.Content as string ?? string.Empty,
            confirm.IsEffectivelyEnabled,
            BoundsOf(card),
            listBounds);
    }

    /// <summary>Waits until the checklist on top has finished its search and its opens (1.9's done state).</summary>
    public async Task<AddRepositoriesSnapshot> WaitForAddRepositoriesDoneAsync(TimeSpan? timeout = null)
    {
        await WaitForAsync(
            () => Session.Services.DialogHost.Current is AddRepositoriesDialogViewModel { IsDone: true },
            "the Add repositories checklist's search to end",
            timeout);
        return AddRepositories();
    }

    /// <summary>
    /// New workspace… as a user does it on the active tab's welcome page (1.6): clicks
    /// <c>NewWorkspaceButton</c>, types <paramref name="name"/> into the name dialog, clicks
    /// <c>Create</c> and waits until the dialog has closed and the store holds the workspace.
    /// Returns its id.
    /// </summary>
    public async Task<string> NewWorkspaceAsync(string name, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        var before = Session.Services.Workspaces.Current.Workspaces.Select(workspace => workspace.Id).ToHashSet(StringComparer.Ordinal);
        Click(Named<Button>(ActiveWelcomeView, "NewWorkspaceButton"));
        await WaitForDialogAsync<WorkspaceNameDialogViewModel>(timeout);
        TypeInto("NameBox", name);
        Click(FindByAutomationId("ConfirmButton"));
        Workspace? created = null;
        await WaitForAsync(
            () => Session.Services.DialogHost.Current is not WorkspaceNameDialogViewModel
                && (created = Session.Services.Workspaces.Current.Workspaces.FirstOrDefault(workspace => !before.Contains(workspace.Id))) is not null,
            $"the workspace {name} to be created",
            timeout);
        return created!.Id;
    }
}

/// <summary>The welcome page's Workspaces section as drawn (13.1).</summary>
/// <param name="Heading"><c>WorkspacesHeading</c>'s text.</param>
/// <param name="HeadingBounds">Its bounds in the window.</param>
/// <param name="HeadingForeground">Its brush.</param>
/// <param name="NewWorkspaceEnabled"><c>NewWorkspaceButton</c> is enabled.</param>
/// <param name="NewWorkspaceToolTip">Its tooltip.</param>
/// <param name="NewWorkspaceName">Its automation name.</param>
/// <param name="NewWorkspaceHelpText">Its help text.</param>
/// <param name="NewWorkspaceBounds">Its bounds in the window.</param>
/// <param name="Notice"><c>WorkspacesNotice</c>'s text while it shows, else null.</param>
/// <param name="NoticeForeground">Its brush while it shows.</param>
/// <param name="ShowsNoWorkspaces"><c>NoWorkspacesText</c> shows.</param>
/// <param name="Items">The <c>WorkspaceItem</c>s, top to bottom.</param>
public sealed record WelcomeWorkspacesSnapshot(
    string Heading,
    Rect HeadingBounds,
    IBrush? HeadingForeground,
    bool NewWorkspaceEnabled,
    string? NewWorkspaceToolTip,
    string? NewWorkspaceName,
    string? NewWorkspaceHelpText,
    Rect NewWorkspaceBounds,
    string? Notice,
    IBrush? NoticeForeground,
    bool ShowsNoWorkspaces,
    IReadOnlyList<WelcomeWorkspaceItemSnapshot> Items);

/// <summary>One <c>WorkspaceItem</c> of the welcome page as drawn.</summary>
/// <param name="Name">The workspace's name.</param>
/// <param name="CountText"><c>{N} repositories</c>, <c>1 repository</c>, <c>No repositories</c>.</param>
/// <param name="ToolTip">Its tooltip: the folder names.</param>
/// <param name="AutomationName">Its automation name.</param>
/// <param name="Bounds">The item's bounds in the window.</param>
/// <param name="NameBounds">The name's bounds.</param>
/// <param name="CountBounds">The count's bounds.</param>
/// <param name="NameForeground">The name's brush.</param>
/// <param name="CountForeground">The count's brush.</param>
/// <param name="DrawnName">The name as drawn, with "…" where it was cut.</param>
public sealed record WelcomeWorkspaceItemSnapshot(
    string Name,
    string CountText,
    string? ToolTip,
    string? AutomationName,
    Rect Bounds,
    Rect NameBounds,
    Rect CountBounds,
    IBrush? NameForeground,
    IBrush? CountForeground,
    string DrawnName);

/// <summary>The "Add repositories" checklist as drawn (1.9).</summary>
/// <param name="Title"><c>AddRepositoriesTitle</c>.</param>
/// <param name="FolderText"><c>ScanFolderText</c>'s text.</param>
/// <param name="FolderLines">Its drawn lines (13.2's rule: a folder breaks only after a separator).</param>
/// <param name="ProgressText"><c>ScanProgressText</c> while it shows, else null.</param>
/// <param name="Rows">The <c>FoundRepository</c> rows in the list's order.</param>
/// <param name="StoppedText"><c>ScanStoppedText</c> while it shows, else null.</param>
/// <param name="FailedText"><c>ScanFailedText</c> while it shows, else null.</param>
/// <param name="FailedToolTip">Its tooltip.</param>
/// <param name="ConfirmText">The confirm button's label.</param>
/// <param name="ConfirmEnabled">The confirm button is enabled.</param>
/// <param name="CardBounds">The card's bounds in the window.</param>
/// <param name="ListBounds">The list's visible part (its scroll viewer) while it shows.</param>
public sealed record AddRepositoriesSnapshot(
    string Title,
    string FolderText,
    IReadOnlyList<DrawnLine> FolderLines,
    string? ProgressText,
    IReadOnlyList<FoundRepositorySnapshot> Rows,
    string? StoppedText,
    string? FailedText,
    string? FailedToolTip,
    string ConfirmText,
    bool ConfirmEnabled,
    Rect CardBounds,
    Rect? ListBounds);

/// <summary>One row of the checklist as drawn.</summary>
/// <param name="Name">The repository's name.</param>
/// <param name="Detail">Its relative path, <c>Already in this workspace</c> or <c>Contains {folder name}</c>.</param>
/// <param name="IsChecked">Its box is checked.</param>
/// <param name="IsEnabled">Its box can be changed.</param>
/// <param name="ToolTip">The detail's tooltip.</param>
/// <param name="AutomationName">The row's automation name.</param>
/// <param name="Bounds">The row's bounds in the window.</param>
/// <param name="InView">The row lies whole inside the list's visible part.</param>
public sealed record FoundRepositorySnapshot(
    string Name,
    string Detail,
    bool IsChecked,
    bool IsEnabled,
    string? ToolTip,
    string? AutomationName,
    Rect Bounds,
    bool InView);
