using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.Views;

/// <summary>
/// Gives the focus back after a command whose run took it away: a dialog opened over a button that
/// the command itself disables while it runs, so that the window's own return (D104) finds the
/// opener disabled; or a row the command removed while one of its buttons had the focus.
/// </summary>
public static class FocusReturn
{
    /// <summary>
    /// When <paramref name="command"/>'s current or next run ends, once the layout has followed (at
    /// <see cref="DispatcherPriority.Loaded"/>): if nothing has the focus, the window has it, or it
    /// lies outside <paramref name="scope"/>, the element <paramref name="target"/> gives then takes
    /// it by <paramref name="method"/>, when it is shown and enabled. A focus the user moved into
    /// <paramref name="scope"/> meanwhile stays where it is. Call it as the run starts, or just
    /// before (a button's click, which comes before its command runs).
    /// </summary>
    public static void AfterRun(IAsyncRelayCommand command, Control scope, Func<InputElement?> target, NavigationMethod method)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(target);

        // The window is taken now: the command may take the scope out of it (a panel that goes).
        var topLevel = TopLevel.GetTopLevel(scope);
        void Follow(Task run) =>
            run.ContinueWith(
                _ => Dispatcher.UIThread.Post(() => Return(topLevel, scope, target, method), DispatcherPriority.Loaded),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        if (command is { IsRunning: true, ExecutionTask: { } running })
        {
            Follow(running);
            return;
        }

        // The next run: its task, which may have ended already when the command hands it over.
        var started = false;
        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IAsyncRelayCommand.ExecutionTask) && command.ExecutionTask is { } run)
            {
                started = true;
                command.PropertyChanged -= OnChanged;
                Follow(run);
            }
        }

        command.PropertyChanged += OnChanged;

        // A command that never started (it could not run) leaves nothing waiting.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!started)
                {
                    command.PropertyChanged -= OnChanged;
                }
            },
            DispatcherPriority.Loaded);
    }

    private static void Return(TopLevel? topLevel, Control scope, Func<InputElement?> target, NavigationMethod method)
    {
        var window = topLevel ?? TopLevel.GetTopLevel(scope);
        var focused = window?.FocusManager?.GetFocusedElement();
        var kept = focused is Visual visual && !ReferenceEquals(visual, window) && visual.IsEffectivelyVisible
            && (ReferenceEquals(visual, scope) || scope.IsVisualAncestorOf(visual));
        if (!kept && target() is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } element)
        {
            element.Focus(method);
        }
    }
}
