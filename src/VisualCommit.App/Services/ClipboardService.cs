using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace VisualCommit.App.Services;

/// <summary>
/// <see cref="IClipboardService"/> through the window's clipboard (docs/phase-3-design.md 2.32):
/// <c>TopLevel.Clipboard.SetTextAsync</c>, on the UI thread. The default test run never creates
/// this class (<c>ShellDriver</c> injects a fake), so it never touches the clipboard.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public ClipboardService(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        TopLevel = topLevel;
    }

    private Func<TopLevel?> TopLevel { get; }

    /// <summary>Puts <paramref name="text"/> on the clipboard. Without a window (the app is closing) nothing is copied.</summary>
    public async Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetTextAsync(text)).ConfigureAwait(false);
            return;
        }

        if (TopLevel()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
