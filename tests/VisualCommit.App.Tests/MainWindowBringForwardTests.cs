using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.App.Views;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="MainWindow.BringForward"/>, which a second start on the same data folder asks for
/// (docs/phase-8a-design.md 1.11, 4.3.3): a minimised window comes back in the state it had before,
/// maximised or normal, and is activated; after it closed the answer is Closing. A bare window, as
/// <c>PanelViewTests</c> makes one; no session. The headless platform keeps a window active when it
/// is minimised or another is shown, so each test first makes it inactive as a platform reports it
/// (<see cref="Deactivate"/>): only an activation can make it active again.
/// </summary>
public sealed class MainWindowBringForwardTests
{
    [AvaloniaFact]
    public void A_minimised_window_that_was_maximised_comes_back_maximised()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            window.WindowState = WindowState.Maximized;
            Dispatcher.UIThread.RunJobs();
            window.WindowState = WindowState.Minimized;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(WindowState.Minimized, window.WindowState);
            var activations = Deactivate(window);

            Assert.Equal(ForwardAnswer.Activated, window.BringForward());
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.True(window.IsActive);
            Assert.Equal(1, activations());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_minimised_window_that_was_normal_comes_back_normal()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            window.WindowState = WindowState.Minimized;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(WindowState.Minimized, window.WindowState);
            var activations = Deactivate(window);

            Assert.Equal(ForwardAnswer.Activated, window.BringForward());
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.IsActive);
            Assert.Equal(1, activations());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_window_that_is_not_minimised_keeps_its_state_and_is_activated()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            window.WindowState = WindowState.Maximized;
            Dispatcher.UIThread.RunJobs();
            var activations = Deactivate(window);

            Assert.Equal(ForwardAnswer.Activated, window.BringForward());
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.True(window.IsActive);
            Assert.Equal(1, activations());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_closed_window_answers_closing()
    {
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ForwardAnswer.Closing, window.BringForward());
    }

    /// <summary>
    /// Makes <paramref name="window"/> inactive as a platform reports another window taking the
    /// foreground, which the headless platform never does by itself, and counts its activations from
    /// then on. The platform's callback is read by reflection, as <c>HeadlessInputClock</c> reads the
    /// headless window's clock: Avalonia does not let code outside it reach the callback.
    /// </summary>
    private static Func<int> Deactivate(Window window)
    {
        var impl = window.PlatformImpl ?? throw new InvalidOperationException("The window has no platform implementation.");
        var callback = impl.GetType().GetProperty("Deactivated", BindingFlags.Instance | BindingFlags.Public);
        if (impl.GetType().FullName != "Avalonia.Headless.HeadlessWindowImpl" || callback?.GetValue(impl) is not Action deactivated)
        {
            throw new InvalidOperationException(
                $"The window's {impl.GetType().FullName} has no public Deactivated callback: not a headless window, or Avalonia.Headless changed, and the test needs another way to deactivate it.");
        }

        deactivated();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsActive, "The window is still active after its platform reported it deactivated.");

        var activations = 0;
        window.Activated += (_, _) => activations++;
        return () => activations;
    }
}
