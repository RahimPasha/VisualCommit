using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The guide's pages (<c>docs/guide/src</c>) and the catalogue of pictures held to each other
/// (docs/phase-8a-design.md 11.3.3, 11.4; D440): every picture a page names is in the catalogue and
/// every name of the catalogue is shown; each picture is two raw <c>&lt;img&gt;</c>s, light first,
/// each with its <c>alt</c>, from <c>pictures/</c> beside the page's folder, and with
/// <c>srcset="&lt;src&gt; 2x"</c> exactly while <see cref="GuideCamera.Scale"/> is 2. They read the
/// pages alone: no app starts, and the demo's root is not needed.
/// </summary>
public sealed partial class GuideCatalogueTests
{
    private static readonly string Pages = Path.Combine(GuideSettings.RepoRoot, "docs", "guide", "src");

    [Fact]
    public void The_catalogue_holds_the_42_pictures_each_once_with_a_name_a_file_can_carry()
    {
        Assert.Equal(42, GuideShotCatalogue.Names.Count);
        Assert.Equal(GuideShotCatalogue.Names.Count, GuideShotCatalogue.Names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(GuideShotCatalogue.Names, name => Assert.Matches("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", name));
        Assert.Equal("stage-hunk-light.png", GuideShotCatalogue.FileName(GuideShotCatalogue.StageHunk, GuideTheme.Light));
        Assert.Equal("stage-hunk-dark.png", GuideShotCatalogue.FileName(GuideShotCatalogue.StageHunk, GuideTheme.Dark));
    }

    [Fact]
    public void Every_picture_a_page_names_is_in_the_catalogue()
    {
        var unknown = PictureReferences()
            .Where(reference => !GuideShotCatalogue.Names.Contains(reference.Name))
            .Select(reference => $"{reference.Page}: {reference.File}")
            .ToList();
        Assert.True(unknown.Count == 0, "Pages name pictures the catalogue does not make:\n" + string.Join('\n', unknown));
    }

    [Fact]
    public void Every_picture_of_the_catalogue_is_shown_in_both_themes()
    {
        var shown = PictureReferences().Select(reference => reference.File).ToHashSet(StringComparer.Ordinal);
        var missing = GuideShotCatalogue.Names
            .SelectMany(name => new[] { GuideShotCatalogue.FileName(name, GuideTheme.Light), GuideShotCatalogue.FileName(name, GuideTheme.Dark) })
            .Where(file => !shown.Contains(file))
            .ToList();
        Assert.True(missing.Count == 0, $"No page shows {missing.Count} of the catalogue's pictures:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void Each_picture_is_two_images_light_first_each_with_its_alt_text_and_its_srcset()
    {
        var problems = new List<string>();
        foreach (var page in PageFiles())
        {
            var images = Images(page).ToList();
            for (var i = 0; i < images.Count; i++)
            {
                var image = images[i];
                var where = $"{Relative(page)} line {image.Line}";
                if (string.IsNullOrWhiteSpace(image.Alt))
                {
                    problems.Add($"{where}: no alt text");
                }

                var expectedSrcset = GuideCamera.Scale == 2 ? image.Src + " 2x" : null;
                if (image.Srcset != expectedSrcset)
                {
                    problems.Add($"{where}: srcset \"{image.Srcset}\", expected {(expectedSrcset is null ? "none at scale 1" : $"\"{expectedSrcset}\"")}");
                }

                var light = LightPicture().Match(image.Src);
                if (light.Success)
                {
                    var dark = i + 1 < images.Count ? images[i + 1] : null;
                    var expectedDark = light.Groups["path"].Value + "-dark.png";
                    if (dark is null || dark.Src != expectedDark)
                    {
                        problems.Add($"{where}: {image.Src} is not followed by its dark image {expectedDark}");
                    }
                    else if (dark.Alt != image.Alt)
                    {
                        problems.Add($"{where}: the two images of {light.Groups["path"].Value} have different alt texts");
                    }

                    i++;
                }
                else if (DarkPicture().IsMatch(image.Src))
                {
                    problems.Add($"{where}: {image.Src} comes before, or without, its light image");
                }
            }
        }

        Assert.True(problems.Count == 0, "Pictures not written as D440 says:\n" + string.Join('\n', problems));
    }

    [Fact]
    public void Every_picture_is_a_raw_image_from_the_pictures_folder_beside_the_pages_root()
    {
        var problems = new List<string>();
        foreach (var page in PageFiles())
        {
            var text = File.ReadAllText(page);
            foreach (Match markdown in MarkdownImage().Matches(text))
            {
                problems.Add($"{Relative(page)} line {LineOf(text, markdown.Index)}: a Markdown image, which cannot carry srcset: {markdown.Value}");
            }

            var depth = Path.GetRelativePath(Pages, Path.GetDirectoryName(page)!) is "." ? 0 : Path.GetRelativePath(Pages, Path.GetDirectoryName(page)!).Split(Path.DirectorySeparatorChar).Length;
            var prefix = string.Concat(Enumerable.Repeat("../", depth)) + "pictures/";
            foreach (var image in Images(page))
            {
                if (!image.Src.StartsWith(prefix, StringComparison.Ordinal) || image.Src[prefix.Length..].Contains('/', StringComparison.Ordinal))
                {
                    problems.Add($"{Relative(page)} line {image.Line}: {image.Src} is not in {prefix}");
                }
            }
        }

        Assert.True(problems.Count == 0, "Pictures from elsewhere:\n" + string.Join('\n', problems));
    }

    /// <summary>The pages: every Markdown file under <c>docs/guide/src</c> but <c>SUMMARY.md</c>.</summary>
    private static IEnumerable<string> PageFiles()
    {
        Assert.True(Directory.Exists(Pages), $"The guide's pages are not at {Pages}.");
        return Directory.EnumerateFiles(Pages, "*.md", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).Equals("SUMMARY.md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<(string Page, string File, string Name)> PictureReferences()
    {
        foreach (var page in PageFiles())
        {
            foreach (Match reference in PictureFile().Matches(File.ReadAllText(page)))
            {
                yield return (Relative(page), reference.Groups["file"].Value, reference.Groups["name"].Value);
            }
        }
    }

    private static IEnumerable<PageImage> Images(string page)
    {
        var text = File.ReadAllText(page);
        foreach (Match tag in ImageTag().Matches(text))
        {
            var attributes = Attribute().Matches(tag.Value)
                .GroupBy(attribute => attribute.Groups["name"].Value.ToLowerInvariant(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Groups["value"].Value, StringComparer.Ordinal);
            yield return new PageImage(
                LineOf(text, tag.Index),
                attributes.GetValueOrDefault("src") ?? string.Empty,
                attributes.GetValueOrDefault("srcset"),
                attributes.GetValueOrDefault("alt"));
        }
    }

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    private static string Relative(string page) => Path.GetRelativePath(Pages, page).Replace('\\', '/');

    private sealed record PageImage(int Line, string Src, string? Srcset, string? Alt)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"line {Line}: {Src}");
    }

    [GeneratedRegex(@"(?<file>(?<name>[a-z0-9]+(?:-[a-z0-9]+)*?)-(?:light|dark)\.png)")]
    private static partial Regex PictureFile();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImageTag();

    [GeneratedRegex(@"(?<name>[A-Za-z-]+)\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"^(?<path>.*)-light\.png$")]
    private static partial Regex LightPicture();

    [GeneratedRegex(@"-dark\.png$")]
    private static partial Regex DarkPicture();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex MarkdownImage();
}
