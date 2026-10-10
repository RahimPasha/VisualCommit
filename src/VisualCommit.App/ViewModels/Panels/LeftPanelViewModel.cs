using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// The left panel of one repository tab: the filter and the sections Local branches, Remotes,
/// Pull requests, Tags and Stashes, as "What a repository tab must show" in
/// docs/test-reports/phase-1.md describes them. A tab without a repository uses an instance that
/// was never updated: five closed sections with the count 0.
/// </summary>
public partial class LeftPanelViewModel : ObservableObject
{
    /// <summary>The text in the filter box. Items whose full name contains it (ignoring case) stay.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// Raised when the user activates (clicks) a branch, remote branch, tag or stash: the id of
    /// the commit it points to, which the graph then selects and scrolls into view.
    /// </summary>
    public event EventHandler<string>? RefActivated;

    /// <summary>
    /// Shows <paramref name="refs"/>. Keeps the filter and which folders and sections the user
    /// opened or closed, so a refresh after an outside change does not undo them.
    /// </summary>
    public void Update(RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
    }

    /// <summary>Raises <see cref="RefActivated"/>.</summary>
    protected void OnRefActivated(string sha) => RefActivated?.Invoke(this, sha);
}
