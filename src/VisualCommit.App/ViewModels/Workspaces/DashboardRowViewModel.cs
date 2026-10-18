using CommunityToolkit.Mvvm.ComponentModel;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// One row of a dashboard: one repository of the workspace (docs/phase-6-design.md 2.6, 2.10),
/// read by its own summary-mode view model or following a tab's.
/// <para>A stub: work package WP-D builds it (its reads, cells and the <c>directoryExists</c> seam).</para>
/// </summary>
/// <param name="path">The saved folder, as <c>workspaces.json</c> holds it.</param>
public sealed partial class DashboardRowViewModel(string path) : ObservableObject
{
    /// <summary>The saved folder, as <c>workspaces.json</c> holds it; a read never rewrites it.</summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>The view model behind the row: its own summary-mode one, or the first registered tab's on its worktree (3.5); null while it has none.</summary>
    [ObservableProperty]
    public partial RepositoryViewModel? Repository { get; private set; }
}
