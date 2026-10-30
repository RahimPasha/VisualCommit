using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.GraphTestHost;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The graph's pull-request badge (docs/phase-7a-design.md 11.2, 11.4; D309): the icon, the label's
/// width, the cut name, "+N" folding, one badge per pull request per row, the tooltip, and a map that
/// changes nothing when it is equal or empty.
/// </summary>
public sealed class GraphPullRequestBadgeTests
{
    private const string LongBranch = "feature/a-very-long-branch-name-that-the-graph-must-cut";

    [AvaloniaFact]
    public void The_pull_request_icon_is_lucide_s_on_the_24_grid()
    {
        Assert.True(Application.Current!.TryFindResource("IconPullRequest", out var resource), "IconPullRequest is not a resource.");
        var bounds = Assert.IsAssignableFrom<Geometry>(resource).Bounds;
        Assert.Equal((3, 3, 18, 18), (Math.Round(bounds.X, 2), Math.Round(bounds.Y, 2), Math.Round(bounds.Width, 2), Math.Round(bounds.Height, 2)));
    }

    [AvaloniaFact]
    public void A_label_with_a_pull_request_is_its_badge_wider_and_keeps_its_name()
    {
        var data = Graph(3, (0, Local("feature/login")), (1, Remote("origin/docs/x")), (2, Local("main", head: true)));
        using var plain = Show(new CommitGraphControl { Data = data });
        using var host = Show(new CommitGraphControl { Data = data, PullRequests = ByRef(("refs/heads/feature/login", Pull(1, "Add the login form", "feature/login"))) });

        var before = Assert.Single(plain.Control.LabelsInRow(0));
        var label = Assert.Single(host.Control.LabelsInRow(0));
        Assert.Equal(("feature/login", "feature/login", RefLabelKind.LocalBranch, "refs/heads/feature/login"), (label.Text, label.FullText, label.Kind, label.RefName));
        Assert.Equal(new PullRequestBadge(1, "Add the login form", false), label.Badge);
        Assert.Null(before.Badge);
        Assert.Equal(before.Bounds.Width + host.BadgeWidth(1), label.Bounds.Width, 0.01);
        Assert.Equal(before.Bounds.X, label.Bounds.X);

        // Part G's measure (open point 41): #1 is about 12.4, the badge 31.4, the label to x 117.8.
        Assert.Equal(31.4, host.BadgeWidth(1), 0.6);
        Assert.Equal(117.8, label.Bounds.Right, 1.0);

        // A row whose ref has no pull request is as without the map.
        Assert.Equal(plain.Control.LabelsInRow(1), host.Control.LabelsInRow(1));
        Assert.Equal(plain.Control.LabelsInRow(2), host.Control.LabelsInRow(2));
    }

    [AvaloniaFact]
    public void A_long_name_is_cut_and_the_badge_kept_inside_the_column()
    {
        var data = Graph(2, (0, Remote("origin/" + LongBranch)), (1, Local("main", head: true)));
        using var host = Show(new CommitGraphControl { Data = data, PullRequests = ByRef(("refs/remotes/origin/" + LongBranch, Pull(3, "Shorten long labels", LongBranch))) });

        var label = Assert.Single(host.Control.LabelsInRow(0));
        var column = host.Control.Columns.Refs;
        Assert.EndsWith("…", label.Text, StringComparison.Ordinal);
        Assert.Equal("origin/" + LongBranch, label.FullText);
        Assert.Equal(new PullRequestBadge(3, "Shorten long labels", false), label.Badge);
        Assert.True(label.Bounds.Right <= column.Right, $"The label ends at {label.Bounds.Right}, past the column's {column.Right}.");

        // The name as drawn plus the badge fill the label: the badge is whole.
        var name = host.LabelTextWidth(label.Text);
        Assert.Equal(CommitGraphControl.LabelPadding + name + host.BadgeWidth(3) + CommitGraphControl.LabelPadding, label.Bounds.Width, 0.01);

        // One letter more would not fit (CutToFit's longest start).
        var longer = LongBranch.Length > label.Text.Length - 1 ? ("origin/" + LongBranch)[..label.Text.Length] + "…" : label.Text;
        Assert.True(CommitGraphControl.LabelStart + (2 * CommitGraphControl.LabelPadding) + host.LabelTextWidth(longer) + host.BadgeWidth(3) > column.Width);
    }

    [AvaloniaFact]
    public void A_badge_folds_the_next_label_into_plus_n_sooner_and_plus_n_has_none()
    {
        // Two labels that fit side by side without the badge, and not with it.
        var data = Graph(2, (0, Local("login")), (0, Remote("origin/x")), (1, Local("main", head: true)));
        using var plain = Show(new CommitGraphControl { Data = data });
        var pull = Pull(1, "Add the login form", "feature/login");
        using var host = Show(new CommitGraphControl
        {
            Data = data,
            PullRequests = ByRef(("refs/heads/login", pull), ("refs/remotes/origin/x", pull)),
        });

        Assert.Equal(["login", "origin/x"], plain.Control.LabelsInRow(0).Select(label => label.Text));
        var labels = host.Control.LabelsInRow(0);
        Assert.Equal(2, labels.Count);
        Assert.Equal(("+1", true, null), (labels[1].Text, labels[1].IsOverflow, labels[1].Badge));
        Assert.Equal(("login", "login", 1), (labels[0].Text, labels[0].FullText, labels[0].Badge?.Number));
        Assert.True(labels[1].Bounds.Right <= host.Control.Columns.Refs.Right);
    }

    [AvaloniaFact]
    public void A_pull_request_has_one_badge_per_row_and_each_pull_request_its_own()
    {
        var one = Pull(1, "One", "a");
        var two = Pull(2, "Two", "b");
        var data = Graph(3, (0, Local("a")), (0, Remote("origin/a")), (1, Local("b")), (1, Local("c")), (2, Local("main", head: true)));
        using var host = Show(new CommitGraphControl
        {
            Data = data,
            PullRequests = ByRef(("refs/heads/a", one), ("refs/remotes/origin/a", one), ("refs/heads/b", one), ("refs/heads/c", two)),
        });

        Assert.Equal([("a", 1), ("origin/a", 0)], host.Control.LabelsInRow(0).Select(label => (label.Text, label.Badge?.Number ?? 0)));

        // A branch ahead of its upstream: the pull request's badge on each row; another pull request's beside it.
        Assert.Equal([("b", 1), ("c", 2)], host.Control.LabelsInRow(1).Select(label => (label.Text, label.Badge?.Number ?? 0)));
    }

    [AvaloniaFact]
    public void The_tooltip_names_each_label_s_pull_request_also_in_plus_n()
    {
        var login = Pull(1, "Add the login form", "feature/login");
        var readme = Pull(2, "Update the readme", "docs/readme", draft: true);
        var data = Graph(3, (0, Local("feature/login")), (0, Remote("origin/feature/login")), (1, Remote("origin/docs/readme")), (2, Local("main", head: true)), (2, Remote("origin/main")));
        using var host = Show(new CommitGraphControl
        {
            Data = data,
            PullRequests = ByRef(("refs/heads/feature/login", login), ("refs/remotes/origin/feature/login", login), ("refs/remotes/origin/docs/readme", readme)),
        });

        host.MoveMouse(host.Control.LabelsInRow(0)[0].Bounds.Center);
        Assert.Equal("feature/login (pull request #1: Add the login form)\norigin/feature/login (pull request #1: Add the login form)", ToolTip.GetTip(host.Control));
        host.MoveMouse(host.Control.LabelsInRow(1)[0].Bounds.Center);
        Assert.Equal("origin/docs/readme (draft pull request #2: Update the readme)", ToolTip.GetTip(host.Control));
        host.MoveMouse(host.Control.LabelsInRow(2)[0].Bounds.Center);
        Assert.Equal("main\norigin/main", ToolTip.GetTip(host.Control));
    }

    [AvaloniaFact]
    public void An_equal_map_keeps_the_rows_and_a_changed_one_updates_them_without_new_data()
    {
        var data = Graph(2, (0, Local("feature/login")), (1, Local("main", head: true)));
        var control = new CommitGraphControl { Data = data, PullRequests = ByRef(("refs/heads/feature/login", Pull(1, "Add the login form", "feature/login"))) };
        using var host = Show(control);
        host.Capture();
        var frames = 0;
        control.FrameDrawn += (_, _) => frames++;

        // An equal map, another instance: no new layout, nothing drawn again.
        control.PullRequests = ByRef(("refs/heads/feature/login", Pull(1, "Add the login form", "feature/login") with { Author = "ada" }));
        host.Capture();
        Assert.Equal(0, frames);

        // Another title: the same data, the label's badge and the frame follow.
        control.PullRequests = ByRef(("refs/heads/feature/login", Pull(1, "Add the login form again", "feature/login")));
        host.Capture();
        Assert.Equal(1, frames);
        Assert.Same(data, control.Data);
        Assert.Equal(new PullRequestBadge(1, "Add the login form again", false), Assert.Single(control.LabelsInRow(0)).Badge);

        // A draft now, then the ref without one.
        control.PullRequests = ByRef(("refs/heads/feature/login", Pull(1, "Add the login form again", "feature/login", draft: true)));
        Assert.True(Assert.Single(control.LabelsInRow(0)).Badge!.IsDraft);
        control.PullRequests = ByRef();
        Assert.Null(Assert.Single(control.LabelsInRow(0)).Badge);
    }

    [AvaloniaFact]
    public void An_empty_map_lays_out_and_draws_as_a_control_never_given_one()
    {
        var data = Graph(3, (0, Local("feature/login")), (0, Remote("origin/feature/login")), (1, Remote("origin/" + LongBranch)), (2, Local("main", head: true)));
        using var plain = Show(new CommitGraphControl { Data = data });
        using var host = Show(new CommitGraphControl { Data = data, PullRequests = ByRef() });

        for (var row = 0; row < data.Count; row++)
        {
            Assert.Equal(plain.Control.LabelsInRow(row), host.Control.LabelsInRow(row));
        }

        var expected = plain.Capture();
        var actual = host.Capture();
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.True(expected.PixelAt(x, y) == actual.PixelAt(x, y), $"The pixel at ({x}, {y}) differs.");
            }
        }
    }
}
