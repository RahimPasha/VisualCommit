using FlaUI.Core.AutomationElements;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// Phase 4's shared additions to the real-window driver (docs/phase-4-design.md 13.2, package
/// WP-S): the in-progress banner's Continue and Skip (design 5.2), found by their automation ids
/// (7.4) and clicked with the real mouse. The interactive rebase's list and the conflict resolver
/// have parts of their own (RealApp.Phase4.Rebase.cs, RealApp.Phase4.Conflicts.cs). The caret of
/// the resolver's result does not blink in the app this driver starts (<see cref="Launch(string, IReadOnlyDictionary{string, string?}?)"/>
/// sets <c>VISUALCOMMIT_CARET_BLINK=0</c>).
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The banner's Continue button's automation id.</summary>
    public const string BannerContinueId = "BannerContinueButton";

    /// <summary>The banner's Skip button's automation id.</summary>
    public const string BannerSkipId = "BannerSkipButton";

    /// <summary>The banner's Continue button, as UI Automation finds it.</summary>
    public AutomationElement BannerContinueButton() => Find(BannerContinueId);

    /// <summary>The banner's Skip button, as UI Automation finds it.</summary>
    public AutomationElement BannerSkipButton() => Find(BannerSkipId);

    /// <summary>
    /// Clicks the banner's Continue with the real mouse. Throws when it is off screen or disabled:
    /// a click on it then does nothing, and a check that clicks it expects it to act.
    /// </summary>
    public void ClickBannerContinue() => ClickBannerButton(BannerContinueButton(), "Continue");

    /// <summary>Clicks the banner's Skip with the real mouse (the app asks first, design 7.2). Throws when it is off screen or disabled.</summary>
    public void ClickBannerSkip() => ClickBannerButton(BannerSkipButton(), "Skip");

    private void ClickBannerButton(AutomationElement button, string what)
    {
        if (button.IsOffscreen || !button.IsEnabled)
        {
            throw new InvalidOperationException($"The banner's {what} cannot be clicked: off screen {button.IsOffscreen}, enabled {button.IsEnabled}.");
        }

        ClickWithMouse(button);
    }
}
