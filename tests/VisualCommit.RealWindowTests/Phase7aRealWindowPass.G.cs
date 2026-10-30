// Part G's real-window steps (docs/test-reports/phase-7a.md, part G, the R steps in G1's Notes;
// docs/phase-7a-design.md 13.4 G, 17 items 6 and 10): one tab on hello prepared by PartG.PrepareAsync,
// as its twin Check_G1_G2_G3_badges_tooltips_and_details is; signed in through Settings; the real
// process's own handler fetching octo's and ada's avatars without the token into the data folder's
// avatars/, and the details panel's image of octo's commit decoded and drawn at the display's scaling
// (g01a-badges-and-avatar). At 1100×700 the graph's Author column is hidden, so the capture's avatar
// is the details panel's only (the report's open point 35).
using System.Globalization;
using VisualCommit.Core;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase7aRealWindowPass
{
    /// <summary>
    /// Part G (13.4 G steps 1 to 3): after the sign-in, <c>hello: showing GitHub avatars for 2 authors</c>
    /// and the fake's <c>avatar:1001</c> and <c>avatar:1003</c> answered without a token, their images
    /// in <c>avatars/</c>; a real click on row 4 (<c>Update the readme</c>, by Octo Cat) shows the author
    /// with octo's image in the details panel, read on the screen (<see cref="ShowsOctoImage"/>), then
    /// <c>g01a-badges-and-avatar</c>.
    /// </summary>
    [Fact]
    public async Task Check_G_badge_and_avatars()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, ct: TestContext.Current.CancellationToken);
        await PartG.PrepareAsync(served.Hello);
        using var app = LaunchServed(served, emptyTab: false);

        // Step 1: signed in through Settings; D322's two lines, the avatars' for two authors.
        var requests = served.GitHub.Requests.Count;
        var logMark = app.LogMark();
        app.SignInThroughSettings(served.OctoToken, OctoLogin, NetworkTimeout);
        WaitForHostingSettled(app, logMark);
        var showing = CountLinesSince(app, logMark, line => line.EndsWith(PartG.HelloShowing, StringComparison.Ordinal));
        Assert.True(showing == 1, $"{showing} lines of the log since the sign-in end with \"{PartG.HelloShowing}\", not one.");
        var avatars = new AppPaths(app.DataDirectory).AvatarsDirectory;
        foreach (var (user, file) in PartG.IndexUsers)
        {
            var route = "avatar:" + user;
            var avatar = WaitForFake(served, requests, request => request.Route == route && request.Status == 200, $"GET avatars/u/{user}", NetworkTimeout);
            Assert.False(avatar.TokenPresent, $"The request for {route} carried the token (D252).");
            Assert.True(File.Exists(Path.Combine(avatars, file)), $"{Path.Combine(avatars, file)} does not exist.");
        }

        // Step 2: a real click on row 4 in its Message column; the details with octo's image.
        var (subject, _, name, email) = PartG.Rows[PartG.Row];
        Assert.Equal(PartG.OctoAuthorLine, string.Create(CultureInfo.InvariantCulture, $"{name} <{email}>"));
        var point = app.GraphRowPoint(PartG.Row, PartG.Lanes);
        app.ClickAt(point.X, point.Y);
        app.WaitFor(() => app.FindAll("DetailsSubject").Any(element => element.Name == subject), $"the details of \"{subject}\"", StepTimeout);
        app.WaitForText("DetailsAuthor", PartG.OctoAuthorLine, StepTimeout);

        // DetailsAuthorAvatar is a plain Ellipse: its automation peer is no control element, so UI
        // Automation does not list it (as a plain panel, DialogRow.cs). The wait reads what shows
        // instead: the author's text 22 right of the date, the room the avatar takes only while shown
        // (RightPanelView.axaml), and octo's image in that room (ShowsOctoImage), as the twin's
        // AssertAuthorLine and ShowsDetailsImage read them. Off the graph first, so that no tooltip of
        // the row covers the panel during the reads.
        app.MoveToNeutral();
        WaitTelling(
            app,
            () => ShowsOctoImage(app, out _),
            "octo's image before the author in the details panel",
            () =>
            {
                ShowsOctoImage(app, out var seen);
                return seen;
            });
        app.DetailsFileRow("README.md", StepTimeout);
        Compare(app, served, PartG.G01a);

        // Step 3.
        AssertCleanEnd(app, served);
    }

    /// <summary>How far right of <c>DetailsDate</c> the author's text starts while the avatar shows: its 16 and its margin's 6 (RightPanelView.axaml; the twin's <c>AssertAuthorLine</c>).</summary>
    private const double AvatarRoom = 22;

    /// <summary>How far the author's left may be from <see cref="AvatarRoom"/> right of the date: a physical pixel's rounding at the display's scaling, in logical pixels, well short of the 22 the avatar's room makes.</summary>
    private const double AvatarRoomTolerance = 1.5;

    /// <summary>
    /// Whether the details panel shows octo's image before the author, read without UI Automation (the
    /// avatar is no control element): the author's text starts <see cref="AvatarRoom"/> right of
    /// <c>DetailsDate</c> (±<see cref="AvatarRoomTolerance"/>), and in a capture of the client area the
    /// avatar's box, 16×16 at the date's left with the author's top (the twin's <c>AssertAuthorLine</c>),
    /// has a white centre and octo's colour 5 left of it, ±<see cref="PartG.ColourTolerance"/> per
    /// channel (the twin's <c>ShowsDetailsImage</c>; the fake's image is a white square on octo's
    /// colour, design 2.3), read at the display's scaling. <paramref name="seen"/> says what was read,
    /// for a time-out's message.
    /// </summary>
    private static bool ShowsOctoImage(RealApp app, out string seen)
    {
        if (app.ShownElements("DetailsDate") is not [var dateElement] || app.ShownElements("DetailsAuthor") is not [var authorElement])
        {
            seen = "The details panel shows no single DetailsDate and DetailsAuthor.";
            return false;
        }

        var date = app.BoundsOf(dateElement);
        var author = app.BoundsOf(authorElement);
        var room = author.Left - date.Left;
        var centreX = date.Left + 8;
        var centreY = author.Top + 8;
        System.Drawing.Color centre;
        System.Drawing.Color ring;
        using (var capture = app.CaptureClient())
        {
            var scaling = app.Scaling;
            centre = capture.ColourAt(centreX, centreY, scaling);
            ring = capture.ColourAt(centreX - 5, centreY, scaling);
        }

        var octo = System.Drawing.ColorTranslator.FromHtml(PartG.OctoColour);
        seen = string.Create(
            CultureInfo.InvariantCulture,
            $"The author starts {room:0.#} right of the date ({AvatarRoom} with the avatar shown); the avatar's centre is #{centre.R:X2}{centre.G:X2}{centre.B:X2} (white expected) and 5 left of it #{ring.R:X2}{ring.G:X2}{ring.B:X2} (octo's {PartG.OctoColour} expected), ±{PartG.ColourTolerance}.");
        return Math.Abs(room - AvatarRoom) <= AvatarRoomTolerance && Near(System.Drawing.Color.White, centre) && Near(octo, ring);
    }

    /// <summary>Whether <paramref name="actual"/> is <paramref name="expected"/> within <see cref="PartG.ColourTolerance"/> in every channel (the twin's <c>Near</c>).</summary>
    private static bool Near(System.Drawing.Color expected, System.Drawing.Color actual) =>
        Math.Abs(expected.R - actual.R) <= PartG.ColourTolerance && Math.Abs(expected.G - actual.G) <= PartG.ColourTolerance && Math.Abs(expected.B - actual.B) <= PartG.ColourTolerance;
}
