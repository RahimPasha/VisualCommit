using VisualCommit.Core.Session;

namespace VisualCommit.App.ViewModels;

// The session's tabs (D46, docs/phase-6-design.md 1.11): what is restored at start and what is
// saved. Work package WP-W adds the dashboards: their restore first, the saved workspace ids.
public partial class MainWindowViewModel
{
    /// <summary>
    /// The tabs of <paramref name="saved"/>, there from the first frame under their folder names
    /// (<c>InitializeAsync</c> opens them once git has been found), and the active one; one empty
    /// tab when none was saved.
    /// </summary>
    private void RestoreTabs(SessionState saved)
    {
        foreach (var tab in saved.Tabs)
        {
            AddTab();
        }

        if (Tabs.Count == 0)
        {
            AddTab();
        }

        for (var i = 0; i < saved.Tabs.Count; i++)
        {
            if (saved.Tabs[i].RepositoryPath is { } path && !string.IsNullOrWhiteSpace(path))
            {
                Tabs[i].ShowPending(path);
            }
        }

        _restoring = true;
        Activate(Tabs[saved.ActiveTab >= 0 && saved.ActiveTab < Tabs.Count ? saved.ActiveTab : 0]);
        _restoring = false;
    }

    /// <summary>What the session keeps of each tab, in tab order.</summary>
    private List<TabState> SavedTabStates() => Tabs.Select(tab => new TabState(tab.SavedPath)).ToList();
}
