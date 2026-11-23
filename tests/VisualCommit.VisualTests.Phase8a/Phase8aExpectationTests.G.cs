using System.Globalization;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;
using AboutTexts = VisualCommit.App.ViewModels.Operations.OperationTexts.AboutTexts;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part G's expectations against the app (docs/test-reports/phase-8a.md, "Conventions of part G"):
/// the command's and the links' texts are the app's word for word, each address is the app's
/// address of its page, the shortcuts list's last group and its counts are the app's key table's,
/// the fakes' values are part F's, and every picture name matches the phase's pattern (design 0.3),
/// is the report's and is used once.
/// </summary>
public sealed partial class Phase8aExpectationTests
{
    [Fact]
    public void Part_G_s_texts_are_the_app_s_word_for_word()
    {
        Assert.Equal(G.OpenUserGuide, AppCommands.OpenUserGuideText);
        Assert.Equal(G.HowVisualCommitFindsGit, GitSetupTexts.HowVisualCommitFindsGit);
        Assert.Equal(G.WhatTheHelperDoes, GitSetupTexts.WhatTheHelperDoes);
        Assert.Equal(G.OtherWaysToInstallGit, GitSetupTexts.OtherWaysToInstallGit);
        Assert.Equal(G.AboutGuideLink, AboutTexts.OpenUserGuide);
        Assert.Equal(G.AboutGuideTip, AboutTexts.GuideTip);
        Assert.Equal(G.UserGuideLabel, AboutTexts.UserGuideLabel);
        Assert.Equal(G.AboutGuideFailedLine, AboutTexts.GuideFailed);
        Assert.Equal(G.NoLauncherLine, AppCommands.OpenUserGuideWithoutLauncher);
        Assert.Equal(G.FailedLine, G.OpenUserGuide + " failed.");
        Assert.Equal(G.Key, new KeyTexts(CommandKey.Ctrl).Of(AppCommand.OpenUserGuide));
        Assert.Equal(G.Key, new KeyTexts(CommandKey.Cmd).Of(AppCommand.OpenUserGuide));
        Assert.Contains(G.Query, G.OpenUserGuide, StringComparison.Ordinal);
        Assert.Equal(G.OpenUserGuide, A.OpenUserGuide);
        Assert.Equal((G.UserGuideLabel, G.AboutGuideTip, G.AboutGuideFailedLine), (A.UserGuideLabel, A.GuideTip, A.GuideFailedLine));
    }

    [Fact]
    public void Part_G_s_addresses_are_the_app_s_pages()
    {
        Assert.Equal(G.StartPage, AppLinks.GuideBase);
        Assert.Equal(G.StartPage, AppLinks.Guide(GuidePage.Start).AbsoluteUri);
        Assert.Equal(G.FirstRunAddress, AppLinks.Guide(GuidePage.FirstRun).AbsoluteUri);
        Assert.Equal(G.GitMissingAddress, AppLinks.Guide(GuidePage.GitMissing).AbsoluteUri);
        Assert.Equal(G.GitChosenMissingAddress, AppLinks.Guide(GuidePage.GitChosenMissing).AbsoluteUri);
        Assert.Equal(G.GitTooOldAddress, AppLinks.Guide(GuidePage.GitTooOld).AbsoluteUri);
        Assert.Equal(G.GitNotWorkingAddress, AppLinks.Guide(GuidePage.GitNotWorking).AbsoluteUri);
        Assert.Equal(G.HelperMissingAddress, AppLinks.Guide(GuidePage.HelperMissing).AbsoluteUri);
        Assert.Equal(G.ShortcutsAddress, AppLinks.Guide(GuidePage.Shortcuts).AbsoluteUri);
        Assert.Equal(A.GuideAddress, G.StartPage);

        // No address has a query string (1.7); method 2's eight alternate a state's page and the start page.
        Assert.All([G.StartPage, G.FirstRunAddress, G.GitMissingAddress, G.GitChosenMissingAddress, G.GitTooOldAddress, G.GitNotWorkingAddress, G.HelperMissingAddress, G.ShortcutsAddress], address => Assert.DoesNotContain('?', address));
        Assert.Equal(8, G.Method2Requests.Count);
        Assert.All(G.Method2Requests.Where((_, index) => index % 2 == 1), address => Assert.Equal(G.StartPage, address));
        Assert.Equal([G.HelperMissingAddress, G.StartPage], G.Method3Requests);
    }

    [Fact]
    public void Part_G_s_help_group_and_counts_are_the_app_s_key_table()
    {
        var windows = AppShortcuts.Groups(CommandKey.Ctrl);
        Assert.Equal(G.ShortcutGroups, windows.Count);
        Assert.Equal(G.ShortcutRows, windows.Sum(group => group.Rows.Count));
        var help = windows[^1];
        Assert.Equal(G.HelpGroup, help.Title);
        Assert.Equal(G.GroupBeforeHelp, windows[^2].Title);
        var row = Assert.Single(help.Rows);
        Assert.Equal((G.OpenUserGuide, G.Key), (row.Action, row.Keys));
        Assert.Equal(G.HelpRowName, $"{row.Action}: {row.Keys}");
        Assert.Null(help.Note);
        Assert.Equal(G.RepositoryNote, Assert.Single(windows, group => group.Note is not null).Note);

        var mac = AppShortcuts.Groups(CommandKey.Cmd)[^1];
        Assert.Equal((G.HelpGroup, G.MacNote), (mac.Title, mac.Note));
        Assert.Equal(G.MacNote, AppShortcuts.HelpNoteOnMac);
    }

    [Fact]
    public void Part_G_s_fakes_and_screens_are_part_F_s()
    {
        Assert.Equal(G.TooOldVersion, F.TooOldVersion);
        Assert.Equal((G.BrokenConfigFile, G.TooOldVersion), (FakeGitSetupProbe.DemoGitConfig, FakeGitSetupProbe.TooOldVersion));
        Assert.Equal(G.MissingTitle, GitSetupTexts.MissingTitle);
        Assert.Equal(G.ChosenMissingTitle, GitSetupTexts.ChosenMissingTitle);
        Assert.Equal(G.TooOldTitle, string.Format(CultureInfo.InvariantCulture, GitSetupTexts.TooOldTitleFormat, G.TooOldVersion));
        Assert.Equal(G.NotWorkingTitle, GitSetupTexts.NotWorkingTitle);
        Assert.Equal(G.HelperMissingTitle, GitSetupTexts.HelperMissingTitle);
        Assert.Equal(G.MissingGuidance, F.MissingGuidanceWindows);
        Assert.Equal(G.ChosenMissingGuidance, string.Format(CultureInfo.InvariantCulture, F.ChosenMissingGuidanceFormat, F.WindowsGit));
        Assert.Equal(G.TooOldGuidance, F.TooOldGuidanceWindows);
        Assert.Equal(G.NotWorkingGuidance, string.Format(CultureInfo.InvariantCulture, F.ConfigFaultGuidanceFormat, G.BrokenConfigLine, G.BrokenConfigFile));
        Assert.Equal(G.HelperMissingGuidance, F.HelperMissingGuidance);
        Assert.Equal(G.HelperMissingStatusFormat, F.HelperMissingStatusFormat);
        Assert.Equal(G.MostF6Presses, F.MostF6Presses);
        Assert.Equal((G.ChooseGitExe, G.CheckAgain), (F.ChooseGitExe, F.CheckAgain));

        // The buttons are part F's, in 5.3's order, each with its text; the guide link comes after the last.
        foreach (var (ids, texts) in new[]
        {
            (G.MissingButtons, G.MissingButtonTexts), (G.ChosenMissingButtons, G.ChosenMissingButtonTexts), (G.TooOldButtons, G.TooOldButtonTexts),
            (G.NotWorkingButtons, G.NotWorkingButtonTexts), (G.HelperMissingButtons, G.HelperMissingButtonTexts),
        })
        {
            Assert.Equal(ids.Count, texts.Count);
            Assert.Equal(ids, F.ButtonOrder.Where(ids.Contains));
        }

        Assert.Equal([F.InstallWithWinget, F.DownloadGitForWindows, F.ChooseGitExe, F.CheckAgain], G.MissingButtonTexts);
        Assert.Equal([F.ChooseGitExe, F.UseTheGitItFound, F.CheckAgain], G.ChosenMissingButtonTexts);
        Assert.Equal([F.UpdateWithWinget, F.DownloadGitForWindows, F.ChooseGitExe, F.CheckAgain], G.TooOldButtonTexts);
        Assert.Equal([F.DownloadGitForWindows, F.ShowTheFile, F.ChooseGitExe, F.CheckAgain], G.NotWorkingButtonTexts);
        Assert.Equal([F.CheckAgain, F.ContinueWithoutIt], G.HelperMissingButtonTexts);
        Assert.Equal(G.MissingButtons, F.FocusOrderWithWinget);
        Assert.Equal((G.GitSetupGuideLinkId, G.GitSetupMoreLinkId), (F.GuideLinkId, F.MoreLinkId));
    }

    [Fact]
    public void Part_G_s_routes_and_places_are_the_earlier_checks()
    {
        // Phase 6's K10 step 1's click point on "ops", and part A's A3 route and places.
        Assert.Equal((484d, 207d), (G.MainWorkPoint.X, G.MainWorkPoint.Y));
        Assert.Equal(3, G.MainWorkRow);
        Assert.Equal(A.NavIds.Skip(1), G.DownsToAbout);
        Assert.Equal(A.TabOrder, G.TabsToGuideLink);
        Assert.Equal((A.SectionX, A.ValuesX, A.Tolerance), (G.LabelX, G.ValueX, G.Tolerance));
        Assert.Equal((A.GuideLinkId, A.RepositoryLinkId, A.SectionId, A.SectionScrollerId), (G.AboutGuideLinkId, G.AboutRepositoryLinkId, G.AboutSectionId, G.SectionScrollerId));
        Assert.Equal((A.Dark.Accent, A.Dark.Panel, A.Dark.Secondary, A.Dark.Primary, A.Dark.Selection), (G.Accent, G.Panel, G.Secondary, G.Primary, G.Selection));
    }

    [Fact]
    public void Part_G_s_picture_names_are_the_report_s_unique_and_follow_the_rule()
    {
        Assert.Equal(["g1a-palette-guide", "g1b-shortcuts-help", "g2a-first-run-guide-link", "g3a-about-guide-link"], G.Pictures);
        Assert.All(G.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.Equal(G.Pictures.Count, G.Pictures.Distinct(StringComparer.Ordinal).Count());
    }
}
