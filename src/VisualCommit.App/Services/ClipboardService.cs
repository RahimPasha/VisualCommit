using Avalonia.Controls;

namespace VisualCommit.App.Services;

/// <summary>
/// <see cref="IClipboardService"/> through the window's clipboard (docs/phase-3-design.md 2.32). A
/// placeholder of work package WP0 so that <c>AppSession</c> can create it; WP4 owns this file and
/// builds it with <c>TopLevel.Clipboard.SetTextAsync</c>.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public ClipboardService(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        TopLevel = topLevel;
    }

    private Func<TopLevel?> TopLevel { get; }

    public Task SetTextAsync(string text) =>
        throw new NotImplementedException("Copying is not built yet (phase 3, WP4).");
}
