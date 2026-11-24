using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Logging;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The text dialog as a view model (D432): the first text shown at opening, the switch moving the
/// shown text, "Copy all" putting the shown text whole on the clipboard and leaving the dialog open
/// (a failure a Warning), Close answering true and Esc false, and no switch with one text. No window.
/// </summary>
public sealed class TextDocumentDialogViewModelTests
{
    private static List<TextDocumentPart> Parts() =>
    [
        new("Components and licenses", "# Third-party notices\n\nThe first text.\n", "TextDocumentPartMain"),
        new("Avalonia", "# WPF\n\nThe second text.\n", "TextDocumentPartAvalonia"),
        new("Onigwrap", "The third text.\n", "TextDocumentPartOnigwrap"),
    ];

    [Fact]
    public void The_first_text_is_shown_and_the_switch_moves_it()
    {
        var parts = Parts();
        var dialog = new TextDocumentDialogViewModel("Third-party notices", parts, new FakeClipboard(), NullAppLog.Instance);

        Assert.Equal("Third-party notices", dialog.Title);
        Assert.True(dialog.ShowsSwitch);
        Assert.Same(parts[0], dialog.Shown);
        Assert.Equal([true, false, false], parts.Select(part => part.IsShown));

        dialog.ShowCommand.Execute(parts[1]);
        Assert.Same(parts[1], dialog.Shown);
        Assert.Equal([false, true, false], parts.Select(part => part.IsShown));

        // The shown text again changes nothing; a text of another dialog is not taken.
        dialog.ShowCommand.Execute(parts[1]);
        dialog.ShowCommand.Execute(new TextDocumentPart("Other", "Other.\n", "TextDocumentPartOther"));
        Assert.Same(parts[1], dialog.Shown);
        Assert.Equal([false, true, false], parts.Select(part => part.IsShown));
        Assert.False(dialog.IsClosed);
    }

    [Fact]
    public async Task Copy_all_puts_the_shown_text_whole_on_the_clipboard_and_the_dialog_stays()
    {
        var parts = Parts();
        var clipboard = new FakeClipboard();
        var dialog = new TextDocumentDialogViewModel("Third-party notices", parts, clipboard, NullAppLog.Instance);

        await dialog.CopyAllCommand.ExecuteAsync(null);
        dialog.ShowCommand.Execute(parts[2]);
        await dialog.CopyAllCommand.ExecuteAsync(null);

        Assert.Equal([parts[0].Text, parts[2].Text], clipboard.Texts);
        Assert.False(dialog.IsClosed);
    }

    [Fact]
    public async Task Copy_all_stays_enabled_while_a_busy_clipboard_holds_the_copy()
    {
        // A disabled button loses the keyboard focus (docs/phase-8a-design.md 8.3.1); the clipboard
        // answers late while another program holds it open.
        var held = new TaskCompletionSource();
        var clipboard = new FakeClipboard { Hold = held.Task };
        var dialog = new TextDocumentDialogViewModel("MIT License", [Parts()[0]], clipboard, NullAppLog.Instance);

        var copying = dialog.CopyAllCommand.ExecuteAsync(null);

        Assert.False(copying.IsCompleted, "The copy did not wait for the clipboard.");
        Assert.True(dialog.CopyAllCommand.CanExecute(null), "Copy all is disabled while the clipboard holds the copy.");
        held.SetResult();
        await copying;
        Assert.True(dialog.CopyAllCommand.CanExecute(null));
        Assert.Equal([Parts()[0].Text], clipboard.Texts);
    }

    [Fact]
    public async Task A_copy_that_fails_is_a_warning()
    {
        var log = new RecordingLog();
        var dialog = new TextDocumentDialogViewModel("MIT License", [Parts()[0]], new RefusingClipboard(), log);

        await dialog.CopyAllCommand.ExecuteAsync(null);

        var entry = Assert.Single(log.Entries);
        Assert.Equal((LogLevel.Warning, "Copying the text to the clipboard failed."), (entry.Level, entry.Message));
        Assert.False(dialog.IsClosed);
    }

    [Fact]
    public async Task Close_answers_true_and_Esc_false()
    {
        var closed = new TextDocumentDialogViewModel("MIT License", Parts(), null, NullAppLog.Instance);
        closed.ConfirmCommand.Execute(null);
        Assert.True(closed.IsClosed);
        Assert.True(await closed.Result);

        var escaped = new TextDocumentDialogViewModel("MIT License", Parts(), null, NullAppLog.Instance);
        escaped.HandleEscape();
        Assert.True(escaped.IsClosed);
        Assert.False(await escaped.Result);
    }

    [Fact]
    public void One_text_has_no_switch()
    {
        var license = new TextDocumentPart("MIT License", "MIT License\n", "TextDocumentPartMain");
        var dialog = new TextDocumentDialogViewModel("MIT License", [license], null, NullAppLog.Instance);

        Assert.False(dialog.ShowsSwitch);
        Assert.Same(license, dialog.Shown);
        Assert.True(license.IsShown);
        Assert.Throws<ArgumentException>(() => new TextDocumentDialogViewModel("Nothing", [], null, NullAppLog.Instance));
    }

    /// <summary>A clipboard that refuses every text.</summary>
    private sealed class RefusingClipboard : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.FromException(new InvalidOperationException("The clipboard is busy."));
    }
}
