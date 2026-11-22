using System.Reflection;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AboutTexts;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part A's expectations against the app (docs/test-reports/phase-8a.md, "Conventions of part A"):
/// <c>OperationTexts.AboutTexts</c>' texts and the app's links are the report's word for word, no
/// text holds an e-mail address, the pinned identity is the report's, and every picture name matches
/// the phase's pattern (design 0.3), is the report's and is used once.
/// </summary>
public sealed partial class Phase8aExpectationTests
{
    [Fact]
    public void Part_A_s_texts_are_the_app_s_word_for_word()
    {
        Assert.Equal(A.Section, Texts.Section);
        Assert.Equal(A.Creator, Texts.CreatedBy(TestIdentity.Pinned.Creator));
        Assert.Equal(A.Labels, [Texts.VersionLabel, Texts.CopyrightLabel, Texts.LicenseLabel, Texts.NoticesLabel, Texts.SourceCodeLabel, Texts.UserGuideLabel]);
        Assert.Equal(A.MitLicense, Texts.MitLicense);
        Assert.Equal(A.ShowLicense, Texts.ShowLicense);
        Assert.Equal(A.NoticesNote, Texts.NoticesNote);
        Assert.Equal(A.ShowNotices, Texts.ShowNotices);
        Assert.Equal(A.RepositoryTip, Texts.RepositoryTip);
        Assert.Equal(A.OpenUserGuide, Texts.OpenUserGuide);
        Assert.Equal(A.GuideTip, Texts.GuideTip);
        Assert.Equal(A.LicenseTitle, Texts.LicenseTitle);
        Assert.Equal(A.NoticesTitle, Texts.NoticesTitle);
        Assert.Equal(A.MainNotices, Texts.MainNotices);
        Assert.Equal(A.CopyAll, Texts.CopyAll);
        Assert.Equal(A.Close, Texts.Close);
        Assert.Equal(A.PaletteCommand, Texts.PaletteCommand);
        Assert.Equal(A.MenuItem, Texts.MenuItem);
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, A.TextNotReadLine, EmbeddedTexts.License), Texts.TextNotRead(EmbeddedTexts.License));
        Assert.Equal((A.RepositoryFailedLine, A.GuideFailedLine, A.CopyFailedLine), (Texts.RepositoryFailed, Texts.GuideFailed, Texts.CopyFailed));
        Assert.Equal(A.NoticesSwitch.Skip(1), EmbeddedTexts.VendorNotices().Select(name => Texts.NoticeFileLabel(EmbeddedTexts.FileNameOf(name))));
        Assert.Equal(A.NoticesSwitchIds.Skip(1), EmbeddedTexts.VendorNotices().Select(name => AboutSettingsViewModel.PartId(EmbeddedTexts.FileNameOf(name))));

        // The ellipsis is the one character U+2026.
        Assert.All([A.ShowLicense, A.ShowNotices, A.PaletteCommand], text => Assert.EndsWith("…", text, StringComparison.Ordinal));
    }

    [Fact]
    public void Part_A_s_identity_and_addresses_are_the_app_s()
    {
        Assert.Equal((A.Name, A.Copyright, PinnedVersion), (TestIdentity.Pinned.Name, TestIdentity.Pinned.Copyright, TestIdentity.Pinned.Version.Display));
        Assert.Equal(A.RepositoryAddress, AppLinks.Repository.AbsoluteUri);
        Assert.Equal(A.GuideAddress, AppLinks.Guide(GuidePage.Start).AbsoluteUri);
        Assert.Equal(A.RepositoryLink, Texts.LinkText(AppLinks.Repository));
    }

    [Fact]
    public void No_text_of_part_A_holds_an_email_address()
    {
        var texts = typeof(A).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => (string)field.GetValue(null)!)
            .Concat(A.Labels).Concat(A.NoticesSwitch).Concat(A.NavButtons)
            .ToList();
        Assert.True(texts.Count > 40, $"Only {texts.Count} texts were read.");
        Assert.DoesNotContain(texts, text => text.Contains('@', StringComparison.Ordinal));
        Assert.DoesNotContain(
            [Texts.CreatedBy(TestIdentity.Pinned.Creator), TestIdentity.Pinned.Copyright, Texts.NoticesNote, Texts.RepositoryTip, Texts.GuideTip, Texts.MenuItem],
            text => text.Contains('@', StringComparison.Ordinal));
    }

    [Fact]
    public void Part_A_s_picture_names_are_the_report_s_unique_and_follow_the_rule()
    {
        Assert.Equal(
            ["a1a-about-dark", "a4a-license", "a1b-about-link-focus", "a2a-about-light", "a3a-about-1920", "a5a-notices-top", "a5b-notices-end", "a5c-notices-avalonia", "a6a-palette-about"],
            A.Pictures);
        Assert.Equal(["a1a-about-dark", "a3a-about-1920", "a4a-license"], A.RealWindowPictures);
        Assert.All(A.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.Equal(A.Pictures.Count, A.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(A.RealWindowPictures, name => Assert.Contains(name, A.Pictures));
    }

    [Fact]
    public void Part_A_s_places_agree_with_one_another()
    {
        // About lies exactly one button and its gap below Accounts, at 7a's places (140, 223) and (550, 383).
        Assert.Equal(A.NavHeight + A.NavGap, A.AboutBelowAccounts);
        Assert.Equal(223 + A.AboutBelowAccounts, A.AboutAt.Y);
        Assert.Equal(383 + A.AboutBelowAccounts, A.AboutAtFull.Y);
        Assert.True(Math.Abs(A.AboutAt.X - 140) <= A.Tolerance && Math.Abs(A.AboutAtFull.X - 550) <= A.Tolerance);

        // The text dialog's card is centred by its maximum in the 1100×700 window.
        Assert.Equal((DefaultSize.Width - A.DialogCard.Width) / 2, A.DialogCard.X);
        Assert.Equal((DefaultSize.Height - A.DialogCard.Height) / 2, A.DialogCard.Y);
        Assert.Equal(A.NavButtons.Count, A.NavIds.Count);
        Assert.Equal(A.NoticesSwitch.Count, A.NoticesSwitchIds.Count);
    }
}
