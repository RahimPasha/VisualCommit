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
/// <c>PanelViewTests</c> makes one; no session.
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

            Assert.Equal(ForwardAnswer.Activated, window.BringForward());
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.True(window.IsActive);
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

            Assert.Equal(ForwardAnswer.Activated, window.BringForward());
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.IsActive);
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

            Assert.Equal(ForwardAnswer.Activated, window.BringForward());
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.True(window.IsActive);
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
}
