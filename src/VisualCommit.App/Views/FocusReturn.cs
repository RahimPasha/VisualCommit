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
    /// <see cref="DispatcherPriority.Loaded"/>): if the focus is lost, the element
    /// <paramref name="target"/> gives then takes it by <paramref name="method"/>, when it is shown
    /// and enabled. The focus is lost when nothing has it, the window has it, or it lies outside
    /// <paramref name="scope"/>; or, when <paramref name="stillLost"/> is given, when that says so
    /// of the focused element (null for none), the scope then being only where the window is found.
    /// A focus the user moved where it is not lost meanwhile stays where it is. Call it as the run
    /// starts, or just before (a button's click, which comes before its command runs).
    /// </summary>
    public static void AfterRun(
        IAsyncRelayCommand command,
        Control scope,
        Func<InputElement?> target,
        NavigationMethod method,
        Func<IInputElement?, bool>? stillLost = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(target);

        // The window is taken now: the command may take the scope out of it (a panel that goes).
        var topLevel = TopLevel.GetTopLevel(scope);
        void Follow(Task run) =>
            run.ContinueWith(
                _ => Dispatcher.UIThread.Post(() => Return(topLevel, scope, target, method, stillLost), DispatcherPriority.Loaded),
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

    private static void Return(TopLevel? topLevel, Control scope, Func<InputElement?> target, NavigationMethod method, Func<IInputElement?, bool>? stillLost)
    {
        var window = topLevel ?? TopLevel.GetTopLevel(scope);
        var focused = window?.FocusManager?.GetFocusedElement();
        var lost = stillLost is not null
            ? stillLost(focused)
            : !(focused is Visual visual && !ReferenceEquals(visual, window) && visual.IsEffectivelyVisible
                && (ReferenceEquals(visual, scope) || scope.IsVisualAncestorOf(visual)));
        if (lost && target() is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } element)
        {
            element.Focus(method);
        }
    }

    /// <summary>
    /// The usual test of a focus that a run took away from <paramref name="pressed"/>: nothing has
    /// it, <paramref name="topLevel"/> (the window) has it, or the element that has it is no longer
    /// shown or enabled; or <paramref name="pressed"/> itself has it while <paramref name="stillThere"/>
    /// says it no longer stands where it was pressed. Any other focus was moved there meanwhile, by
    /// the user, and stays.
    /// </summary>
    public static Func<IInputElement?, bool> LostFrom(TopLevel? topLevel, InputElement pressed, Func<bool> stillThere)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(stillThere);
        return focused => focused is null
            || ReferenceEquals(focused, topLevel)
            || focused is Visual { IsEffectivelyVisible: false }
            || focused is InputElement { IsEffectivelyEnabled: false }
            || (ReferenceEquals(focused, pressed) && !stillThere());
    }
}
