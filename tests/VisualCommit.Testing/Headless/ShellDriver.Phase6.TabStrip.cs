using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The tab strip as phase 6 draws it (docs/phase-6-design.md 13.3, 13.4, 17.2): each tab's bounds,
/// title as drawn, tooltip, automation id, name and item status, its icon; "+"'s bounds and name;
/// the panel's offset and the active tab.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The panel that lays the tabs out (<c>TabList</c>'s items panel).</summary>
    public RepoTabStripPanel TabStripPanel
    {
        get
        {
            Settle();
            return Window.GetVisualDescendants().OfType<RepoTabStripPanel>().FirstOrDefault()
                ?? throw new InvalidOperationException("The tab strip's panel is not in the window.");
        }
    }

    /// <summary>The tab strip as drawn, tab by tab in the strip's order.</summary>
    public TabStripSnapshot TabStrip()
    {
        var panel = TabStripPanel;
        var tabs = panel.Children.Select(child =>
        {
            var border = child.GetVisualDescendants().Prepend(child).OfType<Border>().First(control => control.Name == "RepoTab");
            var model = (RepoTabViewModel)border.DataContext!;
            var button = Named<Button>(border, "TabButton");
            var title = Named<TextBlock>(border, "TabTitle");
            var icon = border.GetVisualDescendants().OfType<Control>().First(control => control.Name == "TabIcon");
            var close = Named<Button>(border, "CloseTabButton");
            return new TabSnapshot(
                model.Title,
                DrawnLines.Of(title).FirstOrDefault() ?? string.Empty,
                ToolTip.GetTip(button) as string,
                AutomationProperties.GetAutomationId(button),
                AutomationProperties.GetName(button),
                AutomationProperties.GetItemStatus(button),
                BoundsOf(border),
                BoundsOf(title),
                icon.IsEffectivelyVisible ? BoundsOf(icon) : null,
                icon.IsEffectivelyVisible ? (icon as App.Controls.Icon)?.Foreground : null,
                title.Foreground,
                button.Padding,
                model.IsActive,
                model.IsDashboard,
                close.IsEffectivelyVisible);
        }).ToList();
        var add = Find<Button>("AddRepoButton");
        return new TabStripSnapshot(
            tabs,
            BoundsOf(add),
            AutomationProperties.GetName(add),
            panel.Offset,
            panel.IsScrolling,
            Session.Shell.ActiveTabIndex,
            BoundsOf(panel));
    }
}

/// <summary>The tab strip as drawn (13.3).</summary>
/// <param name="Tabs">The tabs, left to right.</param>
/// <param name="AddButtonBounds">"+"'s bounds in the window.</param>
/// <param name="AddButtonName">"+"'s automation name.</param>
/// <param name="Offset">How far the tabs are scrolled.</param>
/// <param name="IsScrolling">The tabs overflow at 80 each and scroll.</param>
/// <param name="ActiveIndex">The active tab's index.</param>
/// <param name="PanelBounds">The panel's bounds in the window: the tabs' visible part.</param>
public sealed record TabStripSnapshot(
    IReadOnlyList<TabSnapshot> Tabs,
    Rect AddButtonBounds,
    string? AddButtonName,
    double Offset,
    bool IsScrolling,
    int ActiveIndex,
    Rect PanelBounds);

/// <summary>One tab as drawn (13.3, 13.4).</summary>
/// <param name="Title">The tab's title (the view model's).</param>
/// <param name="DrawnTitle">The title as drawn, with "…" where it was cut.</param>
/// <param name="ToolTip">The tab button's tooltip.</param>
/// <param name="AutomationId"><c>TabButton</c>'s automation id: <c>DashboardTab</c> or <c>RepoTab</c>.</param>
/// <param name="AutomationName">Its automation name.</param>
/// <param name="ItemStatus">Its item status: <c>Workspace</c> on a dashboard, none elsewhere.</param>
/// <param name="Bounds">The tab's bounds in the window (a tab scrolled out on the left starts below 0).</param>
/// <param name="TitleBounds">The title's bounds.</param>
/// <param name="IconBounds"><c>TabIcon</c>'s bounds while it shows (a dashboard's tab).</param>
/// <param name="IconForeground">The icon's brush while it shows.</param>
/// <param name="TitleForeground">The title's brush.</param>
/// <param name="ButtonPadding"><c>TabButton</c>'s padding.</param>
/// <param name="IsActive">The tab is the active one.</param>
/// <param name="IsDashboard">The tab is a workspace's dashboard.</param>
/// <param name="ShowsCloseButton">Its close button shows.</param>
public sealed record TabSnapshot(
    string Title,
    string DrawnTitle,
    string? ToolTip,
    string? AutomationId,
    string? AutomationName,
    string? ItemStatus,
    Rect Bounds,
    Rect TitleBounds,
    Rect? IconBounds,
    IBrush? IconForeground,
    IBrush? TitleForeground,
    Thickness ButtonPadding,
    bool IsActive,
    bool IsDashboard,
    bool ShowsCloseButton);
