using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part I's expectations against the app (docs/test-reports/phase-8a.md, "Conventions of part I"):
/// <c>InstanceNoticeTexts</c>' texts are the report's word for word, and every picture name matches
/// the phase's pattern (design 0.3) and is the report's.
/// </summary>
public sealed partial class Phase8aExpectationTests
{
    [Fact]
    public void Part_I_s_texts_are_the_app_s_word_for_word()
    {
        Assert.Equal(I.Title, InstanceNoticeTexts.Title);
        Assert.Equal(I.Heading, InstanceNoticeTexts.Heading);
        Assert.Equal(I.Body, InstanceNoticeTexts.Body);
        Assert.Equal(I.IfNoWindowOnWindows, InstanceNoticeTexts.IfNoWindowOnWindows);
        Assert.Equal(I.IfNoWindowOnMacOS, InstanceNoticeTexts.IfNoWindowOnMacOS);
        Assert.Equal(I.IfNoWindowOnLinux, InstanceNoticeTexts.IfNoWindowOnLinux);
        Assert.Equal(I.TryAgain, InstanceNoticeTexts.TryAgain);
        Assert.Equal(I.Trying, InstanceNoticeTexts.Trying);
        Assert.Equal(I.Close, InstanceNoticeTexts.Close);
        Assert.Equal(I.StillNoAnswer, InstanceNoticeTexts.StillNoAnswer);
        foreach (var platform in Enum.GetValues<GuidancePlatform>())
        {
            Assert.Equal(I.IfNoWindow(platform), InstanceNoticeTexts.IfNoWindow(platform));
        }

        // The ellipsis is the one character U+2026.
        Assert.EndsWith("\u2026", I.Trying, StringComparison.Ordinal);
    }

    [Fact]
    public void Part_I_s_picture_names_are_the_report_s_unique_and_follow_the_rule()
    {
        Assert.Equal(["i2a-notice-dark", "i2b-notice-trying", "i2c-notice-no-answer", "i3a-notice-light"], I.Pictures);
        Assert.Equal(["i2a-notice-dark", "i2b-notice-trying", "i2c-notice-no-answer"], I.RealWindowPictures);
        Assert.All(I.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.Equal(I.Pictures.Count, I.Pictures.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Part_I_s_folders_are_made_up()
    {
        Assert.Equal(@"C:\Users\demo\AppData\Roaming\VisualCommit", I.WindowsFolder);
        Assert.Equal("/home/demo/.config/VisualCommit", I.LinuxFolder);
    }
}
