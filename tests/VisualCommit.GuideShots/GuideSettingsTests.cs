using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// Which folder may be the demo's root (docs/phase-8a-design.md 1.14, 11.3.5): absolute, outside the
/// temp folder and the repository, and empty or marked by this project. Made-up folders stand for
/// the repository and the temp folder; the folders looked into are the test's own.
/// </summary>
public sealed class GuideSettingsTests
{
    private const string Repository = "/srv/checkouts/vc";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("demo")]
    [InlineData("./demo")]
    public void A_root_that_is_not_set_or_not_absolute_is_refused(string? root)
    {
        using var temp = new TempDirectory("guide-settings");
        Assert.Equal(GuideSettings.RootRefusal, GuideSettings.WhyNotRoot(root, Repository, temp.Path));
    }

    [Fact]
    public void A_root_in_the_temp_folder_or_the_repository_or_holding_either_is_refused()
    {
        using var temp = new TempDirectory("guide-settings");
        var root = temp.Combine("demo");
        Assert.Equal(GuideSettings.RootRefusal, GuideSettings.WhyNotRoot(root, Repository, temp.Path));
        Assert.Equal(GuideSettings.RootRefusal, GuideSettings.WhyNotRoot(temp.Path, Repository, temp.Combine("tmp")));
        Assert.Equal(GuideSettings.RootRefusal, GuideSettings.WhyNotRoot(Path.Combine(temp.Path, "repo", "demo"), Path.Combine(temp.Path, "repo"), "/no/temp"));
        Assert.Equal(GuideSettings.RootRefusal, GuideSettings.WhyNotRoot(temp.Path, temp.Combine("repo"), "/no/temp"));
    }

    [Fact]
    public void A_missing_or_empty_folder_or_one_holding_the_marker_may_be_the_root_and_another_may_not()
    {
        using var temp = new TempDirectory("guide-settings");
        var root = temp.Combine("demo");
        Assert.Null(GuideSettings.WhyNotRoot(root, Repository, "/no/temp"));
        Directory.CreateDirectory(root);
        Assert.Null(GuideSettings.WhyNotRoot(root, Repository, "/no/temp"));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "someone's file");
        Assert.Equal(GuideSettings.RootRefusal, GuideSettings.WhyNotRoot(root, Repository, "/no/temp"));
        File.WriteAllText(Path.Combine(root, GuideSettings.Marker), "used before");
        Assert.Null(GuideSettings.WhyNotRoot(root, Repository, "/no/temp"));
    }

    [Fact]
    public void A_file_is_no_root()
    {
        using var temp = new TempDirectory("guide-settings");
        var file = temp.Combine("demo");
        File.WriteAllText(file, "a file");
        Assert.Equal(GuideSettings.RootRefusal, GuideSettings.WhyNotRoot(file, Repository, "/no/temp"));
    }

    [Theory]
    [InlineData("/home/demo/Projects", "/home/demo", true)]
    [InlineData("/home/demo", "/home/demo/", true)]
    [InlineData("/home/demonstration", "/home/demo", false)]
    [InlineData("/home", "/home/demo", false)]
    public void Within_compares_whole_folder_names(string path, string folder, bool within)
    {
        Assert.Equal(within, GuideSettings.IsWithin(path, folder));
    }
}
