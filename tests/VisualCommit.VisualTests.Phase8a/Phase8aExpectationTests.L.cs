using System.Globalization;
using Avalonia.Media;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part L's expected values against the report (docs/test-reports/phase-8a.md, part L's
/// conventions; docs/phase-8a-design.md 1.5, 2.5): every colour a check reads reaches 4.5:1 on each
/// background it is read on; each new colour lies more than <see cref="TextInk.Tolerance"/> from
/// the one a check tells it from (the placeholder and the secondary text 12, the accent on a row
/// and the accent 15, the danger and lane 3 29, and each from the colour it replaces); every picture
/// name is of design 0.3's form and is named once; the real window's launches are the methods'
/// twins.
/// </summary>
public sealed class Phase8aExpectationTestsL
{
    [Fact]
    public void Every_colour_a_check_reads_reaches_4_5_to_1_where_it_reads_it()
    {
        var failures = new List<string>();
        foreach (var (theme, colours) in new[] { ("dark", L.Dark), ("light", L.Light) })
        {
            void Check(string text, Color foreground, string background, Color behind)
            {
                var ratio = Contrast.Ratio(foreground, behind);
                if (ratio < 4.5)
                {
                    failures.Add(string.Create(CultureInfo.InvariantCulture, $"{theme}: {text} on {background} is {ratio:0.00}:1."));
                }
            }

            Check("the placeholder", colours.Placeholder, "the box", colours.Box);
            Check("the typed text", colours.Typed, "the box", colours.Box);
            Check("the counter", colours.Secondary, "the box", colours.Box);
            Check("the danger", colours.Danger, "a hovered row", colours.Hover);
            Check("the danger", colours.Danger, "a selected row", colours.Selection);
            Check("the danger", colours.Danger, "a control", colours.Control);
            Check("the danger button's label", colours.DangerText, "the danger", colours.Danger);
            Check("the accent", colours.Accent, "the panel", colours.Panel);
            Check("the accent on a row", colours.AccentOnRow, "a hovered row", colours.Hover);
            Check("the accent on a row", colours.AccentOnRow, "a selected row", colours.Selection);
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));

        // Design 1.5's figures.
        Assert.Equal(4.75, Math.Round(Contrast.Ratio(L.Dark.Placeholder, L.Dark.Box), 2));
        Assert.Equal(5.00, Math.Round(Contrast.Ratio(L.Light.Placeholder, L.Light.Box), 2));
        Assert.Equal(4.55, Math.Round(Contrast.Ratio(L.Dark.Danger, L.Dark.Hover), 2));
        Assert.Equal(4.70, Math.Round(Contrast.Ratio(L.Dark.Danger, L.Dark.Selection), 2));
        Assert.Equal(5.18, Math.Round(Contrast.Ratio(L.Dark.Danger, L.Dark.Control), 2));
        Assert.Equal(6.92, Math.Round(Contrast.Ratio(L.Dark.DangerText, L.Dark.Danger), 2));
        Assert.Equal(5.94, Math.Round(Contrast.Ratio(L.Light.DangerText, L.Light.Danger), 2));
        Assert.Equal(4.78, Math.Round(Contrast.Ratio(L.Dark.AccentOnRow, L.Dark.Hover), 2));
        Assert.Equal(4.94, Math.Round(Contrast.Ratio(L.Dark.AccentOnRow, L.Dark.Selection), 2));
        Assert.Equal(4.66, Math.Round(Contrast.Ratio(L.Light.AccentOnRow, L.Light.Hover), 2));
    }

    [Fact]
    public void Each_new_colour_lies_more_than_the_tolerance_from_the_one_a_check_tells_it_from()
    {
        Assert.Equal(L.PlaceholderFromSecondary, Distance(L.Dark.Placeholder, L.Dark.Secondary));
        Assert.Equal(L.PlaceholderFromSecondary, Distance(L.Light.Placeholder, L.Light.Secondary));
        Assert.Equal(L.AccentOnRowFromAccent, Distance(L.Dark.AccentOnRow, L.Dark.Accent));
        Assert.Equal(L.DangerFromLane3, Distance(L.Dark.Danger, L.Dark.Lane3));
        foreach (var distance in new[] { L.PlaceholderFromSecondary, L.AccentOnRowFromAccent, L.DangerFromLane3 })
        {
            Assert.True(distance > TextInk.Tolerance, $"{distance} is within the tolerance of {TextInk.Tolerance}.");
        }

        foreach (var (what, now, replaced) in new[]
        {
            ("the dark placeholder", L.Dark.Placeholder, L.Dark.PlaceholderReplaced),
            ("the light placeholder", L.Light.Placeholder, L.Light.PlaceholderReplaced),
            ("the dark danger", L.Dark.Danger, L.Dark.DangerReplaced),
            ("the dark danger button's label", L.Dark.DangerText, L.Dark.DangerTextReplaced),
            ("the dark accent on a row", L.Dark.AccentOnRow, L.Dark.Accent),
        })
        {
            Assert.True(Distance(now, replaced) > TextInk.Tolerance, $"{what} is within {TextInk.Tolerance} of the colour it replaces.");
        }

        // The replaced placeholder is the secondary text at Fluent's opacity 0.5 over the box.
        Assert.Equal(L.Dark.PlaceholderReplaced, Half(L.Dark.Secondary, L.Dark.Box));
        Assert.Equal(L.Light.PlaceholderReplaced, Half(L.Light.Secondary, L.Light.Box));

        // Light keeps the danger, its label and the accent; the accent on a row is the accent there.
        Assert.Equal((L.Light.Danger, L.Light.DangerText, L.Light.Accent), (L.Light.DangerReplaced, L.Light.DangerTextReplaced, L.Light.AccentOnRow));
        Assert.Equal(1.0, L.PlaceholderOpacity);
    }

    [Fact]
    public void The_picture_names_are_of_the_reports_form_and_each_is_named_once()
    {
        Assert.Equal(31, L.Pictures.Count);
        Assert.All(L.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.All(L.Pictures, name => Assert.StartsWith("l", name, StringComparison.Ordinal));
        Assert.Equal(L.Pictures.Count, L.Pictures.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_real_windows_launches_are_the_twins_of_methods_1_and_2()
    {
        Assert.Equal(("changes", true, (1100, 700)), (L.ChangesLaunch.Scenario, L.ChangesLaunch.Dark, L.ChangesLaunch.Size));
        Assert.Equal(["l1a-placeholders-dark", "l1b-filter-typed-dark", "l5a-discard-dialog-dark", "l2a-placeholders-light", "l2b-filter-typed-light"], L.ChangesLaunch.Captures);
        Assert.Equal(("graph", true, (1100, 700)), (L.GraphLaunch.Scenario, L.GraphLaunch.Dark, L.GraphLaunch.Size));
        Assert.Equal(["l4a-delete-item-menu", "l6a-head-idle", "l6b-head-hovered", "l6c-head-selected"], L.GraphLaunch.Captures);
        Assert.All(L.ChangesLaunch.Captures.Concat(L.GraphLaunch.Captures), name => Assert.Contains(name, L.Pictures));
    }

    [Fact]
    public void The_texts_are_the_reports()
    {
        Assert.Equal("Search commands, branches, tags, stashes, remotes, repositories and workspaces", L.PalettePlaceholder);
        Assert.Equal("Message, author or SHA", L.SearchPlaceholder);
        Assert.Equal("src/main-app.txt\nrenamed from src/app.txt", L.RenamedToolTip(L.MainApp, L.App));
        Assert.Equal("src/helpers.py\nrenamed from src/util.py", L.RenamedToolTip(L.Helpers, L.Util));
        Assert.All(
            new[] { L.SetUpstreamItem, L.CreateBranchItem, L.ManageRemotesItem, L.DeleteFeatureLogin, L.DeleteMainOnOrigin },
            text => Assert.EndsWith("…", text, StringComparison.Ordinal));
    }

    /// <summary>The largest difference of two colours in one channel.</summary>
    private static int Distance(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    /// <summary><paramref name="text"/> at opacity 0.5 over <paramref name="box"/>, each channel rounded down.</summary>
    private static Color Half(Color text, Color box) =>
        Color.FromRgb((byte)((text.R + box.R) / 2), (byte)((text.G + box.G) / 2), (byte)((text.B + box.B) / 2));
}
