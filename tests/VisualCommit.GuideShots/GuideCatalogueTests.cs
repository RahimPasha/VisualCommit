using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The guide's pages (<c>docs/guide/src</c>) and the catalogue of pictures held to each other
/// (docs/phase-8a-design.md 11.3.3, 11.4; D440): every picture a page names is in the catalogue and
/// every name of the catalogue is shown; each picture is two raw <c>&lt;img&gt;</c>s, light first,
/// each with its <c>alt</c>, from <c>pictures/</c> beside the page's folder, and with
/// <c>srcset="&lt;src&gt; 2x"</c> exactly while <see cref="GuideCamera.Scale"/> is 2. A picture is
/// what an <c>&lt;img&gt;</c> loads, its <c>src</c> and its <c>srcset</c>'s entries, outside HTML
/// comments and fenced code: a file's name in a comment, an alt text or a sentence shows nothing.
/// They read the pages alone: no app starts, and the demo's root is not needed; made-up pages prove
/// the checks themselves.
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
        var unknown = UnknownPictures(RealPages());
        Assert.True(unknown.Count == 0, "Pages name pictures the catalogue does not make:\n" + string.Join('\n', unknown));
    }

    [Fact]
    public void Every_picture_of_the_catalogue_is_shown_in_both_themes()
    {
        var missing = MissingPictures(RealPages());
        Assert.True(missing.Count == 0, $"No page shows {missing.Count} of the catalogue's pictures:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void Each_picture_is_two_images_light_first_each_with_its_alt_text_and_its_srcset()
    {
        var problems = ImageProblems(RealPages());
        Assert.True(problems.Count == 0, "Pictures not written as D440 says:\n" + string.Join('\n', problems));
    }

    [Fact]
    public void Every_picture_is_a_raw_image_from_the_pictures_folder_beside_the_pages_root()
    {
        var problems = PlaceProblems(RealPages());
        Assert.True(problems.Count == 0, "Pictures from elsewhere:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// The dark image's own <c>srcset</c> is read too, not only the light one's: at scale 2 a dark
    /// image without one, or with the light image's, is found (at scale 1, any <c>srcset</c>).
    /// </summary>
    [Fact]
    public void A_dark_image_without_its_own_srcset_is_found()
    {
        var page = new PageText("made-up.md", string.Join(
            '\n',
            Picture("stage-hunk", darkSrcset: null),
            string.Empty,
            Picture("diff-inline", darkSrcset: "pictures/diff-inline-light.png 2x"),
            string.Empty,
            Picture("diff-side-by-side", darkSrcset: Srcset("pictures/diff-side-by-side-dark.png"))));

        var problems = ImageProblems([page]);

        Assert.Equal(
            GuideCamera.Scale == 2
                ? new[] { "made-up.md line 2: srcset \"\", expected \"pictures/stage-hunk-dark.png 2x\"", "made-up.md line 5: srcset \"pictures/diff-inline-light.png 2x\", expected \"pictures/diff-inline-dark.png 2x\"" }
                : new[] { "made-up.md line 5: srcset \"pictures/diff-inline-light.png 2x\", expected none at scale 1" },
            problems);
    }

    /// <summary>A picture's name in a comment, a code block, an alt text or a sentence shows no picture, and names none.</summary>
    [Fact]
    public void Only_what_an_image_loads_shows_a_picture_or_names_one()
    {
        var all = GuideShotCatalogue.Names.ToList();
        var shownName = all[0];
        var lines = new[]
        {
            Picture(shownName),
            $"<!-- {Picture(all[1])} -->",
            "<!--",
            Picture(all[2]),
            "-->",
            "```html",
            Picture(all[3]),
            "```",
            $"The file {all[4]}-light.png and {all[4]}-dark.png in a sentence, and not-in-the-catalogue-dark.png too.",
            $"<img src=\"pictures/{shownName}-light.png\" alt=\"Like {all[5]}-dark.png\">",
            $"<img src=\"pictures/{shownName}-dark.png\" alt=\"Like {all[5]}-dark.png\">",
        };
        var page = new PageText("made-up.md", string.Join("\n", lines));

        var missing = MissingPictures([page]);
        Assert.DoesNotContain(GuideShotCatalogue.FileName(shownName, GuideTheme.Light), missing);
        Assert.DoesNotContain(GuideShotCatalogue.FileName(shownName, GuideTheme.Dark), missing);
        foreach (var name in all.Skip(1))
        {
            Assert.Contains(GuideShotCatalogue.FileName(name, GuideTheme.Light), missing);
            Assert.Contains(GuideShotCatalogue.FileName(name, GuideTheme.Dark), missing);
        }

        Assert.Empty(UnknownPictures([page]));
        Assert.Equal(
            ["made-up.md line 1: pictures/no-such-picture-light.png", "made-up.md line 1: pictures/elsewhere.png"],
            UnknownPictures([new PageText("made-up.md", "<img src=\"pictures/no-such-picture-light.png\" srcset=\"pictures/elsewhere.png 2x\" alt=\"x\">")]));
    }

    /// <summary>The pages read from <c>docs/guide/src</c>: every Markdown file there but <c>SUMMARY.md</c>, by its path from that folder.</summary>
    private static List<PageText> RealPages()
    {
        Assert.True(Directory.Exists(Pages), $"The guide's pages are not at {Pages}.");
        return [.. Directory.EnumerateFiles(Pages, "*.md", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).Equals("SUMMARY.md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => new PageText(Path.GetRelativePath(Pages, file).Replace('\\', '/'), File.ReadAllText(file)))];
    }

    /// <summary>Every file an image of <paramref name="pages"/> loads that is not one of the catalogue's pictures.</summary>
    private static List<string> UnknownPictures(IReadOnlyList<PageText> pages) =>
        [.. pages.SelectMany(page => Images(page).SelectMany(image => image.Loads().Select(file => (page, image, file))))
            .Where(loaded => PictureFile().Match(Path.GetFileName(loaded.file)) is not { Success: true } picture
                || !GuideShotCatalogue.Names.Contains(picture.Groups["name"].Value))
            .Select(loaded => $"{loaded.page.Path} line {loaded.image.Line}: {loaded.file}")];

    /// <summary>The catalogue's files, both themes of each, that no image of <paramref name="pages"/> loads.</summary>
    private static List<string> MissingPictures(IReadOnlyList<PageText> pages)
    {
        var shown = pages.SelectMany(Images).SelectMany(image => image.Loads()).Select(file => Path.GetFileName(file)).ToHashSet(StringComparer.Ordinal);
        return [.. GuideShotCatalogue.Names
            .SelectMany(name => new[] { GuideShotCatalogue.FileName(name, GuideTheme.Light), GuideShotCatalogue.FileName(name, GuideTheme.Dark) })
            .Where(file => !shown.Contains(file))];
    }

    /// <summary>
    /// D440's form, image by image: an alt text on each; on each, the light image and the dark one
    /// alike, <c>srcset</c> "<c>&lt;src&gt;</c> 2x" at scale 2 and none at scale 1; each light image
    /// followed by its dark image with the same alt text; no dark image before or without its light one.
    /// </summary>
    private static List<string> ImageProblems(IReadOnlyList<PageText> pages)
    {
        var problems = new List<string>();
        foreach (var page in pages)
        {
            var images = Images(page).ToList();
            for (var i = 0; i < images.Count; i++)
            {
                var image = images[i];
                var where = $"{page.Path} line {image.Line}";
                if (string.IsNullOrWhiteSpace(image.Alt))
                {
                    problems.Add($"{where}: no alt text");
                }

                AddSrcsetProblem(problems, where, image);
                var light = LightPicture().Match(image.Src);
                if (light.Success)
                {
                    var dark = i + 1 < images.Count ? images[i + 1] : null;
                    var expectedDark = light.Groups["path"].Value + "-dark.png";
                    if (dark is null || dark.Src != expectedDark)
                    {
                        problems.Add($"{where}: {image.Src} is not followed by its dark image {expectedDark}");
                    }
                    else
                    {
                        if (dark.Alt != image.Alt)
                        {
                            problems.Add($"{where}: the two images of {light.Groups["path"].Value} have different alt texts");
                        }

                        // The dark image is passed over below, so its own srcset is read here.
                        AddSrcsetProblem(problems, $"{page.Path} line {dark.Line}", dark);
                        i++;
                    }
                }
                else if (DarkPicture().IsMatch(image.Src))
                {
                    problems.Add($"{where}: {image.Src} comes before, or without, its light image");
                }
            }
        }

        return problems;
    }

    /// <summary>Raw images only (Markdown's image syntax cannot carry <c>srcset</c>), each from <c>pictures/</c> beside the pages' root.</summary>
    private static List<string> PlaceProblems(IReadOnlyList<PageText> pages)
    {
        var problems = new List<string>();
        foreach (var page in pages)
        {
            var text = Visible(page.Text);
            foreach (Match markdown in MarkdownImage().Matches(text))
            {
                problems.Add($"{page.Path} line {LineOf(text, markdown.Index)}: a Markdown image, which cannot carry srcset: {markdown.Value}");
            }

            var prefix = string.Concat(Enumerable.Repeat("../", page.Path.Count(c => c == '/'))) + "pictures/";
            foreach (var image in Images(page))
            {
                if (!image.Src.StartsWith(prefix, StringComparison.Ordinal) || image.Src[prefix.Length..].Contains('/', StringComparison.Ordinal))
                {
                    problems.Add($"{page.Path} line {image.Line}: {image.Src} is not in {prefix}");
                }
            }
        }

        return problems;
    }

    private static void AddSrcsetProblem(List<string> problems, string where, PageImage image)
    {
        var expected = Srcset(image.Src);
        if (image.Srcset != expected)
        {
            problems.Add($"{where}: srcset \"{image.Srcset}\", expected {(expected is null ? "none at scale 1" : $"\"{expected}\"")}");
        }
    }

    /// <summary>The page's <c>&lt;img&gt;</c> tags outside HTML comments and fenced code, with their line numbers.</summary>
    private static IEnumerable<PageImage> Images(PageText page)
    {
        var text = Visible(page.Text);
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

    /// <summary>
    /// <paramref name="text"/> with its HTML comments and fenced code blocks blanked out, each
    /// character but a line's end made a space, so that line numbers stay.
    /// </summary>
    private static string Visible(string text)
    {
        var visible = text.ToCharArray();
        void Blank(int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (visible[i] is not ('\n' or '\r'))
                {
                    visible[i] = ' ';
                }
            }
        }

        foreach (Match comment in HtmlComment().Matches(text))
        {
            Blank(comment.Index, comment.Index + comment.Length);
        }

        var lines = new string(visible).Split('\n');
        var offset = 0;
        string? fence = null;
        var fenceStart = 0;
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r').TrimStart(' ');
            if (fence is null)
            {
                if (line.Length - line.TrimStart(' ').Length <= 3 && Fence().Match(trimmed) is { Success: true } opening)
                {
                    fence = opening.Value;
                    fenceStart = offset;
                }
            }
            else if (trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.TrimEnd().All(c => c == fence[0]))
            {
                Blank(fenceStart, offset + line.Length);
                fence = null;
            }

            offset += line.Length + 1;
        }

        if (fence is not null)
        {
            Blank(fenceStart, text.Length);
        }

        return new string(visible);
    }

    /// <summary>A made-up picture written as D440 says, two lines; its dark image's <c>srcset</c> as given, or as D440 says.</summary>
    private static string Picture(string name, string? darkSrcset = "") =>
        $"<img src=\"pictures/{name}-light.png\"{SrcsetAttribute(Srcset($"pictures/{name}-light.png"))} alt=\"A made-up picture.\">\n"
        + $"<img src=\"pictures/{name}-dark.png\"{SrcsetAttribute(darkSrcset == string.Empty ? Srcset($"pictures/{name}-dark.png") : darkSrcset)} alt=\"A made-up picture.\">";

    /// <summary>The <c>srcset</c> D440 gives an image of <paramref name="src"/> at <see cref="GuideCamera.Scale"/>: "<c>&lt;src&gt;</c> 2x" at 2, none at 1.</summary>
    private static string? Srcset(string src) => GuideCamera.Scale == 2 ? src + " 2x" : null;

    private static string SrcsetAttribute(string? srcset) => srcset is null ? string.Empty : $" srcset=\"{srcset}\"";

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>A page of the guide: its path from <c>docs/guide/src</c>, with <c>/</c>, and its text.</summary>
    private sealed record PageText(string Path, string Text);

    private sealed record PageImage(int Line, string Src, string? Srcset, string? Alt)
    {
        /// <summary>What the image loads: its <c>src</c> and each <c>srcset</c> entry's address.</summary>
        public IEnumerable<string> Loads()
        {
            if (Src.Length > 0)
            {
                yield return Src;
            }

            foreach (var entry in (Srcset ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return entry.Split(' ', 2)[0];
            }
        }

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"line {Line}: {Src}");
    }

    [GeneratedRegex(@"^(?<name>[a-z0-9]+(?:-[a-z0-9]+)*?)-(?:light|dark)\.png$")]
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

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex Fence();
}
