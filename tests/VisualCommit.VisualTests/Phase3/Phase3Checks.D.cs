// Part D of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part D: remote
// operations, credentials and prompts, remotes, the light theme and closing the window"): checks
// 17, 17b, 18, 18b, 19, 20, 21, 21b, 22, 27, 28, 29, 30 and 31. This class is part D's alone; its
// expected texts go into Phase3Expectations.D.cs (Phase3Expectations.PartD), and further files of
// its own may hold more of the class (Phase3Checks.D.<topic>.cs). Everything the checks share is in
// Phase3Checks.Support.cs; read its header for the conventions. The server scenarios are
// AuthAtServerAsync and SyncAtServerAsync (port 47391, or ServerScenario.Port and WithPort when it
// was taken). Only this class starts servers, and xunit runs its tests one at a time, so they never
// compete for the port. "Wait until idle" is ShellDriver.WaitForIdleAsync; "wait until the status
// bar shows a percentage" is ShellDriver.WaitForProgressPercentAsync.
using Avalonia.Headless.XUnit;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part D: remote operations, credentials and prompts, remotes, the light theme and closing the window.</summary>
public sealed partial class Phase3ChecksD : Phase3Checks
{
    /// <summary>
    /// Check 17, step 1 (the skeleton's example; part D's writer goes on from here): the auth
    /// scenario at the server without a sign-in, as it opens: "Ready", no progress, the toolbar's
    /// enabled and disabled buttons, and no request at the server yet.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_17_fetch_progress_and_cancel()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, scenario.Repo);

        Shot(app, "17a-before-fetch");

        Assert.Equal(["auth"], app.Session.Shell.Tabs.Select(tab => tab.Title));
        Assert.Equal(-1, Graph(app).SelectedIndex);
        AssertBranch(app, "main");
        AssertStatus(app, "Ready");
        Assert.False(app.FindByAutomationId("OperationProgress").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("OperationWaiting").IsEffectivelyVisible);
        foreach (var enabled in PartD.EnabledOnAuth)
        {
            Assert.True(ToolbarButton(app, enabled).IsEffectivelyEnabled, $"{enabled} is disabled.");
        }

        Assert.False(ToolbarButton(app, "StashButton").IsEffectivelyEnabled);
        AssertTip(ToolbarButton(app, "StashButton"), "There are no changes to stash");
        Assert.False(ToolbarButton(app, "PopButton").IsEffectivelyEnabled);
        AssertTip(ToolbarButton(app, "PopButton"), "There are no stashes");
        Assert.Empty(scenario.Server.Requests);
        AssertNoClippedText(app);
    }
}
