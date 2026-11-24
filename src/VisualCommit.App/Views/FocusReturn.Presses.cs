using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views;

/// <summary>
/// The window's rule for an action by the keys (D462): the app never takes the keyboard focus away
/// without putting it somewhere. A button pressed by the keys (it is focused and shows the keyboard's
/// frame at its Click: Enter or Space after Tab or the arrows; a click on a button that shows the
/// frame from before counts so too), a dialog's confirm run by Enter in one of its boxes, and a menu
/// opened by the keys on an element: when the action has left the focus on nothing, on the window or
/// on an element no longer shown or enabled (Avalonia gives the focus to nothing when the focused
/// element is hidden, removed or disabled), the focus goes, once the action's run has ended and the
/// layout has followed, to the first of these that can take it:
/// <list type="number">
/// <item>the element itself, shown and enabled again with the data it was pressed for;</item>
/// <item>what its view named to stand in its place (<see cref="Instead"/>);</item>
/// <item>the item now at its place in the list it lay in (its first stop), when it was an item's;</item>
/// <item>in a dialog, the dialog's first stop; elsewhere the target of the region it lay in, as F6
/// gives it (<see cref="KeyRegionNavigator.MarkedTargetIn"/>);</item>
/// <item>the graph area's target, for an element in no region (the tab strip, the toolbar, the status bar).</item>
/// </list>
/// The focus comes with the keyboard's frame when the element had it. Views that place the focus
/// themselves after such an action (a stage row's buttons, the first-run screen, the settings
/// screen's Git program row, a dialog's own return, D104) do so first: this rule looks only after
/// them and acts only when the focus is still lost. A click on a button without the frame arms
/// nothing; a focus the user has moved meanwhile stays; a dialog the action closed leaves the return
/// to D104 and to its opener's own action; and after a switch of tabs the window places the focus
/// (<c>MainWindow.FocusAfterSwitch</c>).
/// </summary>
public static partial class FocusReturn
{
    /// <summary>The pseudo-class of an element that shows the keyboard's frame (its focus came by Tab or the arrows).</summary>
    private const string FocusVisibleClass = ":focus-visible";

    /// <summary>The action by the keys each element is followed for now: a newer one replaces it.</summary>
    private static readonly ConditionalWeakTable<InputElement, ActionByKeys> Followed = new();

    /// <summary>What stands in an element's place when its action leaves it hidden or disabled (<see cref="Instead"/>).</summary>
    private static readonly ConditionalWeakTable<InputElement, InPlace> InPlaces = new();

    /// <summary>
    /// Follows every button of <paramref name="window"/> pressed by the keys (the class comment's
    /// rule). Call once, when the window is made.
    /// </summary>
    public static void FollowKeyboardPresses(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.AddHandler(
            Button.ClickEvent,
            (_, e) =>
            {
                if (e.Source is Button button && button.IsFocused && button.Classes.Contains(FocusVisibleClass))
                {
                    Follow(button, button.Command, NavigationMethod.Tab);
                }
            },
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    /// <summary>
    /// Follows an action that a key runs on <paramref name="pressed"/> without a click (a dialog's
    /// confirm run by Enter in one of its boxes, <paramref name="command"/> its command), as a press
    /// by the keys is followed; the focus comes back with the frame when <paramref name="pressed"/>
    /// shows it now. Call it just before the command runs.
    /// </summary>
    public static void AfterKeyboardAction(InputElement pressed, ICommand? command)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        Follow(pressed, command, pressed.Classes.Contains(FocusVisibleClass) ? NavigationMethod.Tab : NavigationMethod.Unspecified);
    }

    /// <summary>
    /// Follows the action chosen from a menu that the keys opened on <paramref name="opener"/> (the
    /// element that had the focus as it opened), which may remove or hide <paramref name="opener"/>
    /// after the menu's own return has given it the focus back; with the frame when it showed it.
    /// </summary>
    public static void AfterKeyboardMenu(InputElement opener)
    {
        ArgumentNullException.ThrowIfNull(opener);
        Follow(opener, null, opener.Classes.Contains(FocusVisibleClass) ? NavigationMethod.Tab : NavigationMethod.Unspecified);
    }

    /// <summary>
    /// Names what takes the focus, by <paramref name="method"/>, when an action by the keys leaves
    /// <paramref name="pressed"/> hidden or disabled: the control that stands in its place (the
    /// clone form's Clone for its Cancel clone) or that opened what it closed (the welcome page's
    /// Clone for the form's Cancel). Call once, when the view is made.
    /// </summary>
    public static void Instead(InputElement pressed, Func<InputElement?> target, NavigationMethod method = NavigationMethod.Tab)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(target);
        InPlaces.AddOrUpdate(pressed, new InPlace(target, method));
    }

    private static void Follow(InputElement pressed, ICommand? command, NavigationMethod method)
    {
        if (TopLevel.GetTopLevel(pressed) is not { } topLevel || InMenu(pressed))
        {
            return;
        }

        if (Followed.TryGetValue(pressed, out var earlier))
        {
            earlier.Stop();
        }

        var action = new ActionByKeys(pressed, topLevel, method);
        Followed.AddOrUpdate(pressed, action);
        if (command is IAsyncRelayCommand run)
        {
            action.FollowNextRun(run);
        }
        else
        {
            action.WatchLoss();
        }
    }

    /// <summary>Whether <paramref name="element"/> lies in a context menu.</summary>
    private static bool InMenu(IInputElement? element) =>
        element is MenuItem || (element is Visual visual && visual.GetSelfAndVisualAncestors().Any(ancestor => ancestor is MenuFlyoutPresenter));

    /// <summary>The element that can take the focus: shown, enabled and in the window.</summary>
    private static bool CanTake(InputElement? element) =>
        element is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } && element.IsAttachedToVisualTree();

    private sealed record InPlace(Func<InputElement?> Target, NavigationMethod Method);

    /// <summary>One action by the keys on an element, followed until the focus is placed or found elsewhere.</summary>
    private sealed class ActionByKeys
    {
        private readonly InputElement _pressed;
        private readonly TopLevel _topLevel;
        private readonly NavigationMethod _method;
        private readonly object? _data;
        private readonly object? _tab;
        private readonly Control? _root;
        private readonly DialogView? _dialog;
        private readonly ItemsControl? _list;
        private readonly int _place;
        private bool _stopped;

        /// <summary>Whether the look follows the end of the action's run, after which a disabled element is disabled for good (Undo with nothing left to undo).</summary>
        private bool _afterRun;

        public ActionByKeys(InputElement pressed, TopLevel topLevel, NavigationMethod method)
        {
            _pressed = pressed;
            _topLevel = topLevel;
            _method = method;
            _data = pressed.DataContext;
            _tab = (topLevel.DataContext as MainWindowViewModel)?.ActiveTab;
            _root = KeyRegion.RootOf(pressed);
            _dialog = pressed.GetSelfAndVisualAncestors().OfType<DialogView>().FirstOrDefault();
            (_list, _place) = PlaceOf(pressed, (Visual?)_dialog ?? _root);
        }

        public void Stop()
        {
            _stopped = true;
            _pressed.LostFocus -= OnLostFocus;
            _pressed.PropertyChanged -= OnEnabledChanged;
            _pressed.DetachedFromVisualTree -= OnDetached;
        }

        /// <summary>The run the press starts (the command's next execution, which the click comes before): when it ends, a look.</summary>
        public void FollowNextRun(IAsyncRelayCommand command)
        {
            var started = false;
            void OnChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IAsyncRelayCommand.ExecutionTask) && command.ExecutionTask is { } run)
                {
                    started = true;
                    command.PropertyChanged -= OnChanged;
                    AfterRun(run);
                }
            }

            command.PropertyChanged += OnChanged;

            // A command that did not start leaves nothing waiting.
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

        /// <summary>The next loss of the focus by the element: a look, once a run of its own that is going on has ended.</summary>
        public void WatchLoss()
        {
            _pressed.LostFocus -= OnLostFocus;
            if (!_stopped)
            {
                _pressed.LostFocus += OnLostFocus;
            }
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, _pressed))
            {
                return;
            }

            _pressed.LostFocus -= OnLostFocus;
            if (_pressed is Button { Command: IAsyncRelayCommand { IsRunning: true, ExecutionTask: { } running } })
            {
                AfterRun(running);
            }
            else
            {
                LookLater();
            }
        }

        private void AfterRun(Task run) =>
            run.ContinueWith(
                _ => Dispatcher.UIThread.Post(() =>
                {
                    _afterRun = true;
                    LookLater();
                }),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        /// <summary>
        /// The element, pressed without a run of its own to wait for, disabled itself until
        /// something else ends (a create's Cancel until the create has stopped): a look once it is
        /// enabled again or leaves the window.
        /// </summary>
        private void WaitUntilEnabled()
        {
            _pressed.PropertyChanged -= OnEnabledChanged;
            _pressed.DetachedFromVisualTree -= OnDetached;
            _pressed.PropertyChanged += OnEnabledChanged;
            _pressed.DetachedFromVisualTree += OnDetached;
        }

        private void OnEnabledChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == InputElement.IsEffectivelyEnabledProperty && _pressed.IsEffectivelyEnabled)
            {
                OnDetached(sender, EventArgs.Empty);
            }
        }

        private void OnDetached(object? sender, EventArgs e)
        {
            _pressed.PropertyChanged -= OnEnabledChanged;
            _pressed.DetachedFromVisualTree -= OnDetached;
            LookLater();
        }

        /// <summary>
        /// A look once the layout has followed, after what the views ask for themselves at the same
        /// priority (a row's return, the first-run screen's search, a page's list), so that theirs
        /// comes first.
        /// </summary>
        private void LookLater() =>
            Dispatcher.UIThread.Post(() => Dispatcher.UIThread.Post(Look, DispatcherPriority.Loaded), DispatcherPriority.Loaded);

        private void Look()
        {
            // A dialog the action closed leaves the return to D104; a switch of tabs away from an
            // element in a region leaves it to the window's own (the tab strip, the toolbar and the
            // status bar are in no region, and the switch does not place their focus).
            if (_stopped
                || (_dialog is not null && !_dialog.IsAttachedToVisualTree())
                || (_root is not null && _tab is not null && !ReferenceEquals(_tab, (_topLevel.DataContext as MainWindowViewModel)?.ActiveTab)))
            {
                return;
            }

            var focused = _topLevel.FocusManager?.GetFocusedElement();
            if (!LostFrom(_topLevel, _pressed, StillThere)(focused))
            {
                // Still on the element, or in a menu or a dialog that gives it back: its next loss is looked at too.
                if (ReferenceEquals(focused, _pressed) || InMenu(focused) || InAnotherDialog(focused))
                {
                    WatchLoss();
                }

                return;
            }

            if (!_afterRun && _pressed.IsAttachedToVisualTree() && _pressed.IsEffectivelyVisible && !_pressed.IsEffectivelyEnabled
                && ReferenceEquals(_pressed.DataContext, _data))
            {
                WaitUntilEnabled();
                return;
            }

            if (Target() is ({ } target, var method))
            {
                target.Focus(method);
            }
        }

        /// <summary>Whether the element stands where it was pressed: in the window, shown, enabled, with its data.</summary>
        private bool StillThere() => CanTake(_pressed) && ReferenceEquals(_pressed.DataContext, _data);

        private bool InAnotherDialog(IInputElement? focused) =>
            focused is Visual visual && visual.GetSelfAndVisualAncestors().OfType<DialogView>().FirstOrDefault() is { } dialog && !ReferenceEquals(dialog, _dialog);

        /// <summary>Where the focus goes (the class comment's order), and how.</summary>
        private (InputElement Target, NavigationMethod Method)? Target()
        {
            if (StillThere())
            {
                return (_pressed, _method);
            }

            if (InPlaces.TryGetValue(_pressed, out var inPlace) && inPlace.Target() is { } instead && CanTake(instead))
            {
                return (instead, inPlace.Method);
            }

            if (ItemAtItsPlace() is { } item)
            {
                return (item, _method);
            }

            var region = _dialog is not null
                ? RowOrderStackPanel.StopsOf(_dialog).FirstOrDefault()
                : _root is { } root && root.IsAttachedToVisualTree() && root.IsEffectivelyVisible
                    ? KeyRegionNavigator.MarkedTargetIn(KeyRegion.GetRole(root), root)
                    : null;
            if (CanTake(region))
            {
                return (region!, _method);
            }

            var graphArea = _dialog is null
                ? _topLevel.GetVisualDescendants().OfType<CommitGraphView>().FirstOrDefault(view => view.IsEffectivelyVisible)?.GraphAreaFocusTarget()
                : null;
            return CanTake(graphArea) ? (graphArea!, _method) : null;
        }

        /// <summary>The first stop of the item now at the element's place in its list, when it was an item's.</summary>
        private InputElement? ItemAtItsPlace()
        {
            if (_list is not { } list || !list.IsAttachedToVisualTree() || !list.IsEffectivelyVisible || list.ItemCount == 0)
            {
                return null;
            }

            var index = Math.Min(_place, list.ItemCount - 1);
            list.ScrollIntoView(index);
            return list.ContainerFromIndex(index) is { } container ? RowOrderStackPanel.StopsOf(container).FirstOrDefault() : null;
        }

        /// <summary>The list whose item holds <paramref name="pressed"/>, below <paramref name="within"/>, and the item's index; none for an element in no list.</summary>
        private static (ItemsControl? List, int Place) PlaceOf(InputElement pressed, Visual? within)
        {
            foreach (var ancestor in pressed.GetSelfAndVisualAncestors().OfType<Control>())
            {
                if (ReferenceEquals(ancestor, within))
                {
                    break;
                }

                if (ancestor.GetVisualAncestors().OfType<ItemsControl>().FirstOrDefault() is { } list && list.IndexFromContainer(ancestor) is >= 0 and var index)
                {
                    return (list, index);
                }
            }

            return (null, -1);
        }
    }
}
