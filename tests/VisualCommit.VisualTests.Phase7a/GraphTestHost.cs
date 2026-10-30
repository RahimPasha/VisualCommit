using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing.Headless;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// WP-V's graph tests' window and data (docs/phase-7a-design.md 11.4): a window of its own that
/// shows one <see cref="CommitGraphControl"/>, as <c>CommitGraphControlTests</c>' <c>Host</c> does,
/// a linear history with refs on chosen rows, and pull requests and their map.
/// </summary>
internal sealed class GraphTestHost : IDisposable
{
    /// <summary>The graph area at 1100×700 (phase 1): 440 wide.</summary>
    public const double AreaWidth = 440;

    /// <summary>The graph area at 1920×1080: 1260 wide, every column shown.</summary>
    public const double WideWidth = 1260;

    private GraphTestHost(Window window, CommitGraphControl control)
    {
        Window = window;
        Control = control;
    }

    public Window Window { get; }

    public CommitGraphControl Control { get; }

    /// <summary>Shows <paramref name="control"/> alone in a window of <paramref name="width"/> by <paramref name="height"/>, dark unless given.</summary>
    public static GraphTestHost Show(CommitGraphControl control, double width = AreaWidth, double height = 300, ThemeVariant? theme = null)
    {
        // The theme is set on the window, not the application: other tests on the same UI thread
        // may change the application's theme while this one waits.
        var window = new Window { Width = width, Height = height, Content = control, RequestedThemeVariant = theme ?? ThemeVariant.Dark };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new GraphTestHost(window, control);
    }

    public Screenshot Capture()
    {
        Dispatcher.UIThread.RunJobs();
        var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window has not rendered a frame.");
        return new Screenshot(frame);
    }

    /// <summary>Moves the mouse to a point of the control.</summary>
    public void MoveMouse(Point point)
    {
        Window.MouseMove(Control.TranslatePoint(point, Window) ?? throw new InvalidOperationException("The control is not in the window."), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose() => Window.Close();

    /// <summary>The label's typeface (Inter SemiBold, the control's family) and <paramref name="text"/>'s width at the label's size.</summary>
    public double LabelTextWidth(string text)
    {
        var layout = new TextLayout(text, new Typeface(Control.FontFamily, FontStyle.Normal, FontWeight.SemiBold), CommitGraphControl.LabelFontSize, Brushes.Black, textWrapping: TextWrapping.NoWrap, maxLines: 1);
        return layout.WidthIncludingTrailingWhitespace;
    }

    /// <summary>What a badge adds to a label: 6, the icon 10, 3, then <c>#n</c> (11.2).</summary>
    public double BadgeWidth(int number) =>
        CommitGraphControl.BadgeGap + CommitGraphControl.LabelIconSize + CommitGraphControl.BadgeIconGap + LabelTextWidth("#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// A complete linear history of <paramref name="authors"/>.Count commits, newest first, the
    /// commit of row i by <paramref name="authors"/>[i] (name and e-mail), with <paramref name="refs"/>
    /// pointing at their rows. HEAD is on <c>main</c> at the bottom row unless a ref is the head.
    /// </summary>
    public static CommitGraphData Graph(IReadOnlyList<(string Name, string Email)> authors, params (int Row, GitRef Ref)[] refs)
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var count = authors.Count;
        var commits = Enumerable.Range(0, count)
            .Select(row => new CommitInfo(
                ShaOf(row),
                row + 1 < count ? [ShaOf(row + 1)] : [],
                authors[row].Name,
                authors[row].Email,
                start.AddMinutes(count - row),
                start.AddMinutes(count - row),
                "Commit " + row.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        var gitRefs = refs.Select(entry => entry.Ref with { TargetSha = ShaOf(entry.Row) }).ToList();
        var head = gitRefs.FirstOrDefault(gitRef => gitRef.IsHead);
        var data = new CommitGraphData(new RepoRefs(new HeadState(head?.Name ?? "main", head?.TargetSha ?? ShaOf(count - 1)), gitRefs, [], ["origin", "other"]));
        var layout = new GraphLayout();
        data.Append(commits, commits.Select(layout.Add).ToList());
        data.Complete();
        return data;
    }

    /// <summary><see cref="Graph(IReadOnlyList{ValueTuple{string, string}}, ValueTuple{int, GitRef}[])"/> with <paramref name="count"/> commits by Test Author.</summary>
    public static CommitGraphData Graph(int count, params (int Row, GitRef Ref)[] refs) =>
        Graph([.. Enumerable.Repeat(("Test Author", "author@example.com"), count)], refs);

    public static string ShaOf(int row) => row.ToString("x40", System.Globalization.CultureInfo.InvariantCulture);

    public static GitRef Local(string name, bool head = false) =>
        new("refs/heads/" + name, name, RefKind.LocalBranch, string.Empty, IsHead: head);

    public static GitRef Remote(string name) =>
        new("refs/remotes/" + name, name, RefKind.RemoteBranch, string.Empty, RemoteName: name.Split('/')[0]);

    public static HostedRepository OctoHello { get; } = new(HostingProvider.GitHub, "github.com", "octo", "hello");

    public static PullRequest Pull(int number, string title, string head, bool draft = false) =>
        new(number, title, "octo", draft, new Uri($"https://github.com/octo/hello/pull/{number}"), head, ShaOf(0), OctoHello, "main", OctoHello, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    /// <summary>A map by full ref name, as <c>HostingState.ByRef</c> holds it.</summary>
    public static IReadOnlyDictionary<string, PullRequest> ByRef(params (string RefName, PullRequest Pull)[] entries) =>
        entries.ToDictionary(entry => entry.RefName, entry => entry.Pull, StringComparer.Ordinal);
}
