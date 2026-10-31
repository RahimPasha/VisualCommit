using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;
using G = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartG;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part G's helpers (docs/test-reports/phase-7a.md, part G's conventions): a picture with no token on
/// screen and no text cut, the labels against the facts' table, a row's selection and its author
/// line, the avatars' pixels in the graph and the details panel, the requests and the end of every
/// method.
/// </summary>
public sealed partial class Phase7aChecksG
{
    private static readonly Regex RequestId = new(@"\(request (?<id>[^)]+)\)", RegexOptions.CultureInvariant);

    private static IReadOnlyList<(string Name, string Token)> Tokens { get; } =
        [(nameof(OctoToken), OctoToken), (nameof(OctoClassicToken), OctoClassicToken), (nameof(AdaToken), AdaToken)];

    /// <summary>A picture of part G: no token on screen before it, no text cut but by an ellipsis after it.</summary>
    private static Screenshot ShotG(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>No visible text, tooltip or automation name holds a token; a failure names the control and the token's constant.</summary>
    private static void AssertNoTokenOnScreen(ShellDriver app)
    {
        app.Settle();
        foreach (var control in app.Window.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible))
        {
            foreach (var text in new[] { AutomationProperties.GetName(control), control is TextBlock block ? block.Text : null, control is TextBox box and not SecretTextBox ? box.Text : null, ToolTip.GetTip(control) as string })
            {
                foreach (var (tokenName, token) in Tokens)
                {
                    Assert.False(text?.Contains(token, StringComparison.Ordinal) == true, $"A text of {AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name} holds {tokenName}.");
                }
            }
        }
    }

    private static CommitGraphControl Graph(ShellDriver app) => app.Find<CommitGraphControl>("GraphRows");

    private static RepositoryViewModel HelloOf(ShellDriver app) =>
        app.Session.Shell.ActiveTab.Repository ?? throw new Xunit.Sdk.XunitException("hello's tab is not open.");

    private static Control Avatar(ShellDriver app) => app.FindByAutomationId("DetailsAuthorAvatar");

    /// <summary>Every row's labels as the facts' table gives them: text, whole name, kind, ref, from x 6, the right edge ±1, the row's top + 4 to + 22, the badge.</summary>
    private static void AssertLabels(ShellDriver app, IReadOnlyList<IReadOnlyList<G.Label>> table)
    {
        var graph = Graph(app);
        for (var row = 0; row < table.Count; row++)
        {
            var actual = graph.LabelsInRow(row);
            var expected = table[row];
            Assert.True(expected.Count == actual.Count, $"Row {row} has the labels {string.Join(", ", actual.Select(label => label.Text))}.");
            var top = graph.RowBounds(row).Y;
            for (var i = 0; i < expected.Count; i++)
            {
                var (want, label) = (expected[i], actual[i]);
                Assert.Equal((want.Text, want.FullText, want.Kind, want.RefName), (label.Text, label.FullText, label.Kind.ToString(), label.RefName));
                Assert.True(Math.Abs(label.Bounds.Right - want.Right) <= 1, string.Create(CultureInfo.InvariantCulture, $"Row {row}'s {label.Text} ends at {label.Bounds.Right:0.0}; the report gives {want.Right} ±1."));
                Assert.Equal((top + 4, top + 22), (label.Bounds.Top, label.Bounds.Bottom));
                if (i == 0)
                {
                    Assert.Equal(6, label.Bounds.X, 0.01);
                }

                Assert.Equal(
                    want.Badge == 0 ? null : new PullRequestBadge(want.Badge, G.Badges[want.Badge].Title, G.Badges[want.Badge].Draft),
                    label.Badge);
            }
        }
    }

    /// <summary>
    /// G1: row 0's label as laid out and as drawn in <paramref name="picture"/>. Laid out, from the
    /// control (<see cref="CommitGraphControl.BadgeInRow"/>, the places the drawing uses), to the
    /// report's one decimal: the name whole to x 80.4, the icon from 86.4 and <c>#1</c> from 99.4.
    /// Drawn (<see cref="AssertInkInParts"/>), between the label's rounded corners and, in its middle
    /// rows, out to 1 px inside its edges: the name's and <c>#1</c>'s ink within their glyphs'
    /// outlines across and their laid-out boxes down (<see cref="LabelTextInk"/>), ±1; the icon's
    /// within its stroked outline (<see cref="IconInk"/>); none in the padding or the gaps between them.
    /// </summary>
    private static void AssertLoginLabel(ShellDriver app, Screenshot picture)
    {
        var graph = Graph(app);
        var placed = graph.LabelsInRow(0)[0];
        var label = placed.Bounds;
        var badge = graph.BadgeInRow(0, 0) ?? throw new Xunit.Sdk.XunitException("Row 0's label has no badge.");
        Assert.Equal(
            (G.LoginNameRight, G.LoginIconLeft, G.LoginNumberLeft),
            (Math.Round(badge.Name.Right, 1), Math.Round(badge.Icon.X, 1), Math.Round(badge.Number.X, 1)));
        var fill = picture.PixelAt(graph.TranslatePoint(new Point(label.X + 3, label.Center.Y), app.Window) ?? throw new InvalidOperationException("The graph is not in the window."));
        InkPart[] parts =
        [
            new("the name", LabelTextInk(graph, placed.Text, badge.Name), TextAllowance),
            new("the icon", IconInk(graph, badge.Icon), 0),
            new("#1", LabelTextInk(graph, string.Create(CultureInfo.InvariantCulture, $"#{placed.Badge?.Number}"), badge.Number), TextAllowance),
        ];
        const double corner = CommitGraphControl.LabelCornerRadius;
        AssertInkInParts(
            app,
            picture,
            new Rect(label.X + corner, label.Y, label.Width - (2 * corner), label.Height),
            fill,
            "Row 0's label between its rounded corners",
            parts);
        AssertInkInParts(
            app,
            picture,
            new Rect(label.X + 1, label.Y + corner, label.Width - 2, label.Height - (2 * corner)),
            fill,
            "Row 0's label in its middle rows",
            parts);
    }

    /// <summary>
    /// Where the badge's icon, drawn in its 10-px <paramref name="box"/>, puts its ink: its outline
    /// (<c>IconPullRequest</c>, 3 to 21 on the 24 grid) scaled into the box, grown by half the stroke,
    /// whose caps and joins are round. Skia draws the path itself on every platform, so it needs no
    /// allowance; the box would hide 1 px, as the stroked outline keeps 0.6 px inside it on each side.
    /// </summary>
    private static Rect IconInk(CommitGraphControl graph, Rect box)
    {
        Assert.True(graph.TryFindResource("IconPullRequest", graph.ActualThemeVariant, out var icon), "IconPullRequest is not a resource.");
        var outline = Assert.IsAssignableFrom<Geometry>(icon).Bounds;
        var scale = CommitGraphControl.LabelIconSize / CommitGraphControl.IconGridSize;
        return new Rect(box.X + (outline.X * scale), box.Y + (outline.Y * scale), outline.Width * scale, outline.Height * scale).Inflate(CommitGraphControl.LabelIconStroke / 2);
    }

    /// <summary>
    /// Where a label's <paramref name="text"/>, laid out at <paramref name="laidOut"/>, puts its ink
    /// before <see cref="TextAllowance"/>: across, from its glyphs' outlines' left to their right; down,
    /// its laid-out box. The outlines are the font's own, unhinted (a glyph run's geometry), and Inter
    /// is the app's own file, so they are the same on every platform. Across, the laid-out box ±1
    /// would hide 2 px: the <c>1</c> of <c>#1</c> ends its outline 1.3 px inside it (at x 110.5, the
    /// box at 111.8), so <c>#1</c> drawn 2 px right of its place stays in the box. The text is laid
    /// out again in the label's typeface, as wide as the control's or the check fails.
    /// </summary>
    private static Rect LabelTextInk(CommitGraphControl graph, string text, Rect laidOut)
    {
        var layout = new TextLayout(text, new Typeface(graph.FontFamily, FontStyle.Normal, FontWeight.SemiBold), CommitGraphControl.LabelFontSize, Brushes.Black, textWrapping: TextWrapping.NoWrap, maxLines: 1);
        Assert.True(layout.WidthIncludingTrailingWhitespace == laidOut.Width, string.Create(CultureInfo.InvariantCulture, $"{text} is {layout.WidthIncludingTrailingWhitespace} wide in the label's typeface, {laidOut.Width} in the graph."));
        var run = Assert.Single(Assert.Single(layout.TextLines).TextRuns.OfType<ShapedTextRun>());
        var outline = run.GlyphRun.BuildGeometry().Bounds;
        return new Rect(laidOut.X + outline.X, laidOut.Y, outline.Width, laidOut.Height);
    }

    /// <summary>
    /// How far a text's ink may lie outside its glyphs' outlines, and so outside its laid-out box:
    /// each platform's font back-end under Skia (DirectWrite on Windows, CoreText on macOS, FreeType
    /// on Linux) places and smooths glyphs its own way, so the ink reaches less than a pixel past an
    /// outline's edge or stops short of it. Row 0's name has its outline from x 12.2 to 79.7 and its
    /// layout from 12 to 80.4: CI's macOS ended its ink at x 79; Windows inks x 80 faintly, and puts
    /// the <c>f</c> into x 11. Paths and images, which Skia draws itself on every platform, get none.
    /// </summary>
    private const double TextAllowance = 1;

    /// <summary>A part of a drawing whose ink lies in <paramref name="Box"/> (the graph's coordinates), grown by <paramref name="Allowance"/> on every side.</summary>
    private sealed record InkPart(string What, Rect Box, double Allowance);

    /// <summary>
    /// The ink rule of part G's pictures, which holds whatever the platform's text rasterising: every
    /// pixel of <paramref name="picture"/> inside <paramref name="area"/> (the graph's coordinates)
    /// that is not <paramref name="background"/> overlaps one of <paramref name="parts"/>' grown
    /// boxes, and each part holds some ink, so the gaps between the parts are clear. A failure names
    /// the pixel in the graph's coordinates.
    /// </summary>
    private static void AssertInkInParts(ShellDriver app, Screenshot picture, Rect area, Color background, string what, params InkPart[] parts)
    {
        var origin = Graph(app).TranslatePoint(default, app.Window) ?? throw new InvalidOperationException("The graph is not in the window.");
        var inWindow = area.Translate(new Vector(origin.X, origin.Y));
        var inked = new bool[parts.Length];
        for (var y = (int)Math.Ceiling(inWindow.Top); y + 1 <= inWindow.Bottom; y++)
        {
            for (var x = (int)Math.Ceiling(inWindow.Left); x + 1 <= inWindow.Right; x++)
            {
                var pixel = picture.PixelAt(x, y);
                if (pixel == background)
                {
                    continue;
                }

                var (gx, gy) = (x - origin.X, y - origin.Y);
                var part = Array.FindIndex(parts, part => Grown(part) is var box && gx + 1 > box.Left && gx < box.Right && gy + 1 > box.Top && gy < box.Bottom);
                if (part < 0)
                {
                    throw new Xunit.Sdk.XunitException(string.Create(CultureInfo.InvariantCulture, $"{what} has ink {pixel} at ({gx}, {gy}), in none of: {string.Join("; ", parts.Select(Describe))}."));
                }

                inked[part] = true;
            }
        }

        for (var i = 0; i < parts.Length; i++)
        {
            Assert.True(inked[i], $"{what} shows no ink of {Describe(parts[i])}.");
        }

        static Rect Grown(InkPart part) => part.Box.Inflate(part.Allowance);
        static string Describe(InkPart part) =>
            string.Create(CultureInfo.InvariantCulture, $"{part.What} at x {Grown(part).Left:0.0} to {Grown(part).Right:0.0}, y {Grown(part).Top:0.0} to {Grown(part).Bottom:0.0}");
    }

    /// <summary>The details panel's changed files as "{status} {path}".</summary>
    private static List<string> ChangedFiles(ShellDriver app) =>
        [.. HelloOf(app).Details.Files.Where(row => row.IsFile).Select(row => $"{row.StatusLetter} {row.Key}")];

    /// <summary>Row <paramref name="row"/> whole within the graph's view.</summary>
    private static void AssertRowInView(ShellDriver app, int row)
    {
        var graph = Graph(app);
        var bounds = graph.RowBounds(row);
        Assert.True(bounds.Top >= 0 && bounds.Bottom <= graph.ViewportHeight, string.Create(CultureInfo.InvariantCulture, $"Row {row} at y {bounds.Top} to {bounds.Bottom} is not in the graph's view, 0 to {graph.ViewportHeight}."));
    }

    /// <summary>G3 method 2: no author cut: with the pointer over each row's Author cell the graph has no tooltip (a cut author's is its whole name); then the pointer goes back to rest.</summary>
    private static void AssertNoAuthorCut(ShellDriver app)
    {
        var graph = Graph(app);
        var column = graph.Columns.Author;
        for (var row = 0; row < G.Rows.Count; row++)
        {
            var cell = graph.TranslatePoint(new Point(column.X + (column.Width / 2), graph.RowBounds(row).Center.Y), app.Window) ?? throw new InvalidOperationException("The graph is not in the window.");
            app.MoveMouse(cell);
            Assert.True(ToolTip.GetTip(graph) is null, $"Row {row}'s author is cut: the graph's tooltip is \"{ToolTip.GetTip(graph)}\".");
        }

        app.MoveToNeutral();
    }

    /// <summary>Clicks row <paramref name="row"/> and waits for its author line, and for the image when <paramref name="withAvatar"/>.</summary>
    private static async Task SelectAsync(ShellDriver app, int row, string authorLine, bool withAvatar)
    {
        var hello = HelloOf(app);
        app.Click(app.GraphRowPoint(row));
        await app.WaitForAsync(() => hello.SelectedIndex == row && !hello.Details.IsLoading && hello.Details.AuthorText == authorLine, $"row {row}'s details");
        if (withAvatar)
        {
            await app.WaitForAsync(() => Avatar(app).IsVisible, "the details panel's avatar");
        }

        app.WaitForCommitDetails();
    }

    /// <summary>The author line (part G's conventions): with an image the ellipse 16×16 at <c>DetailsDate</c>'s left, its top <c>DetailsAuthor</c>'s, the text 22 further; without one the text at <c>DetailsDate</c>'s left.</summary>
    private static void AssertAuthorLine(ShellDriver app, string authorLine, bool withAvatar)
    {
        app.Settle();
        var date = app.BoundsOf(app.FindByAutomationId("DetailsDate"));
        var author = app.BoundsOf(app.FindByAutomationId("DetailsAuthor"));
        Assert.Equal(authorLine, HelloOf(app).Details.AuthorText);
        Assert.Equal(withAvatar, Avatar(app).IsVisible);
        Assert.Equal(G.AvatarName, AutomationProperties.GetName(Avatar(app)));
        if (withAvatar)
        {
            var place = app.BoundsOf(Avatar(app));
            Assert.Equal((date.X, author.Y, 16d, 16d), (Math.Round(place.X, 1), Math.Round(place.Y, 1), place.Width, place.Height));
            Assert.Equal(date.X + 22, author.X, 0.5);
        }
        else
        {
            Assert.Equal(date.X, author.X, 0.5);
        }
    }

    /// <summary>In <paramref name="picture"/> the details panel's image: its centre pixel white and the pixel 5 left of it <paramref name="colour"/>, ±24 per channel.</summary>
    private static bool ShowsDetailsImage(ShellDriver app, Screenshot picture, string colour)
    {
        var centre = app.BoundsOf(Avatar(app)).Center;
        return Near(Colors.White, picture.PixelAt(centre)) && Near(Color.Parse(colour), picture.PixelAt(new Point(centre.X - 5, centre.Y)));
    }

    private static bool Near(Color expected, Color actual) =>
        Math.Abs(expected.R - actual.R) <= G.ColourTolerance && Math.Abs(expected.G - actual.G) <= G.ColourTolerance && Math.Abs(expected.B - actual.B) <= G.ColourTolerance;

    /// <summary>
    /// At 1920×1080 (G3, G6): each row's author, the Author column at x 928 to 1068. Laid out, from
    /// the control (<see cref="CommitGraphControl.AuthorInRow"/>, the places the drawing uses): with
    /// <paramref name="images"/> octo's and ada's rows have their image at x + 8 to + 24, the row's top
    /// + 5 to + 21, and the name from x + 30; every other row (<c>BareRows</c>), and every row without
    /// them, no image and the name from x + 8. In the picture: an image's centre white, 5 left of it
    /// the user's colour and the 4×4 block at its centre white; where there is none, no pixel of the
    /// image's 16×16 box within ±24 of either user's colour and that block not all white; and the
    /// cell's ink (<see cref="AssertInkInParts"/>) inside the image's square and the name's laid-out
    /// box ±1 only, each holding some, so the gaps before and after the name are clear.
    /// </summary>
    private static void AssertAuthorColumn(ShellDriver app, Screenshot picture, bool images)
    {
        var graph = Graph(app);
        var column = graph.Columns.Author;
        Assert.True(column.IsVisible, "The Author column is hidden.");
        Assert.Equal((G.AuthorX, G.AuthorRight), (column.X, column.Right));
        Assert.Equal(Enumerable.Range(0, G.Rows.Count).Except(G.OctoRows).Except(G.AdaRows), G.BareRows);
        for (var row = 0; row < G.Rows.Count; row++)
        {
            var colour = !images ? null : G.OctoRows.Contains(row) ? G.OctoColour : G.AdaRows.Contains(row) ? G.AdaColour : null;
            var top = graph.RowBounds(row).Y;
            var placed = graph.AuthorInRow(row) ?? throw new Xunit.Sdk.XunitException($"Row {row} draws no author.");
            Assert.Equal(colour is null ? (Rect?)null : new Rect(column.X + 8, top + 5, 16, 16), placed.Avatar);
            Assert.True(placed.Name.X == column.X + (colour is null ? 8 : 30), string.Create(CultureInfo.InvariantCulture, $"Row {row}'s author is laid out from x {placed.Name.X}, not {column.X + (colour is null ? 8 : 30)}."));
            Point At(double x, double y) => graph.TranslatePoint(new Point(x, y), app.Window) ?? throw new InvalidOperationException("The graph is not in the window.");
            var background = picture.PixelAt(At(column.Right - 2, top + 2));

            // The 4×4 block around an image's centre: white in an image (its white square is 6 px).
            var centre = Enumerable.Range(14, 4).SelectMany(x => Enumerable.Range(11, 4).Select(y => picture.PixelAt(At(column.X + x, top + y)))).ToList();
            if (colour is not null)
            {
                Assert.True(Near(Colors.White, picture.PixelAt(At(column.X + 16, top + 13))), $"Row {row}'s image has no white centre.");
                Assert.True(Near(Color.Parse(colour), picture.PixelAt(At(column.X + 11, top + 13))), $"Row {row}'s image is not {colour}.");
                Assert.True(centre.TrueForAll(pixel => Near(Colors.White, pixel)), $"Row {row}'s image has no white square.");
            }
            else
            {
                // An image here would start at x + 8 as the name does: its colours and its white
                // square tell it. In the light theme a pixel without ink is the white background, so
                // one white pixel would tell nothing: an image's square is the whole 4×4 block white,
                // and on a bare row the name's strokes leave ink in that block.
                Assert.False(centre.TrueForAll(pixel => Near(Colors.White, pixel)), $"Row {row} has a white square at an image's centre.");
                for (var y = top + CommitGraphControl.AvatarTop; y < top + CommitGraphControl.AvatarTop + CommitGraphControl.AvatarSize; y++)
                {
                    for (var x = column.X + 8; x < column.X + 8 + CommitGraphControl.AvatarSize; x++)
                    {
                        var pixel = picture.PixelAt(At(x, y));
                        Assert.False(Near(Color.Parse(G.OctoColour), pixel) || Near(Color.Parse(G.AdaColour), pixel), string.Create(CultureInfo.InvariantCulture, $"Row {row} has an image's colour {pixel} at ({x}, {y - top}) of its row."));
                    }
                }
            }

            // The cell's ink: the image's (a bitmap in a circle, which Skia draws itself on every
            // platform) inside its square, the name's inside its laid-out box ±1.
            var name = new InkPart("the name", placed.Name, TextAllowance);
            AssertInkInParts(
                app,
                picture,
                new Rect(column.X, top, column.Width, CommitGraphControl.RowHeight),
                background,
                $"Row {row}'s author",
                placed.Avatar is { } image ? [new InkPart("the image", image, 0), name] : [name]);
        }
    }

    /// <summary>The fake's API, avatar and page requests since <paramref name="mark"/> as "route status", the query and token where asked.</summary>
    private static List<FakeGitHubRequest> RequestsSince(ServedHosting served, int mark) => [.. served.GitHub.Requests.Skip(mark)];

    private static bool IsAvatarRun(FakeGitHubRequest request) =>
        request.Route is { } route && (route.StartsWith("commits:", StringComparison.Ordinal) || route.StartsWith("avatar:", StringComparison.Ordinal));

    /// <summary>
    /// G3's avatars' requests since <paramref name="mark"/>: exactly one commits page (the query,
    /// the token, 200) after the repository's, no lookup, and octo's and ada's images once each
    /// without the token, 200; then the avatars and <c>avatars/</c> as part G's facts give them: its
    /// files, the two images byte for byte, the index's users and the map's entries.
    /// </summary>
    private static void AssertAvatarRun(ShellDriver app, TempDirectory data, ServedHosting served, int mark)
    {
        var since = RequestsSince(served, mark);
        var commits = Assert.Single(since, request => request.Route?.StartsWith("commits:", StringComparison.Ordinal) == true);
        Assert.Equal(("GET", "commits:octo/hello", G.CommitsQuery, true, 200), (commits.Method, commits.Route, commits.Query, commits.TokenPresent, commits.Status));
        Assert.True(since.FindIndex(request => request.Route == "repo:octo/hello") < since.IndexOf(commits), "The commits page came before the repository's read.");
        foreach (var id in new[] { OctoId, AdaId })
        {
            var image = Assert.Single(since, request => request.Route == string.Create(CultureInfo.InvariantCulture, $"avatar:{id}"));
            Assert.Equal(("GET", G.AvatarQuery, false, 200), (image.Method, image.Query, image.TokenPresent, image.Status));
        }

        Assert.DoesNotContain(since, request => request.Route == string.Create(CultureInfo.InvariantCulture, $"avatar:{AcmeId}"));
        var avatars = Path.Combine(data.Path, "avatars");
        var hello = HelloOf(app).Avatars.FileByEmail;
        Assert.Equal(
            [(AdaEmail, Path.GetFullPath(Path.Combine(avatars, "1003.png"))), (OctoEmail, Path.GetFullPath(Path.Combine(avatars, "1001.png")))],
            hello.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, Path.GetFullPath(pair.Value))));
        Assert.Equal(
            ["1001.png", "1003.png", WithPort(G.AuthorsFile, served.GitHub.Port).Replace('/', Path.DirectorySeparatorChar), "index.json"],
            Directory.GetFiles(avatars, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(avatars, file)).Order(StringComparer.Ordinal));
        Assert.Equal(TestImages.SquareOn(64, 64, TestImages.Rgb.Parse(G.OctoColour), TestImages.Rgb.Parse("#FFFFFF"), 24), File.ReadAllBytes(Path.Combine(avatars, "1001.png")));
        Assert.Equal(TestImages.SquareOn(64, 64, TestImages.Rgb.Parse(G.AdaColour), TestImages.Rgb.Parse("#FFFFFF"), 24), File.ReadAllBytes(Path.Combine(avatars, "1003.png")));

        // The index's users and the e-mail map's entries, in order (the dates are the system clock's).
        var port = served.GitHub.Port;
        using (var index = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(avatars, "index.json"))))
        {
            Assert.Equal(G.IndexUsers, index.RootElement.GetProperty("users").EnumerateObject().Select(user => (user.Name, user.Value.GetProperty("file").GetString() ?? string.Empty)));
        }

        using (var map = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(avatars, WithPort(G.AuthorsFile, port)))))
        {
            Assert.Equal(
                G.AuthorsEntries.Select(entry => (entry.Email, entry.UserId, entry.AvatarUrl is null ? null : WithPort(entry.AvatarUrl, port))),
                map.RootElement.GetProperty("authors").EnumerateArray().Select(author => (author.GetProperty("email").GetString() ?? string.Empty, Number(author, "userId"), Text(author, "avatarUrl"))));
        }

        static long? Number(JsonElement entry, string name) => entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;
        static string? Text(JsonElement entry, string name) => entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>The request id a log line names, <c>(request …)</c>.</summary>
    private static string RequestIdOf(string line)
    {
        var match = RequestId.Match(line);
        Assert.True(match.Success, $"\"{line}\" names no request.");
        return match.Groups["id"].Value;
    }

    /// <summary>Every method's end (part G's conventions): the app closed, the tripwire for the three tokens, no fake error or refused address, no 400 or 415, no avatar request with a token, no launch.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting served, params string[] more)
    {
        var calls = app.Session.GitCalls;
        app.Dispose();
        foreach (var (_, token) in Tokens)
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.Hello.Path, served.Tool.Path, .. more], calls);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Route?.StartsWith("avatar:", StringComparison.Ordinal) == true && request.TokenPresent);
        Assert.Empty(app.Launcher.Requests);
    }
}
