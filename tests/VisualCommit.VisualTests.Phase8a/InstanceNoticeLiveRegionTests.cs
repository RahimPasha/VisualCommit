using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The single-instance notice's status as a screen reader is told of it (D421, D435; design 4.3.4,
/// 9.3.9), no picture: part I's checks take those. The notice through <see cref="InstanceNoticeHost"/>.
/// </summary>
public sealed class InstanceNoticeLiveRegionTests
{
    /// <summary>
    /// The status is a polite live region whose text comes with the first unanswered try: one Name
    /// change, to "It still did not answer.", which Windows speaks. A second unanswered try says
    /// nothing new and raises none. Before, the status was a fixed text that only turned visible,
    /// with no live setting, so no try's end was spoken.
    /// </summary>
    [AvaloniaFact]
    public async Task The_status_is_a_polite_live_region_that_speaks_when_a_try_goes_unanswered()
    {
        using var host = InstanceNoticeHost.Show(GuidancePlatform.Windows, I.WindowsFolder, ThemeVariant.Dark);
        var tryAgain = host.Find<Button>(I.TryAgainId);
        var status = host.Find<TextBlock>(I.StatusId);
        await host.WaitForAsync(() => ReferenceEquals(host.Focused, tryAgain), "Try again to take the keyboard focus");
        var peer = ControlAutomationPeer.CreatePeerForElement(status);
        var names = new List<object?>();
        peer.PropertyChanged += (_, change) =>
        {
            if (change.Property == AutomationElementIdentifiers.NameProperty)
            {
                names.Add(change.NewValue);
            }
        };

        Assert.Equal(AutomationLiveSetting.Polite, peer.GetLiveSetting());
        Assert.False(status.IsEffectivelyVisible);

        foreach (var tries in new[] { 1, 2 })
        {
            host.PressKey(Key.Enter);
            await host.WaitForAsync(() => host.Try.IsHeld, "the try to be held");
            host.Try.Answer(NoticeRetry.NoAnswer);
            await host.WaitForAsync(() => status.IsEffectivelyVisible && tryAgain.IsEffectivelyEnabled && ReferenceEquals(host.Focused, tryAgain), "the status to show and Try again to have the focus again");
            InstanceNoticeHost.Settle();

            Assert.Equal(tries, host.Try.Asked);
            Assert.Equal(I.StillNoAnswer, status.Text);
            Assert.Equal(I.StillNoAnswer, peer.GetName());
            Assert.Equal([I.StillNoAnswer], names);
        }
    }
}
