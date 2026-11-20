using System.Reflection;
using VisualCommit.App.Services;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The app's links (<see cref="AppLinks"/>): the guide's address follows from the repository the
/// build names (<c>RepositoryUrl</c>), and every page of the guide is an https address with no
/// query string. Pure.
/// </summary>
public class AppLinksTests
{
    /// <summary>The repository the build stamps on the app (<c>RepositoryUrl</c> in Directory.Build.props).</summary>
    private static Uri BuiltRepository => new(typeof(AppLinks).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "RepositoryUrl").Value!);

    [Fact]
    public void The_repository_is_the_builds_and_the_guide_is_its_owners_pages_under_its_name()
    {
        var repository = BuiltRepository;
        var segments = repository.AbsolutePath.Trim('/').Split('/');
        Assert.Equal(2, segments.Length);
        var (owner, name) = (segments[0], segments[1]);

        Assert.Equal(repository, AppLinks.Repository);
        var pages = "https://" + owner.ToLowerInvariant() + ".github.io/" + name + "/";
        Assert.Equal(AppLinks.GuideBase, pages);
        Assert.Equal(AppLinks.GuideBase, AppLinks.Guide(GuidePage.Start).AbsoluteUri);
    }

    [Fact]
    public void Every_page_is_an_https_address_under_the_guide_with_no_query()
    {
        var pages = Enum.GetValues<GuidePage>();
        foreach (var page in pages)
        {
            var address = AppLinks.Guide(page);
            Assert.Equal(Uri.UriSchemeHttps, address.Scheme);
            Assert.Empty(address.Query);
            Assert.StartsWith(AppLinks.GuideBase, address.AbsoluteUri, StringComparison.Ordinal);
        }

        Assert.Equal(pages.Length, pages.Select(page => AppLinks.Guide(page).AbsoluteUri).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_pages_paths_and_anchors_are_the_guides()
    {
        string[] expected =
        [
            "",
            "first-run.html",
            "troubleshooting/git.html#git-missing",
            "troubleshooting/git.html#git-chosen-missing",
            "troubleshooting/git.html#git-too-old",
            "troubleshooting/git.html#git-not-working",
            "troubleshooting/git.html#helper-missing",
            "reference/shortcuts.html",
        ];
        Assert.Equal(
            expected.Select(path => AppLinks.GuideBase + path),
            Enum.GetValues<GuidePage>().Select(page => AppLinks.Guide(page).AbsoluteUri));
        Assert.Throws<ArgumentOutOfRangeException>(() => AppLinks.Guide((GuidePage)99));
    }
}
