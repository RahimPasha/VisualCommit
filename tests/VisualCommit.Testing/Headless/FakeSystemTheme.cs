using Avalonia.Threading;
using VisualCommit.App.Services;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Stands in for the operating system's light or dark mode in the scripted walk-through and the
/// headless tests (docs/phase-6-design.md 11.3, 17.2): the app's <see cref="ISystemTheme"/>, which
/// a test sets and flips, so that no picture depends on the machine's mode and a check can follow
/// the system without touching it. <c>ShellDriver.Start</c> gives every session one
/// (<c>ShellDriver.SystemTheme</c>), dark unless the test asks for light. The real window has no
/// fake: it reads the desktop's mode (11.8).
/// </summary>
public sealed class FakeSystemTheme : ISystemTheme
{
    /// <param name="dark">Whether the system asks for dark apps at first.</param>
    public FakeSystemTheme(bool dark = true) => IsDark = dark;

    public bool IsDark { get; private set; }

    public event EventHandler? Changed;

    /// <summary>
    /// Sets the system's mode, as the user switching it on the desktop would: when it changes,
    /// raises <see cref="Changed"/> on the UI thread (posted there when called from another
    /// thread). Setting the mode the system has already raises nothing, as the platform's own
    /// event does not.
    /// </summary>
    public void Set(bool dark)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Apply(dark);
        }
        else
        {
            Dispatcher.UIThread.Post(() => Apply(dark));
        }
    }

    private void Apply(bool dark)
    {
        if (dark == IsDark)
        {
            return;
        }

        IsDark = dark;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
