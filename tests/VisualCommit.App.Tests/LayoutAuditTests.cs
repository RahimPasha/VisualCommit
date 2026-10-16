using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>What <see cref="LayoutAudit"/> reports as text that is not shown in full, and what it lets through.</summary>
public class LayoutAuditTests
{
    [AvaloniaFact]
    public void A_wrapped_text_whose_lines_fit_is_not_clipped_although_the_space_it_broke_at_hangs_past_the_edge()
    {
        var word = new TextBlock { Text = "Discarding" };
        var wrapped = new TextBlock { Text = "Discarding all", TextWrapping = TextWrapping.Wrap };
        using var host = DiffHost.Show(new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Children = { word, wrapped } }, 400, 200, ThemeVariant.Dark);

        // Just wider than the first word, narrower than the word and its space: two lines.
        wrapped.Width = Math.Ceiling(word.TextLayout.Width) + 1;
        DiffHost.Settle();

        Assert.Equal(2, wrapped.TextLayout.TextLines.Count);
        Assert.True(
            wrapped.TextLayout.WidthIncludingTrailingWhitespace > wrapped.Bounds.Width + 0.75,
            "The first line's trailing space hangs past the edge, the case this test is about.");
        Assert.Empty(LayoutAudit.FindClippedText(host.Window));
    }

    /// <summary>
    /// A list at the bottom of a page that clips shows its last row in part: the page cuts that
    /// row where the list's view ends anyway, so with scrolled-out text let through it is not a
    /// fault (phase 5's backups list above the status bar); without, it still is.
    /// </summary>
    [AvaloniaFact]
    public void A_row_cut_at_a_lists_edge_is_let_through_also_where_a_page_around_the_list_ends_there()
    {
        var list = new ListBox
        {
            ItemsSource = Enumerable.Range(0, 12).Select(row => $"Row {row}").ToList(),
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = text, Height = 26 }),
        };
        var page = new Border { Width = 300, Height = 100, ClipToBounds = true, Child = list, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        using var host = DiffHost.Show(page, 400, 200, ThemeVariant.Dark);

        Assert.NotEmpty(LayoutAudit.FindClippedText(host.Window, allowShortenedWithToolTip: false, allowScrolledOutOfView: false));
        Assert.Empty(LayoutAudit.FindClippedText(host.Window, allowShortenedWithToolTip: false, allowScrolledOutOfView: true));

        // A text the page cuts inside the list's view is still a fault.
        page.Height = 60;
        list.Height = 100;
        DiffHost.Settle();
        Assert.NotEmpty(LayoutAudit.FindClippedText(host.Window, allowShortenedWithToolTip: false, allowScrolledOutOfView: true));
    }

    [AvaloniaFact]
    public void A_line_that_does_not_fit_is_still_reported()
    {
        var single = new TextBlock { Text = "Discard the changes", Width = 40 };
        using var host = DiffHost.Show(new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Children = { single } }, 400, 200, ThemeVariant.Dark);

        var problem = Assert.Single(LayoutAudit.FindClippedText(host.Window));

        Assert.StartsWith("\"Discard the changes\" needs a width of", problem, StringComparison.Ordinal);
    }
}
