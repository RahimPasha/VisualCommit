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
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views;

/// <summary>
/// The window's rule for an action by the keys (D462): the app never takes the keyboard focus away
/// without putting it somewhere. A button pressed by the keys (it is focused and shows the keyboard's
/// frame at its Click: Enter or Space after Tab or the arrows; a click on a button that shows the
/// frame from before counts so too), a dialog's confirm run by Enter in one of its boxes, a menu
/// opened by the keys on an element, and a key of the window's (a shortcut) pressed while the
/// focused element shows the frame: when the action has left the focus on nothing, on the window or
/// on an element no longer shown or enabled (Avalonia gives the focus to nothing when the focused
/// element is hidden, removed or disabled), the focus goes, once the action's run has ended and the
/// layout has followed, to the first of these that can take it:
/// <list type="number">
/// <item>the element itself, shown and enabled again with the data it was pressed for;</item>
/// <item>what its view named to stand in its place (<see cref="Instead"/>);</item>
/// <item>the item now at its place in the list it lay in (its first stop), when it was an item's;</item>
/// <item>in a dialog, what the dialog gives the focus to as it opens (the settings screen's shown
/// section's button), else its first stop; elsewhere the target of the region it lay in, as F6
/// gives it (<see cref="KeyRegionNavigator.MarkedTargetIn"/>);</item>
/// <item>the graph area's target, for an element in no region (the tab strip, the toolbar, the status bar).</item>
/// </list>
/// The focus comes with the keyboard's frame when the element had it; a text box given the focus
/// back keeps the caret and selection it had at the press, and a dialog's opening box gets them as
/// on opening. Views that place the focus themselves after such an action (a stage row's buttons,
/// the first-run screen, the settings screen's Git program row, a dialog's own return, D104) do so
/// first: this rule looks only after them and acts only when the focus is still lost. A click on a
/// button without the frame arms nothing, and a press of the mouse on an element ends what the keys
/// armed on it, so the mouse's path stays as it was. A press is dropped once the focus moves, after
/// it, to another element that stands, outside a menu and outside a dialog over the element's place
/// (the user's Tab or click, or a view's own placement), so a focus the user moved meanwhile stays,
/// also when the user's own later action loses it. A dialog the action closed leaves the return to
/// D104 and to its opener's own action; a dialog that stands over the element's place (one opened
/// over the window, or over the element's own dialog) is waited for, and the look comes once it has
/// closed, so that no focus goes behind it; after a switch of tabs the window places the focus first
/// (<c>MainWindow.FocusAfterSwitch</c>), and a focus still on nothing then, as when the action had
/// taken it before the switch, goes to the graph area's target of the tab now shown; and while the
/// window is inactive no look is made: the platform's taking of the focus from a window it
/// deactivates is no loss of an action's, and the look comes once the window is active again and
/// Avalonia has given back the focus it remembered (<see cref="WhenActive"/>). While the action's run
/// lasts, a pressed element its run disables hands the focus to what its view named to stand in its
/// place for that time, when that shows (the clone form's Cancel clone beside its disabled Clone). The
/// rule holds no tab or data it was pressed for: a closed tab goes.
/// </summary>
public static partial class FocusReturn
{
    /// <summary>The pseudo-class of an element that shows the keyboard's frame (its focus came by Tab or the arrows).</summary>
    private const string FocusVisibleClass = ":focus-visible";

    /// <summary>The press by the keys (a click, a dialog's Enter, a menu) each element is followed for now: a newer one replaces it.</summary>
    private static readonly ConditionalWeakTable<InputElement, ActionByKeys> Followed = new();

    /// <summary>
    /// The key of the window's (<see cref="AfterKeyboardShortcut"/>) each element is followed for now,
    /// beside its press in <see cref="Followed"/>: a newer key on the element replaces it, so a key held
    /// or repeated arms one follower.
    /// </summary>
    private static readonly ConditionalWeakTable<InputElement, ActionByKeys> FollowedByKey = new();

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
                    Follow(button, button.Command, NavigationMethod.Tab, Followed);
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
        Follow(pressed, command, pressed.Classes.Contains(FocusVisibleClass) ? NavigationMethod.Tab : NavigationMethod.Unspecified, Followed);
    }

    /// <summary>
    /// Follows the action chosen from a menu that the keys opened on <paramref name="opener"/> (the
    /// element that had the focus as it opened), which may remove or hide <paramref name="opener"/>
    /// after the menu's own return has given it the focus back; with the frame when it showed it.
    /// </summary>
    public static void AfterKeyboardMenu(InputElement opener)
    {
        ArgumentNullException.ThrowIfNull(opener);
        Follow(opener, null, opener.Classes.Contains(FocusVisibleClass) ? NavigationMethod.Tab : NavigationMethod.Unspecified, Followed);
    }

    /// <summary>
    /// Follows a key of the window's (a shortcut its router runs: Stage all, Close tab, Undo, …)
    /// pressed while <paramref name="focused"/> has the focus, when it shows the keyboard's frame:
    /// a key that takes the focused element away (its row staged, its tab closed) leaves the focus
    /// where a press by the keys on it would. Call it just before the key's command runs. A focus
    /// without the frame (a click's) arms nothing, as a click does not.
    /// </summary>
    public static void AfterKeyboardShortcut(IInputElement? focused)
    {
        if (focused is InputElement element && element.Classes.Contains(FocusVisibleClass))
        {
            // Beside an action already followed on the element (a press whose run still lasts), not
            // in its place; in the place of the key before on the same element, which, the element
            // having kept the focus, has nothing pending but its watch of the element's next loss.
            Follow(element, null, NavigationMethod.Tab, FollowedByKey);
        }
    }

    /// <summary>
    /// Names what takes the focus, by <paramref name="method"/>, when an action by the keys leaves
    /// <paramref name="pressed"/> hidden or disabled: the control that stands in its place (the
    /// clone form's Clone for its Cancel clone) or that opened what it closed (the welcome page's
    /// Clone for the form's Cancel), second in the class comment's order once the action's run has
    /// ended. With <paramref name="whileRunning"/>, it also takes a focus that the pressed element's
    /// own run takes as it disables the element, while that run lasts (Cancel clone beside Clone).
    /// Call once, when the view is made.
    /// </summary>
    public static void Instead(InputElement pressed, Func<InputElement?> target, NavigationMethod method = NavigationMethod.Tab, bool whileRunning = false)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        ArgumentNullException.ThrowIfNull(target);
        InPlaces.AddOrUpdate(pressed, new InPlace(target, method, whileRunning));
    }

    private static void Follow(InputElement pressed, ICommand? command, NavigationMethod method, ConditionalWeakTable<InputElement, ActionByKeys> table)
    {
        if (TopLevel.GetTopLevel(pressed) is not { } topLevel || InMenu(pressed))
        {
            return;
        }

        if (table.TryGetValue(pressed, out var earlier))
        {
            earlier.Stop();
        }

        var action = new ActionByKeys(pressed, topLevel, method, table);
        table.AddOrUpdate(pressed, action);
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

    private sealed record InPlace(Func<InputElement?> Target, NavigationMethod Method, bool WhileRunning);

    /// <summary>One action by the keys on an element, followed until the focus is placed or found elsewhere.</summary>
    private sealed class ActionByKeys
    {
        private readonly InputElement _pressed;
        private readonly TopLevel _topLevel;
        private readonly NavigationMethod _method;
        private readonly ConditionalWeakTable<InputElement, ActionByKeys> _table;

        /// <summary>The element's data at the press, held weakly: the action keeps no closed tab's view models (D462).</summary>
        private readonly WeakReference<object>? _data;

        /// <summary>The active tab at the press, held weakly for the same reason.</summary>
        private readonly WeakReference<object>? _tab;

        private readonly Control? _root;
        private readonly DialogView? _dialog;
        private ItemsControl? _list;
        private int _place;

        /// <summary>The pressed text box's selection and caret as the action armed or last found it focused: a single-line box focused by Tab selects all it holds, and the look gives them back.</summary>
        private (int Start, int End, int Caret)? _text;

        private bool _stopped;

        /// <summary>Whether the rule is placing the focus itself, so that the move it makes does not end the action.</summary>
        private bool _placing;

        /// <summary>Whether the look follows the end of the action's run, after which a disabled element is disabled for good (Undo with nothing left to undo).</summary>
        private bool _afterRun;

        /// <summary>Whether a look found the active tab changed and came again one round later, after the window's own placement.</summary>
        private bool _afterSwitch;

        /// <summary>The window's dialogs while a look waits for those that stand over the element's place to close.</summary>
        private DialogHostViewModel? _waitingOn;

        /// <summary>Whether a loss while the run lasts is handed to what stands in the element's place for that time (<see cref="Instead"/>).</summary>
        private bool _watchingRun;

        public ActionByKeys(InputElement pressed, TopLevel topLevel, NavigationMethod method, ConditionalWeakTable<InputElement, ActionByKeys> table)
        {
            _pressed = pressed;
            _topLevel = topLevel;
            _method = method;
            _table = table;
            _data = pressed.DataContext is { } data ? new WeakReference<object>(data) : null;
            _tab = (topLevel.DataContext as MainWindowViewModel)?.ActiveTab is { } tab ? new WeakReference<object>(tab) : null;
            _root = KeyRegion.RootOf(pressed);
            _dialog = pressed.GetSelfAndVisualAncestors().OfType<DialogView>().FirstOrDefault();
            (_list, _place) = PlaceOf(pressed, (Visual?)_dialog ?? _root);
            _text = TextOf(pressed);
            topLevel.AddHandler(InputElement.GotFocusEvent, OnFocusMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        public void Stop()
        {
            _stopped = true;
            StopWatchingLoss();
            _pressed.PropertyChanged -= OnEnabledChanged;
            _pressed.DetachedFromVisualTree -= OnDetached;
            _topLevel.RemoveHandler(InputElement.GotFocusEvent, OnFocusMoved);
            StopWatchingRun();
            StopWaitingForDialogs();
            if (_table.TryGetValue(_pressed, out var current) && ReferenceEquals(current, this))
            {
                _table.Remove(_pressed);
            }
        }

        /// <summary>
        /// The run the press starts (the command's next execution, which the click comes before): when
        /// it ends, a look. Meanwhile, for an element whose view named what stands in its place while
        /// its run lasts, a loss of the focus (the run disabling the element as it starts) hands it there.
        /// </summary>
        public void FollowNextRun(IAsyncRelayCommand command)
        {
            if (InPlaces.TryGetValue(_pressed, out var inPlace) && inPlace.WhileRunning)
            {
                _watchingRun = true;
                _pressed.LostFocus += OnLostDuringRun;
            }

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
                        Stop();
                    }
                },
                DispatcherPriority.Loaded);
        }

        private void StopWatchingRun()
        {
            _watchingRun = false;
            _pressed.LostFocus -= OnLostDuringRun;
        }

        /// <summary>The element lost the focus while its run lasts: a look, once the layout shows what stands in its place.</summary>
        private void OnLostDuringRun(object? sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(e.Source, _pressed))
            {
                Dispatcher.UIThread.Post(
                    () => Dispatcher.UIThread.Post(() => WhenActive(_topLevel, LookDuringRun), DispatcherPriority.Loaded),
                    DispatcherPriority.Loaded);
            }
        }

        /// <summary>While the run lasts, a focus still lost goes to what stands in the element's place, when it can take it; the look at the run's end does the rest.</summary>
        private void LookDuringRun()
        {
            if (!_watchingRun || _stopped || _afterRun || DialogsAbove() is not null
                || !LostFrom(_topLevel, _pressed, StillThere)(_topLevel.FocusManager?.GetFocusedElement()))
            {
                return;
            }

            if (InPlaces.TryGetValue(_pressed, out var inPlace) && inPlace.WhileRunning && inPlace.Target() is { } instead && CanTake(instead))
            {
                Place(instead, inPlace.Method);
            }
        }

        /// <summary>
        /// The next loss of the focus by the element: a look, once a run of its own that is going on
        /// has ended. Meanwhile a press of the mouse on the element (a right-click's menu, a click
        /// without the frame) starts an action of the mouse's, and what the keys armed ends.
        /// </summary>
        public void WatchLoss()
        {
            StopWatchingLoss();
            if (!_stopped)
            {
                _pressed.LostFocus += OnLostFocus;
                _pressed.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }

        private void StopWatchingLoss()
        {
            _pressed.LostFocus -= OnLostFocus;
            _pressed.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => Stop();

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, _pressed))
            {
                return;
            }

            StopWatchingLoss();
            if (_pressed is Button { Command: IAsyncRelayCommand { IsRunning: true, ExecutionTask: { } running } })
            {
                AfterRun(running);
            }
            else
            {
                LookLater();
            }
        }

        /// <summary>
        /// The focus moved, after the press, to another element that stands (the user's Tab or click,
        /// or a view's own placement): the press no longer holds it, and its look is dropped. A move
        /// into a menu, into a dialog over the element's place, back to the element, or to nothing that
        /// stands (the window, an element hidden or disabled) keeps it, as does the rule's own placing.
        /// </summary>
        private void OnFocusMoved(object? sender, FocusChangedEventArgs e)
        {
            if (_placing || _stopped || e.Source is not InputElement source || ReferenceEquals(source, _pressed) || InMenu(source)
                || LostFrom(_topLevel, _pressed, static () => true)(source)
                || (DialogsAbove() is not null && !ReferenceEquals(source.GetSelfAndVisualAncestors().OfType<DialogView>().FirstOrDefault(), _dialog)))
            {
                return;
            }

            Stop();
        }

        private void AfterRun(Task run) =>
            run.ContinueWith(
                _ => Dispatcher.UIThread.Post(() =>
                {
                    _afterRun = true;
                    StopWatchingRun();
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
        /// comes first; and once the window is active (<see cref="WhenActive"/>).
        /// </summary>
        private void LookLater() =>
            Dispatcher.UIThread.Post(
                () => Dispatcher.UIThread.Post(() => WhenActive(_topLevel, Look), DispatcherPriority.Loaded),
                DispatcherPriority.Loaded);

        private void Look()
        {
            if (_stopped)
            {
                return;
            }

            // A dialog the action closed leaves the return to D104.
            if (_dialog is not null && !_dialog.IsAttachedToVisualTree())
            {
                Stop();
                return;
            }

            // A dialog over the element's place: the look comes once it has closed (and D104 has
            // given back what it took), never behind it.
            if (DialogsAbove() is { } dialogs)
            {
                WaitForDialogs(dialogs);
                return;
            }

            var focused = _topLevel.FocusManager?.GetFocusedElement();
            if (_root is not null && TabChanged())
            {
                // A switch of tabs away from an element in a region: the window's own placement
                // comes first, a round later than the switch; a focus it found on nothing already
                // (the action had taken it before the switch) goes to the graph area's target of the
                // tab now shown. The tab strip, the toolbar and the status bar are in no region:
                // their elements are looked at as below.
                if (!_afterSwitch)
                {
                    _afterSwitch = true;
                    LookLater();
                    return;
                }

                if (LostFrom(_topLevel, _pressed, static () => true)(focused) && GraphAreaTarget() is { } graph && CanTake(graph))
                {
                    Place(graph, _method);
                }

                Stop();
                return;
            }

            if (!LostFrom(_topLevel, _pressed, StillThere)(focused))
            {
                // Still on the element, or in a menu that gives it back: its next loss is looked at
                // too, from its place and text as they are now.
                if (ReferenceEquals(focused, _pressed) || InMenu(focused))
                {
                    if (_pressed.IsAttachedToVisualTree())
                    {
                        (_list, _place) = PlaceOf(_pressed, (Visual?)_dialog ?? _root);
                    }

                    if (ReferenceEquals(focused, _pressed))
                    {
                        _text = TextOf(_pressed);
                    }

                    WatchLoss();
                }
                else
                {
                    Stop();
                }

                return;
            }

            if (!_afterRun && _pressed.IsAttachedToVisualTree() && _pressed.IsEffectivelyVisible && !_pressed.IsEffectivelyEnabled && HasItsData())
            {
                WaitUntilEnabled();
                return;
            }

            if (Target() is ({ } target, var method))
            {
                Place(target, method);
            }

            Stop();
        }

        /// <summary>Gives <paramref name="target"/> the focus by <paramref name="method"/>, as the rule's own move, and a text box its caret and selection back (<see cref="KeepText"/>).</summary>
        private void Place(InputElement target, NavigationMethod method)
        {
            _placing = true;
            try
            {
                target.Focus(method);
                KeepText(target);
            }
            finally
            {
                _placing = false;
            }
        }

        /// <summary>
        /// A text box the rule focused: the pressed box gets back the selection and caret it had as
        /// the action armed (a single-line box focused by Tab selects all it holds); a dialog's opening
        /// box gets them as on opening. A region's mark keeps what F6 gives it.
        /// </summary>
        private void KeepText(InputElement target)
        {
            if (target is not TextBox box)
            {
                return;
            }

            var length = box.Text?.Length ?? 0;
            if (ReferenceEquals(box, _pressed) && _text is { } text)
            {
                var (start, end, caret) = text;
                if (start == end)
                {
                    box.ClearSelection();
                    box.CaretIndex = Math.Min(caret, length);
                }
                else
                {
                    // The anchor first: a selection made backwards keeps its direction.
                    box.SelectionStart = Math.Min(start, length);
                    box.SelectionEnd = Math.Min(end, length);
                }
            }
            else if (_dialog is not null && ReferenceEquals(box, _dialog.OpeningFocus()))
            {
                _dialog.PlaceCaretAsOnOpening(box);
            }
        }

        /// <summary>Whether the element stands where it was pressed: in the window, shown, enabled, with its data.</summary>
        private bool StillThere() => CanTake(_pressed) && HasItsData();

        /// <summary>Whether the element still has the data it was pressed for (data that has been let go it cannot have).</summary>
        private bool HasItsData() =>
            _data is null
                ? _pressed.DataContext is null
                : _data.TryGetTarget(out var data) && ReferenceEquals(_pressed.DataContext, data);

        /// <summary>Whether the active tab is another than the one of the press (a closed tab that has been let go is not the active one).</summary>
        private bool TabChanged() =>
            _tab is not null
            && !(_tab.TryGetTarget(out var tab) && ReferenceEquals(tab, (_topLevel.DataContext as MainWindowViewModel)?.ActiveTab));

        /// <summary>The window's dialogs when one stands over the element's place: any, for an element outside them; one above its own, for an element in a dialog. Null otherwise.</summary>
        private DialogHostViewModel? DialogsAbove() =>
            _topLevel.DataContext is MainWindowViewModel { Dialogs: { IsOpen: true } dialogs }
                && !(_dialog is not null && ReferenceEquals(_dialog.DataContext, dialogs.Current))
                ? dialogs
                : null;

        /// <summary>A look again when the dialogs change (one closes, or another opens over them), until none stands over the element's place.</summary>
        private void WaitForDialogs(DialogHostViewModel dialogs)
        {
            StopWaitingForDialogs();
            _waitingOn = dialogs;
            dialogs.PropertyChanged += OnDialogsChanged;
        }

        private void StopWaitingForDialogs()
        {
            if (_waitingOn is { } dialogs)
            {
                dialogs.PropertyChanged -= OnDialogsChanged;
            }

            _waitingOn = null;
        }

        private void OnDialogsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DialogHostViewModel.Current))
            {
                StopWaitingForDialogs();
                LookLater();
            }
        }

        /// <summary>The graph area's target of the tab shown (the graph, the welcome page's Open, the dashboard's list).</summary>
        private InputElement? GraphAreaTarget() =>
            _topLevel.GetVisualDescendants().OfType<CommitGraphView>().FirstOrDefault(view => view.IsEffectivelyVisible)?.GraphAreaFocusTarget();

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
                ? (_dialog.OpeningFocus() is InputElement opening && CanTake(opening) ? opening : RowOrderStackPanel.StopsOf(_dialog).FirstOrDefault())
                : _root is { } root && root.IsAttachedToVisualTree() && root.IsEffectivelyVisible
                    ? KeyRegionNavigator.MarkedTargetIn(KeyRegion.GetRole(root), root)
                    : null;
            if (CanTake(region))
            {
                return (region!, _method);
            }

            var graphArea = _dialog is null ? GraphAreaTarget() : null;
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

        /// <summary>A text box's selection and caret; none for another element.</summary>
        private static (int Start, int End, int Caret)? TextOf(InputElement element) =>
            element is TextBox box ? (box.SelectionStart, box.SelectionEnd, box.CaretIndex) : null;

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
