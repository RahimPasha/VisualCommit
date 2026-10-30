using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
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
    /// G1: in <paramref name="picture"/> row 0's label holds the name whole to x 80.4, the icon from
    /// 86.4 and <c>#1</c> from 99.4 (±1, in the graph's x): the columns that hold ink against the
    /// label's fill, between its rounded ends; the name's ink ends before the gap to the icon, the
    /// icon's starts after it, and <c>#1</c>'s is the first after the gap that follows the icon.
    /// </summary>
    private static void AssertLoginLabel(ShellDriver app, Screenshot picture)
    {
        var graph = Graph(app);
        var label = graph.LabelsInRow(0)[0].Bounds;
        Point At(double x, double y) => graph.TranslatePoint(new Point(x, y), app.Window) ?? throw new InvalidOperationException("The graph is not in the window.");
        var fill = picture.PixelAt(At(label.X + 3, label.Center.Y));
        var ink = new List<int>();
        for (var x = (int)Math.Ceiling(label.X + 3); x < (int)Math.Floor(label.Right - 3); x++)
        {
            for (var y = label.Y + 2; y < label.Bottom - 2; y++)
            {
                if (picture.PixelAt(At(x, y)) != fill)
                {
                    ink.Add(x);
                    break;
                }
            }
        }

        var gap = (G.LoginNameRight + G.LoginIconLeft) / 2;
        var (nameEnd, iconStart, numberStart) = (ink.Where(x => x < gap).Max(), ink.Where(x => x > gap).Min(), ink.Where(x => x > G.LoginNumberLeft - 2).Min());
        Assert.True(Math.Abs(nameEnd - G.LoginNameRight) <= 1, string.Create(CultureInfo.InvariantCulture, $"Row 0's name ends at x {nameEnd}; the report gives {G.LoginNameRight} ±1."));
        Assert.True(Math.Abs(iconStart - G.LoginIconLeft) <= 1, string.Create(CultureInfo.InvariantCulture, $"Row 0's icon starts at x {iconStart}; the report gives {G.LoginIconLeft} ±1."));
        Assert.True(Math.Abs(numberStart - G.LoginNumberLeft) <= 1, string.Create(CultureInfo.InvariantCulture, $"Row 0's #1 starts at x {numberStart}; the report gives {G.LoginNumberLeft} ±1."));
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
    /// At 1920×1080 (G3, G6): each row's author in the picture, the Author column at x 928 to 1068 —
    /// with <paramref name="images"/> octo's and ada's rows show their image at x + 8 to + 24 (its
    /// centre white, 5 left of it the user's colour) and the name from x + 30; every other row
    /// (<c>BareRows</c>), and every row without them, the name from x + 8 and no image: no pixel of the
    /// image's 16×16 box within ±24 of either user's colour, and the 4×4 block at its centre not all white.
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
                // square tell it. A name's glyph can be near white in the dark theme (Ada's "A" at
                // x + 16, top + 13), so the square is the whole 4×4 block, which no glyph's stroke fills.
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

            // The name's first ink: + 30 beside an image, + 8 without (a glyph's bearing allowed: 2).
            var from = colour is null ? column.X : column.X + 25;
            var ink = -1d;
            for (var x = from; x < column.Right && ink < 0; x++)
            {
                for (var y = top + 6; y < top + 21; y++)
                {
                    if (picture.PixelAt(At(x, y)) != background)
                    {
                        ink = x;
                        break;
                    }
                }
            }

            var start = column.X + (colour is null ? 8 : 30);
            Assert.True(ink >= start && ink <= start + 2, string.Create(CultureInfo.InvariantCulture, $"Row {row}'s author begins at x {ink}, not {start}."));
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
