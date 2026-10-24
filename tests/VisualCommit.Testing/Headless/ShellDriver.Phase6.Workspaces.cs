using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 6's workspaces in the harness (docs/phase-6-design.md 17.2, D189): the workspaces file
/// and the session's tabs, dashboards among them, written as the app writes them before the
/// start; a start on a workspace; a tab of the session opened, in the background too; the welcome
/// page's Workspaces section and the "Add repositories" checklist as snapshots; and New workspace…
/// as a user does it.
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
    /// <see cref="StartWithTabs(string, IReadOnlyList{string?}, int, int?, int?, IGitPromptHandler?, bool, TimeProvider?, ToolCatalogues?)"/>
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
        TimeProvider? readClock = null,
        ToolCatalogues? tools = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        WriteSession(dataDirectory, new SessionState { Tabs = [.. tabs], ActiveTab = activeTab });
        return Start(dataDirectory, width, height, promptHandler, systemDark, readClock, tools);
    }

    /// <summary>
    /// Starts the app on <paramref name="workspaces"/> (17.2): writes them, then a session whose
    /// first tab is the dashboard of <paramref name="openDashboardOf"/> (when given) followed by
    /// <paramref name="extraTabs"/>, the first tab active, and starts as <see cref="Start"/> does.
    /// With neither a dashboard nor extra tabs the session holds no tab, so the app starts on one
    /// empty tab whose welcome page lists the workspaces. <paramref name="tools"/> is
    /// <see cref="Start"/>'s: other tool catalogues than the fixed ones, such as <see cref="HeldMergeTools.Catalogues"/>.
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
        TimeProvider? readClock = null,
        ToolCatalogues? tools = null)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        WriteWorkspaces(dataDirectory, workspaces);
        List<TabState> tabs = openDashboardOf is null ? [] : [new TabState(null, openDashboardOf)];
        tabs.AddRange(extraTabs ?? []);
        return StartWithTabs(dataDirectory, tabs, 0, width, height, promptHandler, systemDark, readClock, tools);
    }

    /// <summary>
    /// The last git call of a repository's clean-up at opening (docs/phase-4-design.md 9 (b), D139),
    /// as the call log renders it: the read of the backups it might remove
    /// (<c>IGitRepository.FindBackupsToRemoveAsync</c>), which runs outside the queue, so its record
    /// has no <see cref="GitCallRecord.OperationId"/>. The clean-up an operation runs after saving a
    /// backup makes the same call inside that operation.
    /// </summary>
    public static string OpeningCleanUpRead { get; } =
        new GitCommand("for-each-ref", "--format=%(refname) %(objectname)", GitRepository.BackupRefPrefix).DisplayText;

    /// <summary>
    /// Waits until the tab of the repository at <paramref name="path"/> has opened, whether it is
    /// the active tab or not (report part W, W1 step 9: tabs restored in the background), by
    /// <see cref="WaitForRepositoryAsync"/>'s conditions applied to that tab: its whole history
    /// loaded (and drawn by the graph control while it is the active tab), its
    /// <c>&lt;repo&gt;: working tree:</c> line logged, and its queue idle; and its clean-up at
    /// opening decided: the read of <see cref="OpeningCleanUpRead"/> recorded in its working tree
    /// outside any operation, or the line saying that the clean-up waits (an operation in progress)
    /// or could not read the backups. A clean-up that finds backups to remove starts its background
    /// operation only after that read, so a check on a repository whose backups are due waits for
    /// that operation's record as well (<c>WaitForOperationRecordAsync("Clean up backups")</c>,
    /// phase 5's way); with no backup at all (the read's output empty) nothing follows it.
    /// <paramref name="path"/> is the folder as the session or the workspace names it (a copy's
    /// <c>PathOf</c>); the tab is found by the folder it opened (<c>IGitRepository.OpenedFolder</c>)
    /// or still opens, or by git's own spelling of it, through <see cref="WorkspacePaths.Same"/>.
    /// Returns the tab's repository.
    /// </summary>
    /// <exception cref="TimeoutException">The tab did not open, or no such tab came, in time.</exception>
    public async Task<RepositoryViewModel> WaitForTabOpenedAsync(string path, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await WaitUntilReadyAsync();
        RepositoryViewModel? repository = null;
        bool Opened()
        {
            var tab = TabOf(path);
            repository = tab?.Repository;
            if (tab is null || repository is null || repository.IsSummary || ReferenceEquals(repository.Graph, CommitGraphData.Empty) || !repository.Graph.IsComplete)
            {
                return false;
            }

            // The active tab's history is the one the graph control shows, as WaitForRepositoryAsync asks.
            if (ReferenceEquals(Session.Shell.ActiveTab, tab)
                && Window.GetVisualDescendants().OfType<CommitGraphControl>().FirstOrDefault(graph => graph.Name == "GraphRows") is { } control
                && !ReferenceEquals(control.Data, repository.Graph))
            {
                return false;
            }

            return CountLogLines(AppLogFile.WorkingTreeRead(repository.Name)) > 0 && OpeningCleanUpDecided(repository);
        }

        await WaitForAsync(Opened, $"the tab of {path} to open: its history loaded, its status read, its clean-up at opening decided", timeout);
        await WaitForIdleAsync(repository, timeout);
        return repository!;
    }

    /// <summary>The tab whose repository is, or opens, at <paramref name="path"/>; null when no tab has it.</summary>
    private RepoTabViewModel? TabOf(string path) =>
        Session.Shell.Tabs.FirstOrDefault(tab =>
            new[] { tab.PendingPath, tab.FailedPath, tab.Repository?.Repository.OpenedFolder, tab.Repository?.Path }
                .Any(folder => folder is not null && WorkspacePaths.Same(folder, path)));

    /// <summary>Whether <paramref name="repository"/>'s clean-up at opening has read the backups, or said why it does not.</summary>
    private bool OpeningCleanUpDecided(RepositoryViewModel repository)
    {
        var read = GitCalls().Any(call => call.OperationId is null
            && call.CommandText == OpeningCleanUpRead
            && call.WorkingDirectory is { } folder
            && WorkspacePaths.Same(folder, repository.Path));
        if (read)
        {
            return true;
        }

        var name = Regex.Escape(repository.Name);
        return CountLogLines(new Regex($@"\] {name}: The backups' clean-up waits: ", RegexOptions.CultureInvariant)) > 0
            || CountLogLines(new Regex($@"\] {name}: the backups to clean up could not be read; they stay\.", RegexOptions.CultureInvariant)) > 0;
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
