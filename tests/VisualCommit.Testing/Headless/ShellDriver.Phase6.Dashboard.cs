using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 6's dashboard as the checks read and drive it (docs/phase-6-design.md 17.2; the report's
/// part W): the header and every row as drawn (<see cref="DashboardSnapshot()"/>, with what a
/// trigger decided, WD37), the waits for a read pass and for a row, a row opened or its menu, the
/// window's activation (2.10's trigger 2) and the git calls since a mark in one folder.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The active tab's dashboard, or null on any other tab.</summary>
    public DashboardViewModel? ActiveDashboard => Session.Shell.ActiveTab.Dashboard;

    /// <summary>
    /// The active dashboard as drawn (2.5, 2.6): the header, and per row its cells' texts, tones,
    /// tooltips, automation names and whether each is cut, whether <c>Cancel</c> or
    /// <c>Details…</c> shows, whether it is selected, whether it has a read outstanding
    /// (<c>IsReading</c>), and whether a pass is under way (<c>IsPassRunning</c>, WD37). A row in
    /// view is read from its drawn cells; one out of view from its view model.
    /// </summary>
    /// <exception cref="InvalidOperationException">The active tab is no dashboard.</exception>
    public DashboardSnapshot DashboardSnapshot()
    {
        Settle();
        var dashboard = ActiveDashboard ?? throw new InvalidOperationException("The active tab is no dashboard.");
        var view = Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == "Dashboard" && control.IsEffectivelyVisible && ReferenceEquals(control.DataContext, dashboard));
        return SnapshotOf(dashboard, view);
    }

    /// <summary>The dashboard of the workspace <paramref name="workspaceId"/>, active or not, from its view models (WD37: a dashboard in the background).</summary>
    /// <exception cref="InvalidOperationException">No tab shows that workspace's dashboard.</exception>
    public DashboardSnapshot DashboardSnapshot(string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        Settle();
        var dashboard = Session.Shell.Tabs.Select(tab => tab.Dashboard).FirstOrDefault(candidate => candidate?.WorkspaceId == workspaceId)
            ?? throw new InvalidOperationException($"No tab shows the dashboard of the workspace {workspaceId}.");
        return ReferenceEquals(dashboard, ActiveDashboard) ? DashboardSnapshot() : SnapshotOf(dashboard, view: null);
    }

    /// <summary>
    /// Waits for a read pass of the active dashboard (2.10): its line
    /// <c>{workspace}: read … repositories in {ms} ms</c> logged after <paramref name="since"/> (a
    /// <see cref="LogMark"/>; the start when null), then no row showing <c>Reading…</c> and no pass
    /// under way.
    /// </summary>
    public async Task WaitForDashboardReadAsync(int? since = null, TimeSpan? timeout = null)
    {
        var dashboard = ActiveDashboard ?? throw new InvalidOperationException("The active tab is no dashboard.");
        await WaitForLogLineAsync(ReadPassLine(dashboard.Name), mark: since ?? 0, timeout: timeout);
        await WaitForAsync(
            () => !dashboard.IsPassRunning && dashboard.Rows.All(row => row.State.Text != DashboardTexts.ReadingText),
            $"no row of {dashboard.Name} to read",
            timeout);
    }

    /// <summary>The pattern of a read pass's log line of <paramref name="workspace"/> (14.14).</summary>
    public static Regex ReadPassLine(string workspace) =>
        new($@"{Regex.Escape(workspace)}: read \d+ repositor(y|ies) in \d+ ms", RegexOptions.CultureInvariant);

    /// <summary>Waits until the active dashboard's row <paramref name="name"/> satisfies <paramref name="predicate"/>, and returns it.</summary>
    public async Task<DashboardRowSnapshot> WaitForRowAsync(string name, Func<DashboardRowSnapshot, bool> predicate, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(predicate);
        DashboardRowSnapshot? found = null;
        await WaitForAsync(
            () => (found = DashboardSnapshot().Rows.FirstOrDefault(row => row.Name == name)) is { } row && predicate(row),
            $"the row {name} of the dashboard",
            timeout);
        return found!;
    }

    /// <summary>The drawn <c>DashboardRow</c> of the active dashboard named <paramref name="name"/>.</summary>
    public ListBoxItem DashboardRowControl(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Settle();
        return DashboardListControl().GetVisualDescendants().OfType<ListBoxItem>()
            .FirstOrDefault(item => item.DataContext is DashboardRowViewModel row && row.Name == name && item.IsEffectivelyVisible)
            ?? throw new InvalidOperationException($"No row {name} is drawn on the dashboard.");
    }

    /// <summary>Where a check clicks a row (WD22): in its Repository cell, at x 66 of the window.</summary>
    public Point DashboardRowPoint(string name)
    {
        var bounds = BoundsOf(DashboardRowControl(name));
        return new Point(bounds.X + (DashboardColumnsWidth.Repository / 2), bounds.Center.Y);
    }

    /// <summary>Double-clicks the row <paramref name="name"/> in its Repository cell (2.3) and lets the UI thread run.</summary>
    public Task OpenRowAsync(string name)
    {
        DoubleClick(DashboardRowPoint(name));
        return Task.CompletedTask;
    }

    /// <summary>Right-clicks the row <paramref name="name"/> in its Repository cell and returns the menu that opened (2.7).</summary>
    public Task<MenuSnapshot> RowMenuAsync(string name)
    {
        RightClick(DashboardRowPoint(name));
        return Task.FromResult(Menu());
    }

    /// <summary>
    /// The window is activated (2.10's trigger 2, Q4): raises the window's <c>Activated</c> as the
    /// platform would when the user comes back to it. Headless, <c>Window.Activate()</c> of a window
    /// that is already active raises nothing, so the window's handler is called directly
    /// (<c>MainWindowViewModel.OnWindowActivated</c>, the one thing <c>MainWindow</c>'s handler does).
    /// </summary>
    public void ActivateWindow()
    {
        Session.Shell.OnWindowActivated();
        Settle();
    }

    /// <summary>The highest sequence number of the git calls so far: a mark for <see cref="CallsSince"/>.</summary>
    public long CallMark() => GitCalls().Select(call => call.Sequence).DefaultIfEmpty(0).Max();

    /// <summary>
    /// The git calls recorded after <paramref name="sequence"/> (<see cref="GitCallRecord.Sequence"/>),
    /// all of them, or those whose folder <paramref name="inFolder"/> accepts. A call's folder is
    /// the one git ran in, as git spells it (<c>IGitRepository.WorkingDirectory</c>): on macOS
    /// <c>/private/var/…</c> where the saved path says <c>/var/…</c>, which
    /// <see cref="WorkspacePaths.Same"/> never equates. So match it with git's spelling: a view
    /// model's <c>Repository.WorkingDirectory</c>, or part W's <c>SameFolder</c> (the saved path or
    /// git's <c>--show-toplevel</c> of it). A call is recorded when it ends.
    /// </summary>
    public IReadOnlyList<GitCallRecord> CallsSince(long sequence, Func<string, bool>? inFolder = null) =>
        [.. GitCalls().Where(call => call.Sequence > sequence
            && (inFolder is null || (call.WorkingDirectory is { } folder && inFolder(folder))))];

    private static DashboardSnapshot SnapshotOf(DashboardViewModel dashboard, Control? view)
    {
        var drawn = view?.GetVisualDescendants().OfType<ListBoxItem>()
            .Where(item => item.DataContext is DashboardRowViewModel && item.IsEffectivelyVisible)
            .ToDictionary(item => (DashboardRowViewModel)item.DataContext!, item => item)
            ?? [];
        var rows = dashboard.Rows.Select(row => RowOf(row, drawn.GetValueOrDefault(row), ReferenceEquals(dashboard.SelectedRow, row))).ToList();
        string? Text(string id) => view is null ? null : DashboardFind(view, id) switch
        {
            TextBlock { IsEffectivelyVisible: true } text => text.Text,
            _ => null,
        };

        return new DashboardSnapshot(
            dashboard.Name,
            dashboard.CountText,
            dashboard.Notice,
            dashboard.ShowsBulkText ? dashboard.Bulk.Text : null,
            !dashboard.HasRows,
            dashboard.IsPassRunning,
            dashboard.SelectedRow?.Name,
            view is not null,
            Text("DashboardTitle"),
            Text("DashboardCount"),
            rows);
    }

    private static DashboardRowSnapshot RowOf(DashboardRowViewModel row, ListBoxItem? item, bool selected)
    {
        DashboardCellSnapshot Cell(string id, DashboardCellText fallback)
        {
            if (item is not null && DashboardFind(item, id) is { } control)
            {
                var text = control as TextBlock ?? control.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.IsEffectivelyVisible);
                return new DashboardCellSnapshot(
                    text?.Text ?? string.Empty,
                    fallback.Tone,
                    ToolTip.GetTip(control) as string ?? (text is not null ? ToolTip.GetTip(text) as string : null),
                    AutomationProperties.GetName(control),
                    text is TrimmedTextBlock { IsTrimmed: true },
                    control.IsEffectivelyVisible,
                    DashboardColorOf(text?.Foreground));
            }

            return new DashboardCellSnapshot(fallback.Text, fallback.Tone, fallback.ToolTip, fallback.Text, false, item is not null, null);
        }

        var aheadBehind = row.AheadBehind;
        return new DashboardRowSnapshot(
            row.Name,
            row.Path,
            Cell("DashboardRowName", new DashboardCellText(row.Name, DashboardTone.Primary, row.NameToolTip)),
            Cell("DashboardRowFolder", new DashboardCellText(row.FolderText, DashboardTone.Secondary, null)),
            Cell("DashboardRowBranch", new DashboardCellText(row.BranchText, DashboardTone.Primary, null)),
            Cell("DashboardRowAheadBehind", new DashboardCellText(aheadBehind.Text, aheadBehind.Tone, aheadBehind.ToolTip)) with
            {
                AutomationName = aheadBehind.AutomationName,
                Text = aheadBehind.ShowsArrows ? AheadBehindDrawn(aheadBehind) : aheadBehind.Text,
            },
            Cell("DashboardRowChanges", row.Changes),
            Cell("DashboardRowState", row.State),
            Cell("DashboardRowOperation", row.Operation),
            row.ShowsCancel,
            row.ShowsDetails,
            selected,
            row.IsReading,
            item is not null);
    }

    /// <summary>The arrows' cell as part W writes it: <c>↑ 2</c>, <c>↓ 3</c>, <c>↑ 1 ↓ 2</c>.</summary>
    private static string AheadBehindDrawn(DashboardAheadBehind cell) =>
        string.Join(' ', new[]
        {
            cell.ShowsAhead ? string.Create(CultureInfo.InvariantCulture, $"↑ {cell.Ahead}") : null,
            cell.ShowsBehind ? string.Create(CultureInfo.InvariantCulture, $"↓ {cell.Behind}") : null,
        }.OfType<string>());

    private static Control? DashboardFind(Visual scope, string automationId) =>
        scope.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == automationId);

    private static string? DashboardColorOf(IBrush? brush) =>
        brush is ISolidColorBrush solid ? $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : null;

    private ListBox DashboardListControl() =>
        Window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(list => list.Name == "DashboardList" && list.IsEffectivelyVisible)
        ?? throw new InvalidOperationException("No dashboard list is shown.");

    /// <summary>The fixed widths the driver needs of 2.6's columns.</summary>
    private static class DashboardColumnsWidth
    {
        public const double Repository = 132;
    }
}

/// <summary>A dashboard as <see cref="ShellDriver.DashboardSnapshot()"/> reads it (2.5, 2.6, WD37).</summary>
/// <param name="Title">The workspace's name.</param>
/// <param name="Count">The header's count.</param>
/// <param name="Notice"><c>DashboardNotice</c>'s text, or null while hidden.</param>
/// <param name="BulkText">The run's text in the header, or null before the first run.</param>
/// <param name="ShowsEmpty">The empty state shows.</param>
/// <param name="IsPassRunning">A read pass is under way.</param>
/// <param name="SelectedRow">The selected row's name, or null.</param>
/// <param name="IsDrawn">The dashboard is the window's shown one: its texts are read from its controls.</param>
/// <param name="DrawnTitle"><c>DashboardTitle</c>'s drawn text, or null when not drawn.</param>
/// <param name="DrawnCount"><c>DashboardCount</c>'s drawn text, or null when not drawn.</param>
/// <param name="Rows">The rows in the workspace's order.</param>
public sealed record DashboardSnapshot(
    string Title,
    string Count,
    string? Notice,
    string? BulkText,
    bool ShowsEmpty,
    bool IsPassRunning,
    string? SelectedRow,
    bool IsDrawn,
    string? DrawnTitle,
    string? DrawnCount,
    IReadOnlyList<DashboardRowSnapshot> Rows)
{
    /// <summary>The row named <paramref name="name"/>.</summary>
    public DashboardRowSnapshot Row(string name) =>
        Rows.FirstOrDefault(row => row.Name == name) ?? throw new InvalidOperationException($"The dashboard has no row {name}.");
}

/// <summary>One row as <see cref="ShellDriver.DashboardSnapshot()"/> reads it (2.6).</summary>
public sealed record DashboardRowSnapshot(
    string Name,
    string Path,
    DashboardCellSnapshot Repository,
    DashboardCellSnapshot Folder,
    DashboardCellSnapshot Branch,
    DashboardCellSnapshot AheadBehind,
    DashboardCellSnapshot Changes,
    DashboardCellSnapshot State,
    DashboardCellSnapshot Operation,
    bool ShowsCancel,
    bool ShowsDetails,
    bool IsSelected,
    bool IsReading,
    bool IsDrawn)
{
    /// <summary>The row as part W writes it: <c>name · branch · ahead / behind · changes · state · operation</c>, "empty" for an empty cell.</summary>
    public override string ToString() =>
        string.Join(" · ", new[] { Name, Branch.Text, AheadBehind.Text, Changes.Text, State.Text, Operation.Text }.Select(text => text.Length == 0 ? "empty" : text));
}

/// <summary>One cell: its drawn text, its tone, its tooltip, its automation name, whether it is cut, whether it shows and the drawn colour (#RRGGBB) when drawn.</summary>
public sealed record DashboardCellSnapshot(string Text, DashboardTone Tone, string? ToolTip, string? AutomationName, bool IsTrimmed, bool IsShown, string? Color);
