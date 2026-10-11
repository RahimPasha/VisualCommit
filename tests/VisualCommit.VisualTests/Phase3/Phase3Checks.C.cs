// Part C of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part C: branches,
// checkout with local changes, stashes, tags and the in-progress banners"): checks 13, 13b, 14,
// 14b, 15, 15b, 16 and 25. This class is part C's alone; its expected texts (T1 to T7, the
// scenarios' graphs) go into Phase3Expectations.C.cs (Phase3Expectations.PartC), and further files
// of its own may hold more of the class (Phase3Checks.C.<topic>.cs). Everything the checks share is
// in Phase3Checks.Support.cs; read its header for the conventions (one test method per app start,
// pictures through Shot, no fixed waits). "Select all and type X" is ShellDriver.ReplaceText; the
// error dialogs are asserted with AssertErrorDialogAsync (git's texts word for word on Git 2.36
// only).
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part C: branches, checkout with local changes, stashes, tags and the in-progress banners.</summary>
public sealed partial class Phase3ChecksC : Phase3Checks
{
    /// <summary>
    /// Check 14, step 1 (the skeleton's example; part C's writer goes on from here): the blocked
    /// checkout scenario as it opens: graph G14a with its working-changes row, nothing selected,
    /// the branch <c>main</c>, "Ready", Local branches 4 and Stashes 0.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_14_checkout_blocked()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        Shot(app, "14a-blocked-scenario");

        Assert.Equal("blocked", app.Session.Shell.ActiveTab.Title);
        var graph = Graph(app);
        Assert.True(graph.ShowsWorkingRow);
        Assert.Equal(PartC.G14aSubjects, Enumerable.Range(0, graph.Data!.Count).Select(row => graph.Data.CommitAt(row).Subject));
        Assert.Equal(-1, graph.SelectedIndex);
        Assert.False(graph.IsWorkingRowSelected);
        AssertBranch(app, "main");
        AssertStatus(app, "Ready");
        Assert.Equal(4, ((RefSectionRow)app.RefRowModel("Local branches")).Count);
        Assert.Equal(0, ((RefSectionRow)app.RefRowModel("Stashes")).Count);
        Assert.True(((RefItemRow)app.RefRowModel("Local branches", "main")).IsHead);
        AssertNoClippedText(app);
    }
}
