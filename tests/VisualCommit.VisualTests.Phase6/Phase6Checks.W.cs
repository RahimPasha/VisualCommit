// Part W of phase 6's scripted walk-through (docs/test-reports/phase-6.md, "Part W: workspaces and
// the dashboard"): checks W1 to W9 of design 17.4. This class is part W's alone; its expected
// texts and states are in Phase6Expectations.W.cs (Phase6Expectations.PartW), held against git in
// Phase6ExpectationTests.W.cs. One test method per app start, named as part W's "Test methods and
// app starts" names them (WD6, WD36): Phase6Checks.W.Welcome.cs holds W1 (parts A and B),
// Phase6Checks.W.Rows.cs W2 and W3, Phase6Checks.W.FetchAll.cs W4 and W5 (parts A and B),
// Phase6Checks.W.Failures.cs W6, Phase6Checks.W.PullAll.cs W7, Phase6Checks.W.Tabs.cs W8 and
// Phase6Checks.W.Restart.cs W9. This file holds what they share: the rows as the report writes
// them (ExpectedRow from PartW's rows), the dashboard tab's toolbar and status bar (TD, TR), the
// tab, the activity log's toggle, the call log's "only reads", the files workspaces.json and
// session.json, and the run's header texts as they come.
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>The checks of part W: workspaces and the dashboard (W1 to W9).</summary>
public sealed partial class Phase6ChecksW : Phase6Checks
{
    // ---- Rows as the report writes them. ----

    /// <summary>A tone's word as PartW writes it, as the app's tone.</summary>
    private static DashboardTone ToneOf(string tone) => tone switch
    {
        PartW.Primary => DashboardTone.Primary,
        PartW.Secondary => DashboardTone.Secondary,
        PartW.Warning => DashboardTone.Warning,
        PartW.Danger => DashboardTone.Danger,
        _ => throw new ArgumentException($"\"{tone}\" is no tone of the report.", nameof(tone)),
    };

    /// <summary>A cell of PartW as <see cref="ExpectedCell"/>.</summary>
    private static ExpectedCell Expected(PartW.Cell cell) =>
        cell.IsEmpty && cell.ToolTip is null ? ExpectedCell.Empty : new ExpectedCell(cell.Text, ToneOf(cell.Tone), cell.ToolTip, cell.Name);

    /// <summary>A row of PartW as <see cref="ExpectedRow"/>, its Repository cell's tooltip <paramref name="folder"/> (the saved path; null: not asserted).</summary>
    private static ExpectedRow Expected(PartW.Row row, string? folder) =>
        new(row.Branch, Expected(row.AheadBehind), Expected(row.Changes), Expected(row.State), Expected(row.Operation)) { Folder = folder };

    /// <summary>W0's rows of <paramref name="copy"/>, by name.</summary>
    private static Dictionary<string, PartW.Row> W0Of(TempWorkspace copy) =>
        PartW.W0(copy.PathOf("old-site")).ToDictionary(row => row.Name, StringComparer.Ordinal);

    /// <summary>
    /// Asserts the active dashboard's rows (or those of <paramref name="snapshot"/>): their names in
    /// <paramref name="rows"/>' order, and each row as the report writes it (<see cref="AssertRow"/>),
    /// its Repository cell's tooltip the row's saved path, which must name the copy's folder of that
    /// name (<see cref="SameFolder"/>; <paramref name="folders"/> overrides a row's folder), the
    /// <paramref name="selected"/> row selected and no other.
    /// </summary>
    private static DashboardSnapshot AssertRows(
        ShellDriver app,
        TempWorkspace copy,
        IReadOnlyList<PartW.Row> rows,
        string? selected = null,
        DashboardSnapshot? snapshot = null,
        IReadOnlyDictionary<string, string>? folders = null,
        IReadOnlyCollection<string>? cancel = null,
        IReadOnlyCollection<string>? details = null)
    {
        var dashboard = snapshot ?? app.DashboardSnapshot();
        Assert.Equal(rows.Select(row => row.Name), dashboard.Rows.Select(row => row.Name));
        Assert.Equal(selected, dashboard.SelectedRow);
        foreach (var row in rows)
        {
            var actual = dashboard.Row(row.Name);
            var folder = folders?.GetValueOrDefault(row.Name) ?? copy.PathOf(row.Name);
            Assert.True(SameFolder(copy, folder, actual.Path), $"The row {row.Name} holds {actual.Path}, not {folder}.");
            AssertRow(app, row.Name, Expected(row, actual.Path) with
            {
                Selected = row.Name == selected,
                ShowsCancel = cancel?.Contains(row.Name) == true,
                ShowsDetails = details?.Contains(row.Name) == true,
            }, dashboard);
        }

        return dashboard;
    }

    /// <summary>A row's Operation cell for a run's text or a result (2.6, 4.2): its text in <paramref name="tone"/>, its tooltip the same (the status bar's whole text, <c>CompactToolTip</c>).</summary>
    private static PartW.Cell Shown(string text, string tone = PartW.Secondary) => new(text, tone, text);

    /// <summary>
    /// The row <paramref name="name"/>'s <c>DashboardRowCancel</c> (2.6, 4.5) shows, named <c>Cancel</c>
    /// with the tooltip <c>Stop git</c>, or its <c>DashboardRowDetails</c> named <c>Details</c> showing
    /// <c>Details…</c>.
    /// </summary>
    private static void AssertRowButton(ShellDriver app, string name, string automationId)
    {
        var button = app.DashboardRowControl(name).GetVisualDescendants().OfType<Button>()
            .Single(candidate => AutomationProperties.GetAutomationId(candidate) == automationId);
        Assert.True(button.IsEffectivelyVisible, $"The row {name} shows no {automationId}.");
        if (automationId == "DashboardRowCancel")
        {
            Assert.Equal(("Cancel", PartW.StopGit), (AutomationProperties.GetName(button), TipOf(button)));
        }
        else
        {
            Assert.Equal("Details", AutomationProperties.GetName(button));
            Assert.Equal("Details…", button.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        }
    }

    /// <summary>The <c>*.lock</c> files under the nine repositories' git folders (none after a cancelled fetch, 4.5).</summary>
    private static List<string> LockFiles(TempWorkspace copy) =>
    [
        .. PartW.WithFolder.SelectMany(name => Directory.EnumerateFiles(Path.Combine(copy.PathOf(name), ".git"), "*.lock", SearchOption.AllDirectories)),
    ];

    /// <summary>Each repository's refs, as <c>git for-each-ref</c> lists them, against <paramref name="refs"/> (the check's own reads with <c>GIT_OPTIONAL_LOCKS=0</c>).</summary>
    private static async Task AssertRefsAsync(TempWorkspace copy, IReadOnlyDictionary<string, string[]> refs)
    {
        foreach (var name in PartW.WithFolder)
        {
            using var repo = copy.Repo(name);
            var output = (await repo.GitWithEnvironmentAsync(OptionalLocksOff, "for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput;
            Assert.True(
                refs[name].SequenceEqual(output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))),
                $"{name}'s refs are not the report's.\nExpected:\n  {string.Join("\n  ", refs[name])}\nActual:\n  {output.Replace("\n", "\n  ", StringComparison.Ordinal)}");
        }
    }

    /// <summary>The check's own git reads run with <c>GIT_OPTIONAL_LOCKS=0</c>, as the app's are, so that they refresh no index the app reads.</summary>
    private static readonly IReadOnlyDictionary<string, string?> OptionalLocksOff = new Dictionary<string, string?> { ["GIT_OPTIONAL_LOCKS"] = "0" };

    /// <summary>A repository's <c>git status --porcelain=v2 --branch</c> less <c>branch.oid</c>, the check's own read.</summary>
    private static async Task<List<string>> StatusLinesAsync(TempWorkspace copy, string name)
    {
        using var repo = copy.Repo(name);
        var output = (await repo.GitWithEnvironmentAsync(OptionalLocksOff, "status", "--porcelain=v2", "--branch")).StandardOutput;
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).Where(line => !line.StartsWith("# branch.oid ", StringComparison.Ordinal))];
    }

    /// <summary>A status against the report's lines, "…" standing for a changed file's modes and object names.</summary>
    private static void AssertStatusLines(string name, IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var matches = expected.Count == actual.Count && expected.Zip(actual).All(pair =>
            pair.First.Split(" … ") is [var start, var end]
                ? pair.Second.StartsWith(start + " ", StringComparison.Ordinal) && pair.Second.EndsWith(" " + end, StringComparison.Ordinal)
                : pair.First == pair.Second);
        Assert.True(matches, $"{name}'s status is not the report's.\nExpected:\n  {string.Join("\n  ", expected)}\nActual:\n  {string.Join("\n  ", actual)}");
    }

    /// <summary>Each drawn row of the active dashboard at y 152 + 26 k, 26 high, spanning the window (±2).</summary>
    private static void AssertRowPlaces(ShellDriver app, IReadOnlyList<string> names)
    {
        var width = app.Window.ClientSize.Width;
        for (var k = 0; k < names.Count; k++)
        {
            var bounds = app.BoundsOf(app.DashboardRowControl(names[k]));
            Assert.True(
                Math.Abs(bounds.Top - (PartW.HeadingsBottom + (PartW.RowHeight * k))) <= PartW.Tolerance
                    && Math.Abs(bounds.Height - PartW.RowHeight) <= PartW.Tolerance
                    && Math.Abs(bounds.Left) <= PartW.Tolerance
                    && Math.Abs(bounds.Right - width) <= PartW.Tolerance,
                string.Create(CultureInfo.InvariantCulture, $"The row {names[k]} is at {bounds}; row {k} lies at y {PartW.HeadingsBottom + (PartW.RowHeight * k)} to {PartW.HeadingsBottom + (PartW.RowHeight * (k + 1))}, x 0 to {width}."));
        }
    }

    /// <summary>
    /// The column headings (2.6): the shown headings in <paramref name="columns"/>' order, each 8
    /// into its column (±2), 11 and SemiBold in the secondary colour, on the chrome's background
    /// with a 1-px line at the bottom, y 124 to 152; the header y 88 to 124 above it.
    /// </summary>
    private static void AssertHeadings(ShellDriver app, (string Column, double Left, double Right)[] columns, Screenshot? picture = null)
    {
        var palette = PaletteOf(app);
        var header = app.BoundsOf((Control)app.FindByAutomationId("DashboardHeader"));
        Assert.True(Math.Abs(header.Top - PartW.HeaderTop) <= PartW.Tolerance && Math.Abs(header.Bottom - PartW.HeaderBottom) <= PartW.Tolerance, $"The header is at {header}, not y 88 to 124.");
        var headings = (Control)app.FindByAutomationId("DashboardColumnHeaders");
        var bounds = app.BoundsOf(headings);
        Assert.True(Math.Abs(bounds.Top - PartW.HeaderBottom) <= PartW.Tolerance && Math.Abs(bounds.Bottom - PartW.HeadingsBottom) <= PartW.Tolerance, $"The headings are at {bounds}, not y 124 to 152.");
        Assert.Equal(palette.Chrome, ((ISolidColorBrush)((Border)headings).Background!).Color);
        Assert.Equal(new Thickness(0, 0, 0, 1), ((Border)headings).BorderThickness);
        var shown = headings.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && text.Bounds.Width > 0).OrderBy(text => app.BoundsOf(text).X).ToList();
        Assert.Equal(columns.Select(column => column.Column), shown.Select(text => text.Text));
        foreach (var (text, column) in shown.Zip(columns))
        {
            var left = app.BoundsOf(text).Left;
            Assert.True(Math.Abs(left - (column.Left + PartW.CellInset)) <= PartW.Tolerance, $"The heading {text.Text} starts at x {left}, not {column.Left + PartW.CellInset}.");
            Assert.Equal(11, text.FontSize);
            Assert.Equal(FontWeight.SemiBold, text.FontWeight);
            Assert.Equal(palette.TextSecondary, (text.Foreground as ISolidColorBrush)?.Color);
        }

        if (picture is not null)
        {
            AssertColour(palette.Border, picture, new Point(bounds.Center.X, bounds.Bottom - 0.5), "headings' bottom line (VcBorderBrush)");
            AssertColour(palette.Border, picture, new Point(header.Center.X, header.Bottom - 0.5), "header's bottom line (VcBorderBrush)");
        }
    }

    /// <summary>
    /// The header's left part (2.5): <c>IconWorkspace</c> 14×14 from x 8 in the secondary colour,
    /// <c>DashboardTitle</c> (13, SemiBold, primary) from x 28, <c>DashboardCount</c> 10 after it
    /// (secondary); <c>DashboardMore</c> 28×28 ending at the window's width less 8; the four buttons
    /// 6 apart left of it (±2).
    /// </summary>
    private static void AssertHeaderLayout(ShellDriver app)
    {
        var palette = PaletteOf(app);
        var header = (Control)app.FindByAutomationId("DashboardHeader");
        var icon = header.GetVisualDescendants().OfType<Icon>().First(candidate => candidate.IsEffectivelyVisible);
        var iconBounds = app.BoundsOf(icon);
        Assert.True(Math.Abs(iconBounds.Left - 8) <= PartW.Tolerance && Math.Abs(iconBounds.Width - 14) <= 0.5 && Math.Abs(iconBounds.Height - 14) <= 0.5, $"The header's icon is at {iconBounds}, not 14×14 from x 8.");
        Assert.True(ReferenceEquals(Application.Current!.FindResource("IconWorkspace"), icon.Data), "The header's icon is not IconWorkspace.");
        Assert.Equal(palette.TextSecondary, (icon.Foreground as ISolidColorBrush)?.Color);
        var title = (TextBlock)app.FindByAutomationId("DashboardTitle");
        var titleBounds = app.BoundsOf(title);
        Assert.True(Math.Abs(titleBounds.Left - 28) <= PartW.Tolerance, $"DashboardTitle starts at x {titleBounds.Left}, not 28.");
        Assert.Equal((13d, FontWeight.SemiBold), (title.FontSize, title.FontWeight));
        var count = app.BoundsOf((Control)app.FindByAutomationId("DashboardCount"));
        Assert.True(Math.Abs(count.Left - titleBounds.Right - 10) <= PartW.Tolerance, $"DashboardCount starts at x {count.Left}, {count.Left - titleBounds.Right} after the title, not 10.");
        var more = app.BoundsOf((Control)app.FindByAutomationId("DashboardMore"));
        var width = app.Window.ClientSize.Width;
        Assert.True(Math.Abs(more.Right - (width - 8)) <= PartW.Tolerance && Math.Abs(more.Width - 28) <= 0.5 && Math.Abs(more.Height - 28) <= 0.5, $"DashboardMore is at {more}, not 28×28 ending at x {width - 8}.");
        var buttons = new[] { "DashboardAddRepository", "DashboardFetchAll", "DashboardPullAll", "DashboardRefresh", "DashboardMore" }
            .Select(id => app.BoundsOf((Control)app.FindByAutomationId(id)))
            .ToList();
        for (var i = 1; i < buttons.Count; i++)
        {
            Assert.True(Math.Abs(buttons[i].Left - buttons[i - 1].Right - 6) <= PartW.Tolerance, $"The header's buttons {i} and {i + 1} are {buttons[i].Left - buttons[i - 1].Right} apart, not 6.");
        }
    }

    // ---- The dashboard tab's toolbar, status bar and tab (TD, TR; 2.8, 4.9, 4.10, 13.4). ----

    /// <summary>
    /// The toolbar on a dashboard tab (TD, or TR while <paramref name="running"/> runs, part W's
    /// conventions): Fetch and Pull enabled with <c>Fetch every repository of {workspace} (Ctrl+Shift+F)</c>
    /// and <c>Pull every repository of {workspace} (Ctrl+Shift+L)</c>, or disabled with <c>Wait for
    /// … to finish</c>, the help text the tooltip; Push, Branch, Stash, Pop, Undo, Redo and Search
    /// disabled without a tooltip; Settings and Theme enabled with theirs.
    /// </summary>
    private static void AssertDashboardToolbar(ShellDriver app, string workspace, BulkKind? running = null, bool light = false)
    {
        var wait = running switch
        {
            BulkKind.Fetch => PartW.WaitForFetchAll,
            BulkKind.Pull => PartW.WaitForPullAll,
            _ => null,
        };
        AssertButton(app, "FetchButton", wait is null, wait ?? PartW.FetchAllToolTip(workspace));
        AssertButton(app, "PullButton", wait is null, wait ?? PartW.PullAllToolTip(workspace));
        foreach (var id in PartW.DisabledWithoutToolTip)
        {
            AssertButton(app, id, false, null);
        }

        AssertButton(app, "SettingsButton", true, PartW.SettingsToolTip);
        AssertButton(app, "ThemeSwitch", true, light ? PartW.ThemeToDark : PartW.ThemeToLight);
    }

    /// <summary>A toolbar button by its id: enabled or not, its tooltip (null: none) and, with one, its help text the same.</summary>
    private static void AssertButton(ShellDriver app, string id, bool enabled, string? toolTip)
    {
        var button = ToolbarButton(app, id);
        Assert.True(button.IsEffectivelyEnabled == enabled, $"{id} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}, expected {(enabled ? "enabled" : "disabled")}.");
        Assert.True(ToolTip.GetTip(button) as string == toolTip, $"{id}'s tooltip is \"{ToolTip.GetTip(button)}\", expected \"{toolTip}\".");
        if (toolTip is not null)
        {
            Assert.Equal(toolTip, AutomationProperties.GetHelpText(button));
        }
    }

    /// <summary>
    /// The status bar on a dashboard tab (2.8, 4.9): <c>StatusWorkspaceIcon</c> (13×13, secondary)
    /// and the workspace's name in the branch part; <paramref name="text"/> in its tone; the bar at
    /// <paramref name="progress"/> (null: hidden); the Cancel (<c>Cancel the rest of Fetch all</c>)
    /// shown or not; <paramref name="waiting"/> (<c>4 waiting</c>; null: hidden).
    /// </summary>
    private static void AssertDashboardStatusBar(
        ShellDriver app,
        string workspace,
        string text,
        OperationStatusTone tone = OperationStatusTone.Secondary,
        int? progress = null,
        bool cancel = false,
        string? waiting = null)
    {
        var palette = PaletteOf(app);
        var icon = (Icon)app.FindByAutomationId("StatusWorkspaceIcon");
        Assert.True(icon.IsEffectivelyVisible, "StatusWorkspaceIcon does not show on a dashboard tab.");
        Assert.True(ReferenceEquals(Application.Current!.FindResource("IconWorkspace"), icon.Data), "StatusWorkspaceIcon is not IconWorkspace.");
        Assert.Equal((13d, 13d), (icon.Bounds.Width, icon.Bounds.Height));
        Assert.Equal(palette.TextSecondary, (icon.Foreground as ISolidColorBrush)?.Color);
        AssertBranch(app, workspace);
        AssertStatus(app, text, tone);
        var bar = (ProgressBar)app.FindByAutomationId("OperationProgress");
        Assert.True(bar.IsEffectivelyVisible == progress.HasValue, $"The status bar's progress bar {(bar.IsEffectivelyVisible ? "shows" : "is hidden")}, expected {(progress.HasValue ? "shown" : "hidden")}.");
        if (progress is { } value)
        {
            Assert.False(bar.IsIndeterminate, "The status bar's bar is indeterminate during a run (4.9: determinate).");
            Assert.Equal(value, (int)Math.Round(bar.Value));
        }

        var cancelButton = (Button)app.FindByAutomationId("OperationCancelButton");
        Assert.True(cancelButton.IsEffectivelyVisible == cancel, $"The status bar's Cancel {(cancelButton.IsEffectivelyVisible ? "shows" : "is hidden")}, expected {(cancel ? "shown" : "hidden")}.");
        if (cancel)
        {
            Assert.Equal(PartW.CancelRestOfFetchAll, ToolTip.GetTip(cancelButton) as string);
            Assert.Equal("Cancel", AutomationProperties.GetName(cancelButton));
        }

        var waitingText = (TextBlock)app.FindByAutomationId("OperationWaiting");
        Assert.True(waitingText.IsEffectivelyVisible == (waiting is not null), $"OperationWaiting {(waitingText.IsEffectivelyVisible ? "shows \"" + waitingText.Text + "\"" : "is hidden")}, expected {waiting ?? "hidden"}.");
        if (waiting is not null)
        {
            Assert.Equal(waiting, waitingText.Text);
        }
    }

    /// <summary>The activity log's toggle: enabled or not, its tooltip and help text, <c>selected</c> or not.</summary>
    private static void AssertActivityToggle(ShellDriver app, bool enabled, string toolTip, bool selected = false)
    {
        var toggle = (Button)app.FindByAutomationId("ActivityLogToggle");
        Assert.True(toggle.IsEffectivelyEnabled == enabled, $"ActivityLogToggle is {(toggle.IsEffectivelyEnabled ? "enabled" : "disabled")}, expected {(enabled ? "enabled" : "disabled")}.");
        Assert.Equal(toolTip, ToolTip.GetTip(toggle) as string);
        Assert.Equal(toolTip, AutomationProperties.GetHelpText(toggle));
        Assert.Equal(selected, toggle.Classes.Contains("selected"));
    }

    /// <summary>
    /// TD (part W's conventions): the toolbar's and the header's Fetch and Pull enabled, the header
    /// idle with <paramref name="count"/> (or the last run's <paramref name="result"/> in its tone),
    /// the status bar with the workspace, <c>Ready</c> or the result, nothing running.
    /// </summary>
    private static void AssertTd(ShellDriver app, string workspace, string count, string? result = null, OperationStatusTone tone = OperationStatusTone.Secondary, bool light = false)
    {
        AssertDashboardToolbar(app, workspace, light: light);
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(workspace, count) with { RunText = result, RunTone = tone });
        AssertDashboardStatusBar(app, workspace, result ?? PartW.Ready, tone);
    }

    /// <summary>TR while Fetch all runs (4.9): the header's and the toolbar's Fetch and Pull disabled, the run's text, bar and Cancel all.</summary>
    private static void AssertTr(ShellDriver app, string count, string runText, int progress, string? waiting)
    {
        AssertDashboardToolbar(app, PartW.Workspace, BulkKind.Fetch);
        AssertDashboardHeader(app, ExpectedDashboardHeader.Running(PartW.Workspace, count, BulkKind.Fetch, runText, progress));
        AssertDashboardStatusBar(app, PartW.Workspace, runText, OperationStatusTone.Secondary, progress, cancel: true, waiting);
    }

    /// <summary>
    /// The dashboard's tab (13.4, part W's conventions): <c>DashboardTab</c>, named and tooltipped
    /// <paramref name="title"/>, item status <c>Workspace</c>, <c>TabIcon</c> 12×12 14 from the tab's
    /// left and the title 6 after it, both primary while active and secondary otherwise, its close
    /// button shown, the title whole; returns it.
    /// </summary>
    private static TabSnapshot AssertDashboardTab(ShellDriver app, TabStripSnapshot strip, int index, string title, bool active)
    {
        var palette = PaletteOf(app);
        var tab = strip.Tabs[index];
        Assert.True(tab.IsDashboard, $"Tab {index} ({tab.Title}) is not a dashboard's.");
        Assert.Equal((PartW.DashboardTabId, title, title, title, PartW.ItemStatusWorkspace), (tab.AutomationId, tab.Title, tab.AutomationName, tab.ToolTip, tab.ItemStatus));
        Assert.Equal(title, tab.DrawnTitle);
        Assert.Equal(active, tab.IsActive);
        Assert.True(tab.ShowsCloseButton, "The dashboard's tab shows no close button.");
        var icon = tab.IconBounds ?? throw new Xunit.Sdk.XunitException("The dashboard's tab shows no TabIcon.");
        Assert.True(Math.Abs(icon.Width - 12) <= 0.5 && Math.Abs(icon.Height - 12) <= 0.5, $"TabIcon is {icon.Size}, not 12×12.");
        Assert.True(Math.Abs(icon.Left - tab.Bounds.Left - 14) <= PartW.Tolerance, $"TabIcon starts {icon.Left - tab.Bounds.Left} into the tab, not 14.");
        Assert.True(Math.Abs(tab.TitleBounds.Left - icon.Right - 6) <= PartW.Tolerance, $"The tab's title starts {tab.TitleBounds.Left - icon.Right} after its icon, not 6.");
        var colour = active ? palette.TextPrimary : palette.TextSecondary;
        Assert.Equal(colour, (tab.IconForeground as ISolidColorBrush)?.Color);
        Assert.Equal(colour, (tab.TitleForeground as ISolidColorBrush)?.Color);
        return tab;
    }

    /// <summary>The tabs' titles in the strip's order.</summary>
    private static List<string> TabTitles(ShellDriver app) => [.. app.Session.Shell.Tabs.Select(tab => tab.Title)];

    // ---- The call log and the app log. ----

    /// <summary>The calls since <paramref name="sequence"/> in the copy's folder <paramref name="name"/> (by <see cref="SameFolder"/>).</summary>
    private static IReadOnlyList<GitCallRecord> CallsIn(ShellDriver app, TempWorkspace copy, long sequence, string name) =>
        app.CallsSince(sequence, folder => SameFolder(copy, copy.PathOf(name), folder));

    /// <summary>The calls since <paramref name="sequence"/> in any of the copy's ten folders.</summary>
    private static IReadOnlyList<GitCallRecord> CallsInWorkspace(ShellDriver app, TempWorkspace copy, long sequence) =>
        app.CallsSince(sequence, folder => copy.Names.Any(name => SameFolder(copy, copy.PathOf(name), folder)));

    /// <summary>Whether a call is one of the reads a row or a tab makes (part W's "Only reads"): no operation id, and one of <see cref="PartW.ReadForms"/>.</summary>
    private static bool IsRead(GitCallRecord call) =>
        call.OperationId is null && PartW.ReadForms.Any(form => call.CommandText == form || call.CommandText.StartsWith(form.EndsWith(' ') || form.EndsWith('=') ? form : form + " ", StringComparison.Ordinal));

    /// <summary>Asserts that every call in <paramref name="calls"/> is a read (part W's "Only reads").</summary>
    private static void AssertOnlyReads(IEnumerable<GitCallRecord> calls, string what)
    {
        var writes = calls.Where(call => !IsRead(call)).ToList();
        Assert.True(writes.Count == 0, $"{what}: calls that are not a row's or a tab's reads:\n" + string.Join('\n', writes.Select(call => $"{call.CommandText} (operation {call.OperationId ?? "none"}) in {call.WorkingDirectory}")));
    }

    /// <summary>
    /// Asserts that <paramref name="calls"/> (in one folder) are a row's opening and summary read
    /// (W1 step 4, W2 step 5): only reads, two openings (<c>git rev-parse --is-inside-work-tree …</c>),
    /// and the refs read's and the status read's kinds, not their number.
    /// </summary>
    private static void AssertOpeningAndSummaryRead(IReadOnlyList<GitCallRecord> calls, string what)
    {
        AssertOnlyReads(calls, what);
        Assert.True(calls.Count(call => call.CommandText == PartW.OpenCommand) == 2, $"{what}: {calls.Count(call => call.CommandText == PartW.OpenCommand)} openings, not 2 (the add's or Locate's own, then the row's probe):\n" + string.Join('\n', calls.Select(call => call.CommandText)));
        foreach (var form in PartW.SummaryReadForms)
        {
            Assert.True(calls.Any(call => call.CommandText.StartsWith(form, StringComparison.Ordinal)), $"{what}: no \"{form}…\" among the calls:\n" + string.Join('\n', calls.Select(call => call.CommandText)));
        }
    }

    /// <summary>The app log's lines since <paramref name="mark"/> that hold <paramref name="text"/>.</summary>
    private static List<string> LinesWith(ShellDriver app, int mark, string text) =>
        [.. app.LogLinesSince(mark).Where(line => line.Contains(text, StringComparison.Ordinal))];

    /// <summary>The read passes' lines of <paramref name="workspace"/> since <paramref name="mark"/>.</summary>
    private static List<string> PassLines(ShellDriver app, int mark, string workspace = PartW.Workspace) =>
        [.. app.LogLinesSince(mark).Where(line => ShellDriver.ReadPassLine(workspace).IsMatch(line))];

    /// <summary>Asserts exactly one pass line since <paramref name="mark"/>, counting <paramref name="repositories"/> (WD27).</summary>
    private static void AssertOnePass(ShellDriver app, int mark, int repositories)
    {
        var lines = PassLines(app, mark);
        Assert.True(
            lines.Count == 1 && lines[0].Contains(PartW.ReadPassStart(PartW.Workspace, repositories), StringComparison.Ordinal),
            $"Expected one pass line \"{PartW.ReadPassStart(PartW.Workspace, repositories)}… ms\"; the lines:\n" + string.Join('\n', lines));
    }

    /// <summary>"Right after the input and a settle, no row reading and no pass under way" (WD37), from the active dashboard or <paramref name="workspaceId"/>'s.</summary>
    private static void AssertNothingReads(ShellDriver app, string? workspaceId = null)
    {
        var snapshot = workspaceId is null ? app.DashboardSnapshot() : app.DashboardSnapshot(workspaceId);
        Assert.False(snapshot.IsPassRunning, "A read pass is under way.");
        var reading = snapshot.Rows.Where(row => row.IsReading).Select(row => row.Name).ToList();
        Assert.True(reading.Count == 0, "Rows have a read outstanding: " + string.Join(", ", reading));
    }

    /// <summary>The session's summary-mode view models hold none for the copy's folder <paramref name="name"/>.</summary>
    private static void AssertNoSummaryFor(ShellDriver app, TempWorkspace copy, string name)
    {
        var held = app.Session.Services.OpenRepositories.Summaries.Where(summary => SameFolder(copy, copy.PathOf(name), summary.Path)).ToList();
        Assert.True(held.Count == 0, $"OpenRepositories.Summaries holds a view model for {name}.");
    }

    // ---- workspaces.json and session.json (part W's conventions). ----

    /// <summary><c>workspaces.json</c> as written: its version and each workspace's id, name and paths in order, read as text (no store, which could set a file aside).</summary>
    private static (int Version, List<(string Id, string Name, List<string> Paths)> Workspaces) ReadWorkspacesFile(TempDirectory data)
    {
        var file = new AppPaths(data.Path).WorkspacesFile;
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        var workspaces = root.GetProperty("workspaces").EnumerateArray().Select(workspace => (
            workspace.GetProperty("id").GetString()!,
            workspace.GetProperty("name").GetString()!,
            workspace.GetProperty("repositories").EnumerateArray().Select(repository => repository.GetProperty("path").GetString()!).ToList())).ToList();
        return (root.GetProperty("version").GetInt32(), workspaces);
    }

    /// <summary>Asserts <c>workspaces.json</c>: version 1, one workspace with <paramref name="id"/> (null: 32 lowercase hex digits, WD26) and <paramref name="name"/>, its paths naming the copy's folders <paramref name="names"/> in order (<see cref="SameFolder"/>; <paramref name="folders"/> overrides a name's folder). Returns the id.</summary>
    private static string AssertWorkspacesFile(TempDirectory data, TempWorkspace copy, string? id, string name, IReadOnlyList<string> names, IReadOnlyDictionary<string, string>? folders = null)
    {
        var (version, workspaces) = ReadWorkspacesFile(data);
        Assert.Equal(1, version);
        var workspace = Assert.Single(workspaces);
        if (id is null)
        {
            Assert.Matches("^[0-9a-f]{32}$", workspace.Id);
        }
        else
        {
            Assert.Equal(id, workspace.Id);
        }

        Assert.Equal(name, workspace.Name);
        Assert.True(workspace.Paths.Count == names.Count, $"workspaces.json holds {workspace.Paths.Count} paths, not {names.Count}:\n" + string.Join('\n', workspace.Paths));
        foreach (var (path, repository) in workspace.Paths.Zip(names))
        {
            var folder = folders?.GetValueOrDefault(repository) ?? copy.PathOf(repository);
            Assert.True(SameFolder(copy, folder, path), $"workspaces.json holds {path} where {repository} ({folder}) was expected.");
        }

        return workspace.Id;
    }

    /// <summary><c>session.json</c> as the store reads it.</summary>
    private static SessionState ReadSession(TempDirectory data) => new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current;

    // ---- The run's header texts as they come (W5, WD12). ----

    /// <summary>Records every text <c>DashboardBulkText</c> shows (the run's <c>RunText</c>) from its start until disposed, repeats left out.</summary>
    private sealed class RunTextRecorder : IDisposable
    {
        private readonly DashboardBulkViewModel _bulk;
        private readonly List<string> _texts = [];
        private readonly Lock _gate = new();

        public RunTextRecorder(DashboardBulkViewModel bulk)
        {
            _bulk = bulk;
            _bulk.PropertyChanged += OnChanged;
        }

        public IReadOnlyList<string> Texts
        {
            get
            {
                lock (_gate)
                {
                    return [.. _texts];
                }
            }
        }

        public void Dispose() => _bulk.PropertyChanged -= OnChanged;

        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (null or nameof(DashboardBulkViewModel.RunText)) || _bulk.RunText is not { } text)
            {
                return;
            }

            lock (_gate)
            {
                if (_texts.Count == 0 || _texts[^1] != text)
                {
                    _texts.Add(text);
                }
            }
        }
    }

    // ---- Small helpers. ----

    /// <summary>The window's control with <paramref name="automationId"/>, as <typeparamref name="T"/>.</summary>
    private static T Control<T>(ShellDriver app, string automationId)
        where T : Control => (T)app.FindByAutomationId(automationId);

    /// <summary>A control's tooltip as text.</summary>
    private static string? TipOf(Control control) => ToolTip.GetTip(control) as string;

    /// <summary>The menu at <paramref name="pointer"/>'s right-click on a row: its top-left 2 right of and 2 below the pointer, moved up only where it would pass y 696 (part W's "Dashboard rows").</summary>
    private static void AssertRowMenuPlace(ShellDriver app, MenuSnapshot menu, Point pointer)
    {
        AssertMenuShape(app, menu);
        var height = app.Window.ClientSize.Height;
        var expected = pointer + new Point(2, 2);
        if (expected.Y + menu.Bounds.Height > height - 4)
        {
            expected = expected.WithY(height - 4 - menu.Bounds.Height);
        }

        Assert.True(
            Math.Abs(menu.Bounds.X - expected.X) <= PartW.Tolerance && Math.Abs(menu.Bounds.Y - expected.Y) <= PartW.Tolerance,
            $"The menu's top-left is {menu.Bounds.TopLeft}, expected {expected}.");
    }

    /// <summary>The point where a check clicks a row (WD22): x 66 in its Repository cell, at its centre.</summary>
    private static Point RowPoint(ShellDriver app, string name)
    {
        var point = app.DashboardRowPoint(name);
        Assert.True(Math.Abs(point.X - PartW.RowClickX) <= 0.5, $"The row {name}'s Repository cell's centre is at x {point.X}, not 66.");
        return point;
    }
}
