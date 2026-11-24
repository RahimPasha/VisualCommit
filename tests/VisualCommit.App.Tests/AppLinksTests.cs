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
                var ids = HeadingIdCount(File.ReadAllText(file), anchor);
                if (ids != 1)
                {
                    problems.Add($"{page}: {markdown} holds {{ #{anchor} }} {ids} times, not once");
                }
            }
        }

        Assert.True(problems.Count == 0, "Pages and anchors the app links to that the guide lacks:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// Only a heading gives an id: <c>{ #anchor }</c> in a paragraph, a list, a table, a code block or
    /// an HTML comment is text to mdBook, so the app's link would land on no section (made-up pages).
    /// </summary>
    [Fact]
    public void An_anchor_counts_only_at_the_end_of_a_heading_outside_code()
    {
        const string Id = "{ #git-missing }";
        Assert.Equal(1, HeadingIdCount("# Troubleshooting\n\n## Git is missing " + Id + "\n\nText.\n", "git-missing"));
        Assert.Equal(1, HeadingIdCount("### Git is missing " + Id + "  \r\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("Write " + Id + " after a heading's text.\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("- a list's item " + Id + "\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("| Id | " + Id + " |\n|---|---|\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("<!-- ## Git is missing " + Id + " -->\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("```markdown\n## Git is missing " + Id + "\n```\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("~~~\n## Git is missing " + Id + "\n~~~\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("    ## Git is missing " + Id + "\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("## Git is missing " + Id + " and more\n", "git-missing"));
        Assert.Equal(0, HeadingIdCount("## Git is missing { #git-missing-too }\n", "git-missing"));
        Assert.Equal(2, HeadingIdCount("## One " + Id + "\n\n```\nnot this " + Id + "\n```\n\n## Two " + Id + "\n", "git-missing"));
    }

    /// <summary>
    /// How many headings of <paramref name="markdown"/> end in mdBook's id <c>{ #anchor }</c>: ATX
    /// headings (one to six <c>#</c> at the line's start, at most three spaces in), outside fenced
    /// code and HTML comments.
    /// </summary>
    private static int HeadingIdCount(string markdown, string anchor)
    {
        var heading = new Regex(@"^ {0,3}#{1,6}[ \t].*\{ #" + Regex.Escape(anchor) + @" \}[ \t]*$");
        var count = 0;
        string? fence = null;
        var inComment = false;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (inComment)
            {
                inComment = !line.Contains("-->", StringComparison.Ordinal);
                continue;
            }

            var trimmed = line.TrimStart(' ');
            if (fence is not null)
            {
                if (trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.Trim().All(c => c == fence[0]))
                {
                    fence = null;
                }

                continue;
            }

            if (line.Length - trimmed.Length <= 3 && Fence().Match(trimmed) is { Success: true } opening)
            {
                fence = opening.Value;
                continue;
            }

            if (trimmed.StartsWith("<!--", StringComparison.Ordinal))
            {
                inComment = !line.Contains("-->", StringComparison.Ordinal);
                continue;
            }

            if (heading.IsMatch(line))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The path of a file of the repository, from the root the build names.</summary>
    private static string RepositoryPath(string relative) =>
        Path.GetFullPath(Path.Combine(VisualRun.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>A link of SUMMARY.md: <c>[Title](path.md)</c>.</summary>
    [GeneratedRegex(@"\]\((?<path>[^)#\s]+\.md)\)")]
    private static partial Regex SummaryLink();

    /// <summary>A fenced code block's opening: three or more backticks or tildes.</summary>
    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex Fence();
}
