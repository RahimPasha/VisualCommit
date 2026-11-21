using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The text dialog's text view (D432), in a window of its own with no session, showing the
/// third-party notices as the app embeds them: read-only, wrapped, no tab kept; Down scrolls a
/// line, PageDown the view less a line, End to the true last line of the wrapped text, Home to the
/// top; Tab leaves it; its peer is a document whose value is the text.
/// </summary>
public sealed class DocumentTextViewTests
{
    private const string Title = "Third-party notices";

    private static (DiffHost Host, DocumentTextView View, Button After) Show(string text)
    {
        var view = new DocumentTextView { Height = 400 };
        AutomationProperties.SetName(view, Title);
        var after = new Button { Content = "After" };
        var panel = new StackPanel { Children = { view, after } };
        var host = DiffHost.Show(panel, 720, 520, ThemeVariant.Dark);
        view.Show(text);
        DiffHost.Settle();
        Assert.True(view.FocusText());
        DiffHost.Settle();
        return (host, view, after);
    }

    [AvaloniaFact]
    public void The_view_is_read_only_wrapped_and_keeps_no_tab()
    {
        var (host, view, _) = Show(EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices));
        using var window = host;

        Assert.True(view.IsReadOnly);
        Assert.True(view.WordWrap);
        Assert.False(view.Options.AcceptsTab);
        Assert.Equal(11.5, view.FontSize);
        Assert.Equal(EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices), view.Document.Text);
        Assert.Equal(0, view.ScrollOffset.Y);
        Assert.True(view.TextArea.IsFocused);
    }

    [AvaloniaFact]
    public void Down_scrolls_a_line_and_PageDown_the_view_less_a_line()
    {
        var (host, view, _) = Show(EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices));
        using var window = host;
        var line = view.LineHeight;
        Assert.True(line > 10, $"A line is {line} high.");

        host.PressKey(Key.Down);
        Assert.Equal(line, view.ScrollOffset.Y, 0.5);
        host.PressKey(Key.Up);
        Assert.Equal(0, view.ScrollOffset.Y, 0.5);

        host.PressKey(Key.PageDown);
        Assert.Equal(view.ScrollViewport.Height - line, view.ScrollOffset.Y, 0.5);

        // The hidden caret stays at the start: Up and Down scroll, they do not move it.
        Assert.Equal(0, view.TextArea.Caret.Offset);
    }

    [AvaloniaFact]
    public void End_shows_the_last_line_and_Home_the_first()
    {
        var text = EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices);
        var (host, view, _) = Show(text);
        using var window = host;

        host.PressKey(Key.End);
        DiffHost.Settle();
        var end = view.ScrollExtent.Height - view.ScrollViewport.Height;
        Assert.True(end > 0, "The notices are longer than the view.");
        Assert.Equal(end, view.ScrollOffset.Y, 1.0);

        // The document's last line (empty, after the file's final line end) and the file's last
        // line are drawn whole at the view's bottom.
        var textView = view.TextArea.TextView;
        var document = view.Document;
        Assert.Equal(string.Empty, document.GetText(document.Lines[^1]));
        var lastWritten = document.Lines.Last(line => document.GetText(line).Length > 0);
        Assert.Equal(text.TrimEnd('\n').Split('\n')[^1], document.GetText(lastWritten));
        foreach (var number in new[] { lastWritten.LineNumber, document.LineCount })
        {
            var visual = textView.GetVisualLine(number);
            Assert.NotNull(visual);
            Assert.True(visual!.VisualTop + visual.Height <= textView.VerticalOffset + textView.Bounds.Height + 0.5, $"Line {number} ends below the view.");
            Assert.True(visual.VisualTop >= textView.VerticalOffset - 0.5, $"Line {number} starts above the view.");
        }

        Assert.Equal(document.TextLength, view.TextArea.Caret.Offset);

        host.PressKey(Key.Home);
        Assert.Equal(0, view.ScrollOffset.Y, 0.5);
        Assert.Equal(0, view.TextArea.Caret.Offset);

        host.PressKey(Key.End, RawInputModifiers.Control);
        DiffHost.Settle();
        Assert.Equal(view.ScrollExtent.Height - view.ScrollViewport.Height, view.ScrollOffset.Y, 1.0);
        host.PressKey(Key.Home, RawInputModifiers.Control);
        Assert.Equal(0, view.ScrollOffset.Y, 0.5);
    }

    [AvaloniaFact]
    public void Tab_leaves_the_text()
    {
        var (host, view, after) = Show(EmbeddedTexts.Read(EmbeddedTexts.License));
        using var window = host;
        var before = view.Document.Text;

        host.PressKey(Key.Tab);

        Assert.True(after.IsFocused, "Tab did not move the focus out of the text.");
        Assert.Equal(before, view.Document.Text);
    }

    [AvaloniaFact]
    public void The_peer_is_a_document_whose_value_is_the_text_and_whose_name_is_the_title()
    {
        var license = EmbeddedTexts.Read(EmbeddedTexts.License);
        var (host, view, _) = Show(license);
        using var window = host;

        var peer = ControlAutomationPeer.CreatePeerForElement(view);
        Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
        Assert.Equal(Title, peer.GetName());
        var value = Assert.IsAssignableFrom<IValueProvider>(peer);
        Assert.True(value.IsReadOnly);
        Assert.Equal(license, value.Value);
    }
}
