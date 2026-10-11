using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A URL in a dialog's text is never broken across lines (check 21's pictures put the host of
/// "http://127.0.0.1:47391" on a line of its own): the text breaks before the URL, the text the
/// dialog holds stays as given, and only a URL longer than a whole line is broken.
/// </summary>
public class UrlTextBlockTests
{
    private static readonly string WordJoiner = UrlTextBlock.WordJoiner.ToString();

    [Fact]
    public void Only_urls_and_ssh_addresses_are_kept_together()
    {
        Assert.Equal("Git could not fetch from origin.", UrlTextBlock.KeepUrlsTogether("Git could not fetch from origin."));

        var joined = UrlTextBlock.KeepUrlsTogether("for http://127.0.0.1:47391.");
        Assert.Equal("for h" + WordJoiner + "t" + WordJoiner + "t", joined[..9]);
        Assert.Equal("for http://127.0.0.1:47391.", joined.Replace(WordJoiner, string.Empty, StringComparison.Ordinal));

        // A quoted URL ends at its quote; an ssh address is one too; a pair of surrogates stays whole.
        var quoted = UrlTextBlock.KeepUrlsTogether("Username for 'https://example.com': ");
        Assert.EndsWith("m': ", quoted, StringComparison.Ordinal);
        Assert.Contains("e" + WordJoiner + "x", quoted, StringComparison.Ordinal);
        Assert.Contains("t" + WordJoiner + "@" + WordJoiner + "h", UrlTextBlock.KeepUrlsTogether("git@host:x.git"), StringComparison.Ordinal);
        Assert.Equal("https://host/😀", UrlTextBlock.KeepUrlsTogether("https://host/😀").Replace(WordJoiner, string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("\uD83D" + WordJoiner, UrlTextBlock.KeepUrlsTogether("https://host/😀"), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void The_credentials_dialog_keeps_the_url_on_one_line()
    {
        // Check 21 step 1, picture 21a.
        var dialog = new CredentialsViewModel(new GitPrompt(GitPromptKind.Username, "Username for 'http://127.0.0.1:47391': ", "op-1", "Fetch", "auth", "http://127.0.0.1:47391", null, null));
        var view = new CredentialsView { DataContext = dialog };
        using var host = DiffHost.Show(view, 1100, 700, ThemeVariant.Dark);
        var text = view.FindControl<TextBlock>("CredentialsText")!;

        Assert.Equal("Git needs a user name and a password or token for http://127.0.0.1:47391.", text.Text);
        Assert.Contains(DrawnLines(text), line => line.Contains("http://127.0.0.1:47391.", StringComparison.Ordinal));
        TestPictures.Save(host, "url-21a-credentials");
    }

    [AvaloniaFact]
    public void A_confirmation_keeps_its_url_whole_and_breaks_one_longer_than_a_line()
    {
        const string Url = "https://example.com/team/repository.git";
        var dialogs = new DialogHostViewModel();
        _ = dialogs.ConfirmAsync(new ConfirmationRequest("Remove remote origin?", $"Remove the remote origin and its 2 remote branches? Your local branches stay; those that track it lose their upstream. Its URL was {Url}.", "A backup is saved first, so this can be undone.", "Remove"));
        var view = new ConfirmationView { DataContext = dialogs.Current };
        using var host = DiffHost.Show(view, 1100, 700, ThemeVariant.Light);
        var question = view.FindControl<TextBlock>("ConfirmQuestion")!;

        Assert.Contains(DrawnLines(question), line => line.Contains(Url + ".", StringComparison.Ordinal));
        TestPictures.Save(host, "url-confirmation");

        // A URL longer than a line is broken where it must be, and nothing reaches past the text's width.
        var tooLong = "https://example.com/" + string.Concat(Enumerable.Repeat("very-long-path-", 8)) + "end.git";
        question.Text = $"Its URL was {tooLong}.";
        DiffHost.Settle();
        var lines = DrawnLines(question);
        Assert.True(lines.Count > 2);
        Assert.Equal($"Its URL was {tooLong}.", string.Concat(lines).TrimEnd());
        Assert.All(question.TextLayout.TextLines, line => Assert.True(line.Width <= question.Bounds.Width + 0.5));
        DiffHost.Settle();
        TestPictures.Save(host, "url-confirmation-too-long");
    }

    /// <summary>The lines <paramref name="block"/> draws, as text, without the word joiners that hold a URL together.</summary>
    private static List<string> DrawnLines(TextBlock block) =>
        [.. block.TextLayout.TextLines.Select(line =>
            string.Concat(line.TextRuns.Select(run => run.Text.ToString())).Replace(WordJoiner, string.Empty, StringComparison.Ordinal))];
}
