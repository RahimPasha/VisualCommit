using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The single-instance notice's status as a screen reader is told of it (D421, D435; design 4.3.4,
/// 9.3.9), no picture: part I's checks take those. The notice through <see cref="InstanceNoticeHost"/>,
/// its peers as a client reaches them (<see cref="ClientWalk"/>).
/// </summary>
public sealed class InstanceNoticeLiveRegionTests
{
    /// <summary>
    /// The status is a polite live region, hidden until a try goes unanswered, and a client reaches
    /// only what is shown: a walk down from the notice as it opens reaches no live region, and the
    /// status's words come as it shows, so that no client has the status when it says them. A client
    /// that reads the notice after that try reaches the status, polite, named "It still did not
    /// answer."; a second unanswered try changes nothing there and raises no Name change, while the
    /// focus comes back to Try again.
    /// </summary>
    [AvaloniaFact]
    public async Task The_status_is_a_polite_live_region_that_a_client_reaches_only_once_a_try_went_unanswered()
    {
        using var host = InstanceNoticeHost.Show(GuidancePlatform.Windows, I.WindowsFolder, ThemeVariant.Dark);
        var tryAgain = host.Find<Button>(I.TryAgainId);
        var status = host.Find<TextBlock>(I.StatusId);
        await host.WaitForAsync(() => ReferenceEquals(host.Focused, tryAgain), "Try again to take the keyboard focus");
        var opened = ClientWalk.Reach(host.Window);
        Assert.NotNull(ClientWalk.Find(opened, I.TryAgainId));
        Assert.Null(ClientWalk.Find(opened, I.StatusId));
        Assert.Empty(ClientWalk.LiveRegions(opened));

        await UnansweredTryAsync(host, status, tryAgain);

        Assert.Equal(1, host.Try.Asked);
        Assert.Equal(I.StillNoAnswer, status.Text);
        var reached = ClientWalk.Reach(host.Window);
        Assert.Equal([I.StatusId], ClientWalk.LiveRegions(reached));
        var peer = ClientWalk.Find(reached, I.StatusId)!;
        Assert.Equal(AutomationLiveSetting.Polite, peer.GetLiveSetting());
        Assert.Equal(I.StillNoAnswer, peer.GetName());
        using var names = ShellDriver.RecordNameChanges(peer);

        await UnansweredTryAsync(host, status, tryAgain);

        Assert.Equal(2, host.Try.Asked);
        Assert.Equal(I.StillNoAnswer, peer.GetName());
        Assert.Equal(0, names.Count);
    }

    /// <summary>Enter on Try again, its try held and answered "no answer"; then the status shows and Try again has the focus again.</summary>
    private static async Task UnansweredTryAsync(InstanceNoticeHost host, TextBlock status, Button tryAgain)
    {
        host.PressKey(Key.Enter);
        await host.WaitForAsync(() => host.Try.IsHeld, "the try to be held");
        host.Try.Answer(NoticeRetry.NoAnswer);
        await host.WaitForAsync(() => status.IsEffectivelyVisible && tryAgain.IsEffectivelyEnabled && ReferenceEquals(host.Focused, tryAgain), "the status to show and Try again to have the focus again");
        InstanceNoticeHost.Settle();
    }
}
