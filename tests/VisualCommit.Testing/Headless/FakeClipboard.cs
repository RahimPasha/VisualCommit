using VisualCommit.App.Services;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Stands in for the clipboard in the scripted walk-through and the headless tests
/// (docs/phase-3-design.md 2.32, 7.4): it records every text the app copied, in order, and leaves
/// the machine's clipboard alone, so the default test run never changes what the user copied.
/// </summary>
public sealed class FakeClipboard : IClipboardService
{
    private readonly Lock _gate = new();
    private readonly List<string> _texts = [];

    /// <summary>Every text the app put on the clipboard, in order.</summary>
    public IReadOnlyList<string> Texts
    {
        get
        {
            lock (_gate)
            {
                return [.. _texts];
            }
        }
    }

    /// <summary>What the clipboard holds now: the last text copied, or null when nothing was.</summary>
    public string? Text
    {
        get
        {
            lock (_gate)
            {
                return _texts.Count > 0 ? _texts[^1] : null;
            }
        }
    }

    public Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            _texts.Add(text);
        }

        return Task.CompletedTask;
    }
}
