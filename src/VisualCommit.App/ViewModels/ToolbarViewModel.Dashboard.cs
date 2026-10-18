using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.App.ViewModels;

public sealed partial class ToolbarViewModel
{
    /// <summary>
    /// A dashboard tab's toolbar (docs/phase-6-design.md 4.10, D208): Fetch and Pull run Fetch all
    /// and Pull all over <paramref name="dashboard"/>'s rows, with its states and reasons; Push,
    /// Branch, Stash, Pop, Undo, Redo and Search are disabled without a tooltip, as on an empty tab.
    /// A static factory over a private constructor, so that <c>new ToolbarViewModel(null)</c> stays
    /// unambiguous.
    /// <para>A stub: work package WP-B builds it. Until then every button is disabled, as on an empty tab.</para>
    /// </summary>
    public static ToolbarViewModel ForDashboard(IDashboardToolbar dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        return new ToolbarViewModel(dashboard);
    }

    /// <summary>A dashboard's toolbar: an empty tab's until work package WP-B follows <paramref name="dashboard"/>.</summary>
    private ToolbarViewModel(IDashboardToolbar dashboard)
        : this((RepositoryViewModel?)null)
    {
        Dashboard = dashboard;
    }

    /// <summary>The dashboard whose Fetch all and Pull all Fetch and Pull run, or null for a repository's or an empty tab's toolbar.</summary>
    public IDashboardToolbar? Dashboard { get; }
}
