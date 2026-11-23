using System.Reflection;
using System.Text.RegularExpressions;
using VisualCommit.App.Services;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The app's links (<see cref="AppLinks"/>): the guide's address follows from the repository the
/// build names (<c>RepositoryUrl</c>), every page of the guide is an https address with no query
/// string, and every page and anchor the app links to stands in the guide's source. Pure, but the
/// last fact, which reads the guide's pages; the guide's workflow runs this class (D451).
/// </summary>
public partial class AppLinksTests
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

    /// <summary>
    /// Every page the app links to is a page of the guide's source (docs/guide/src): its Markdown
    /// file exists, the table of contents (SUMMARY.md) links it, so mdBook builds it, and an anchor
    /// stands on the page once as mdBook's heading id <c>{ #anchor }</c>. A page renamed or an
    /// anchor dropped would send the app's links to "not found".
    /// </summary>
    [Fact]
    public void Every_page_and_anchor_the_app_links_to_is_in_the_guides_source()
    {
        var source = RepositoryPath("docs/guide/src");
        Assert.True(Directory.Exists(source), $"The guide's source is not at {source}.");
        var summary = File.ReadAllText(Path.Combine(source, "SUMMARY.md"));
        var linked = SummaryLink().Matches(summary).Select(link => link.Groups["path"].Value).ToHashSet(StringComparer.Ordinal);

        var problems = new List<string>();
        foreach (var page in Enum.GetValues<GuidePage>())
        {
            var address = AppLinks.Guide(page).AbsoluteUri[AppLinks.GuideBase.Length..];
            var parts = address.Split('#');
            var (path, anchor) = parts.Length == 1 ? (parts[0], null) : (parts[0], parts[1]);
            var markdown = path.Length == 0 ? "index.md" : Path.ChangeExtension(path, ".md");
            var file = Path.Combine(source, markdown.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                problems.Add($"{page}: no page {markdown}");
                continue;
            }

            if (!linked.Contains(markdown))
            {
                problems.Add($"{page}: SUMMARY.md does not link {markdown}");
            }

            if (anchor is not null)
            {
                var ids = Regex.Matches(File.ReadAllText(file), @"\{ #" + Regex.Escape(anchor) + @" \}").Count;
                if (ids != 1)
                {
                    problems.Add($"{page}: {markdown} holds {{ #{anchor} }} {ids} times, not once");
                }
            }
        }

        Assert.True(problems.Count == 0, "Pages and anchors the app links to that the guide lacks:\n" + string.Join('\n', problems));
    }

    /// <summary>The path of a file of the repository, from the root the build names.</summary>
    private static string RepositoryPath(string relative) =>
        Path.GetFullPath(Path.Combine(VisualRun.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>A link of SUMMARY.md: <c>[Title](path.md)</c>.</summary>
    [GeneratedRegex(@"\]\((?<path>[^)#\s]+\.md)\)")]
    private static partial Regex SummaryLink();
}
