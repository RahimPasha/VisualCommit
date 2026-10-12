using Avalonia.Controls;
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

    [AvaloniaFact]
    public void A_line_that_does_not_fit_is_still_reported()
    {
        var single = new TextBlock { Text = "Discard the changes", Width = 40 };
        using var host = DiffHost.Show(new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, Children = { single } }, 400, 200, ThemeVariant.Dark);

        var problem = Assert.Single(LayoutAudit.FindClippedText(host.Window));

        Assert.StartsWith("\"Discard the changes\" needs a width of", problem, StringComparison.Ordinal);
    }
}
