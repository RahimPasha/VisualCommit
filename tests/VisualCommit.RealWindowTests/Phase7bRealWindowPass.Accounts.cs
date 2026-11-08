// The three accounts' real-window steps (docs/test-reports/phase-7b.md, part A, the R step in A2's
// Notes; docs/phase-7b-design.md 14.4 A, 10.7): one empty tab with the three fakes on; GitHub, GitLab
// and Azure DevOps signed in through Settings, Accounts, in D353's order, each token typed into its real
// dialog and Azure DevOps' organization typed in lower case, the screen kept open; the section turned
// to its end by the real wheel (a02d-three-signed-in-end); Esc; accounts.json with three records. No
// preparation (14.2).
using System.Globalization;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Hosting;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase7bRealWindowPass
{
    /// <summary>The three providers in D353's order, as the section's blocks stand and as the launch signs in.</summary>
    private static readonly HostingProvider[] Three = [HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps];

    /// <summary>
    /// The three accounts (14.4 A; A2's R step): the launch asks nothing (D257, D350); each sign-in
    /// makes part A's check requests alone, in order, with GitHub's token, GitLab's <c>Bearer</c> or
    /// Azure DevOps' Basic with an empty user, 200; the section at its end, UI Automation's
    /// <c>VerticalScrollPercent</c> 100 asserted before the capture (<c>a02d-three-signed-in-end</c>);
    /// Esc keeps the three (D256) and <c>accounts.json</c> holds three records, <c>userId</c> on Azure
    /// DevOps' alone, no token.
    /// </summary>
    [Fact]
    public async Task Check_A_three_accounts()
    {
        using var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, ownsCopy: true, ct: TestContext.Current.CancellationToken);
        using var app = Launch(served, repository: null);

        // Step 1 (A1's start): the three on, the launch asks none of them (D257, D350).
        AssertNoRequests(served, default, "The launch asked a fake (D257, D350)");

        // Steps 2 to 4 (A2 steps 3 to 5): each provider signed in through Settings in D353's order, the
        // screen kept open; Azure DevOps' organization typed in lower case (the block shows the server's
        // spelling); each fake's check requests alone.
        foreach (var provider in Three)
        {
            var organization = provider == HostingProvider.AzureDevOps ? PartA.OrganizationLower : null;
            var mark = Mark(served);
            app.SignInThroughSettings(provider, TokenOf(served, provider), organization, LoginOf(provider), NetworkTimeout, keepOpen: true);
            AssertSignInRequests(served, provider, mark, organization);
        }

        // Step 5 (A2 step 7): the section's end, its largest offset, by the real wheel; asserted before the capture.
        var signedIn = Mark(served);
        app.ScrollAccountsToEnd(StepTimeout);
        var (scrollable, percent) = app.SettingsSectionScroll();
        Assert.True(
            scrollable && percent >= RealApp.AtEndPercent,
            string.Create(CultureInfo.InvariantCulture, $"The Accounts section is not at its end before the capture: {(scrollable ? "it scrolls" : "it does not scroll")}, its VerticalScrollPercent {percent:0.##}."));
        foreach (var provider in Three)
        {
            app.WaitForAccountLogin(provider, LoginOf(provider), StepTimeout);
        }

        Compare(app, served, PartA.A02ThreeEnd);
        AssertNoRequests(served, signedIn, "Turning the section to its end asked a fake");

        // Step 6 (A2 step 8): Esc closes the screen; the three stay (D256); accounts.json names them,
        // userId on Azure DevOps' alone, and holds no token.
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitUntilGone(RealApp.SettingsDialogId, StepTimeout);
        var expected = ExpectedAccounts(served);
        WaitTelling(
            app,
            () => ReadSavedAccounts(app.DataDirectory, served) is { Version: 1, HoldsAToken: false } saved
                && saved.Accounts.OrderBy(account => account.Provider, StringComparer.Ordinal).SequenceEqual(expected),
            "accounts.json to hold the three accounts, userId on Azure DevOps' alone",
            () => $"It holds: {ReadSavedAccounts(app.DataDirectory, served) switch { null => "nothing readable", { HoldsAToken: true } => "a token (its fields are not printed)", var saved => $"version {saved.Version}: {string.Join(" | ", saved.Accounts.Select(Describe))}" }}.");
        Assert.False(ReadSavedAccounts(app.DataDirectory, served)!.HoldsAToken, "accounts.json holds a token.");
        AssertNoRequests(served, signedIn, "Closing the screen asked a fake");
        AssertCleanEnd(app, served);
    }

    /// <summary>
    /// The three records <c>accounts.json</c> holds after the launch (design 1.8, 9.3; the twin's
    /// <c>AssertRecord</c>), ordered by provider: Azure DevOps' with Ada's <c>userId</c>, GitHub's and
    /// GitLab's with none; each on its fake's host, <c>useForGit</c> true, <c>tokenState</c>
    /// <c>keychain</c> (the memory store, available).
    /// </summary>
    private static IReadOnlyList<SavedAccount> ExpectedAccounts(ServedHosting7b served) =>
    [
        new("azure-devops", PartA.Host(served.AzureDevOps.Port), PartA.Organization, true, AdaAzureId(served), true, "keychain"),
        new("github", PartA.Host(served.GitHub.Port), OctoLogin, false, null, true, "keychain"),
        new("gitlab", PartA.Host(served.GitLab.Port), AdaLogin, false, null, true, "keychain"),
    ];
}
