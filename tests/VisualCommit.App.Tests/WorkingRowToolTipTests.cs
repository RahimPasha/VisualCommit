using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The working-changes row's text cut short in a narrow Message column ("Working changes · 1
/// conflict" with three lanes at 1100×700, phase 3's check 14 step 8 and check 25 step 6) shows
/// the whole text in its tooltip, as every cut text in the app does.
/// </summary>
public class WorkingRowToolTipTests
{
    [AvaloniaFact]
    public void A_cut_working_row_text_shows_whole_in_a_tooltip_and_a_whole_one_has_none()
    {
        var data = Linear(3);
        var control = new CommitGraphControl
        {
            Data = data,
            Dates = new DateDisplay(TimeZoneInfo.Utc),
            ShowsWorkingRow = true,
            WorkingRowText = "Working changes · 1 conflict",
        };
        var window = new Window { Width = 290, Height = 300, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        Hover(window, control, control.Columns.Message.X + 20, control.WorkingRowBounds.Center.Y);
        Assert.Equal("Working changes · 1 conflict", ToolTip.GetTip(control));

        // Outside the Message column the row has no tooltip.
        Hover(window, control, control.Columns.Graph.X + 4, control.WorkingRowBounds.Center.Y);
        Assert.Null(ToolTip.GetTip(control));

        // Shown whole in a wide window: no tooltip.
        window.Width = 1200;
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
        Hover(window, control, control.Columns.Message.X + 20, control.WorkingRowBounds.Center.Y);
        Assert.Null(ToolTip.GetTip(control));
        window.Close();
    }

    private static void Hover(Window window, CommitGraphControl control, double x, double y)
    {
        window.MouseMove(control.TranslatePoint(new Point(x, y), window)!.Value);
        Dispatcher.UIThread.RunJobs();
    }

    private static CommitGraphData Linear(int count)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var commits = Enumerable.Range(0, count)
            .Select(row => new CommitInfo(Sha(row), row + 1 < count ? [Sha(row + 1)] : [], "Test Author", "author@example.com", date.AddMinutes(-row), date.AddMinutes(-row), $"Commit {row}"))
            .ToList();
        var layout = new GraphLayout();
        var rows = commits.Select(layout.Add).ToList();
        var data = new CommitGraphData(new RepoRefs(new HeadState("main", commits[0].Sha), [], [], []));
        data.Append(commits, rows);
        data.Complete();
        return data;

        static string Sha(int row) => row.ToString("x40", System.Globalization.CultureInfo.InvariantCulture);
    }
}
