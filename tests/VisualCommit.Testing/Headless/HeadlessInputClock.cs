using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The clock a headless window stamps its input with: Avalonia.Headless's internal
/// <c>HeadlessWindowImpl</c> gives every pointer and key event the milliseconds of its own
/// <see cref="Stopwatch"/> (<c>_st</c>), real time. Avalonia counts a press as a double-click's
/// second only within the platform's double-tap time (500 ms) of the first by those stamps, while
/// the public input helpers run the dispatcher's jobs and render a frame around every event, so
/// the stamps of a scripted double-click lie as far apart as that work took.
/// <see cref="ShellDriver.DoubleClick(Avalonia.Point)"/> stops the clock for its presses: they are
/// stamped as one moment, as the system stamps a user's double-click with the times of the presses,
/// however long the app then takes to handle them. Throws when the window is not a headless one or
/// Avalonia.Headless no longer keeps its clock there (an upgrade).
/// </summary>
internal static class HeadlessInputClock
{
    /// <summary>Stops <paramref name="window"/>'s input clock until the result is disposed; the clock then runs on from where it stood.</summary>
    public static IDisposable Pause(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var impl = window.PlatformImpl ?? throw new InvalidOperationException("The window has no platform implementation.");
        var field = impl.GetType().GetField("_st", BindingFlags.Instance | BindingFlags.NonPublic);
        if (impl.GetType().FullName != "Avalonia.Headless.HeadlessWindowImpl" || field?.GetValue(impl) is not Stopwatch clock)
        {
            throw new InvalidOperationException(
                $"The window's {impl.GetType().FullName} keeps no input clock in a Stopwatch field _st: not a headless window, or Avalonia.Headless changed, and ShellDriver.DoubleClick needs another way to stamp its presses.");
        }

        if (!clock.IsRunning)
        {
            return new Resume(null);
        }

        clock.Stop();
        return new Resume(clock);
    }

    private sealed class Resume(Stopwatch? clock) : IDisposable
    {
        private Stopwatch? _clock = clock;

        public void Dispose() => Interlocked.Exchange(ref _clock, null)?.Start();
    }
}
