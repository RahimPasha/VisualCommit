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
    /// with octo's image in the details panel (<c>g01a-badges-and-avatar</c>).
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
        Assert.Single(app.LogLinesSince(logMark), line => line.EndsWith(PartG.HelloShowing, StringComparison.Ordinal));
        var avatars = new AppPaths(app.DataDirectory).AvatarsDirectory;
        foreach (var (user, file) in PartG.IndexUsers)
        {
            var route = "avatar:" + user;
            var avatar = WaitForFake(served.GitHub, requests, request => request.Route == route && request.Status == 200, $"GET avatars/u/{user}", NetworkTimeout);
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
        WaitTelling(
            app,
            () => app.ShownElements("DetailsAuthorAvatar") is [var image] && image.Name == PartG.AvatarName,
            "the details panel's avatar before the author",
            () => $"{app.ShownElements("DetailsAuthorAvatar").Count} avatars show.");
        app.DetailsFileRow("README.md", StepTimeout);
        Compare(app, served, PartG.G01a);

        // Step 3.
        AssertCleanEnd(app, served);
    }
}
