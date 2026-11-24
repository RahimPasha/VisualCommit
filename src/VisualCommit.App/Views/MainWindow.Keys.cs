using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views;

// The window's keys (docs/phase-6-design.md 6.2, 6.3, D192, D213, D214): the router (KeyRouter)
// on the window's two passes, and what the window does for a key or a palette entry that moves
// the focus (IWindowKeyActions): the commit message, the filter, F6's regions.
public partial class MainWindow : IWindowKeyActions
{
    /// <summary>The pseudo-class of an element that shows the keyboard's frame (its focus came by Tab or the arrows).</summary>
    private const string FocusVisibleClass = ":focus-visible";

    private readonly KeyRegionNavigator _regions = new();
    private KeyRouter? _router;
    private bool _focusFollowed;
    private MainWindowViewModel? _shell;

    /// <summary>Where the focus lay as the active tab began to change: its region, that region's root (the panel) and whether it showed the keyboard's frame; null outside every region.</summary>
    private (KeyRegionRole Role, Control Root, bool Framed)? _focusAtSwitch;

    /// <summary>The window's key router (6.3).</summary>
    private KeyRouter Router => _router ??= new KeyRouter(this);

    /// <summary>Focuses <c>CommitSummary</c> once the stage panel is laid out (6.3: the working-changes row is selected first).</summary>
    public void FocusCommitMessage() =>
        Dispatcher.UIThread.Post(
            () => VisibleTextBox("CommitSummary")?.Focus(NavigationMethod.Unspecified),
            DispatcherPriority.Loaded);

    /// <summary>Focuses <c>FilterBox</c>, its text selected (6.1's row 33).</summary>
    public void FocusFilter()
    {
        if (VisibleTextBox("FilterBox") is { } filter && filter.Focus(NavigationMethod.Tab))
        {
            filter.SelectAll();
        }
    }

    /// <summary>F6 and Shift+F6 (6.6).</summary>
    public void MoveFocusToRegion(bool backwards) => _regions.Move(this, FocusManager?.GetFocusedElement(), backwards);

    /// <summary>
    /// The window's data context is set (once, by the session): the window's actions are its
    /// view model's (6.3), from then on each region remembers its last focused element (6.6), and a
    /// press by the keys that takes the focus away puts it somewhere (D462).
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (!_focusFollowed)
        {
            _focusFollowed = true;
            AddHandler(GotFocusEvent, (_, args) => _regions.Remember(args.Source), RoutingStrategies.Bubble, handledEventsToo: true);

            // A press by the keys that takes the focus away puts it somewhere (D462).
            FocusReturn.FollowKeyboardPresses(this);
        }

        if (DataContext is MainWindowViewModel shell)
        {
            shell.WindowActions = this;
        }

        FollowShell(DataContext as MainWindowViewModel);
    }

    private void FollowShell(MainWindowViewModel? shell)
    {
        if (ReferenceEquals(shell, _shell))
        {
            return;
        }

        if (_shell is not null)
        {
            _shell.PropertyChanging -= OnShellChanging;
            _shell.PropertyChanged -= OnShellChanged;
        }

        _shell = shell;
        _focusAtSwitch = null;
        if (shell is not null)
        {
            shell.PropertyChanging += OnShellChanging;
            shell.PropertyChanged += OnShellChanged;
        }
    }

    /// <summary>
    /// The active tab is about to change: where the focus lies now, outside a menu (as a dialog
    /// keeps it, D104). The panels are the tabs' shared views, and the switch gives them the other
    /// tab's data: a focused row of a list leaves the window with the list's rows, a panel the
    /// other tab does not show hides with its focused element, and the focus is then on nothing.
    /// </summary>
    private void OnShellChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.ActiveTab))
        {
            return;
        }

        var focused = MenuLayer.FocusOutsideMenus(FocusManager?.GetFocusedElement()) as Control;
        _focusAtSwitch = focused is not null && KeyRegion.RootOf(focused) is { } root
            ? (KeyRegion.GetRole(root), root, focused.Classes.Contains(FocusVisibleClass))
            : null;
    }

    /// <summary>
    /// The active tab has changed while the focus lay in a region: once the layout shows the new
    /// tab, and after what its views ask for themselves at the same priority (a dashboard's list, an
    /// open diff's text), a focus the switch took away goes to that region (<see cref="FocusAfterSwitch"/>).
    /// </summary>
    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.ActiveTab) || _focusAtSwitch is not { } before)
        {
            return;
        }

        _focusAtSwitch = null;
        Dispatcher.UIThread.Post(
            () => Dispatcher.UIThread.Post(() => FocusAfterSwitch(before), DispatcherPriority.Loaded),
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// After a switch of tabs that took the focus away from a region (nothing, the window or an
    /// element no longer shown has it, and no dialog is open): the focus goes to that region's
    /// target in the tab now shown, as F6 would give it without its memory of the other tab's
    /// element (D213), when the tab shows the same panel with a repository of its own; else to the
    /// graph area's target. A tab without a repository (a welcome page, the first-run screen, a tab
    /// still opening) shows only the panels' empty stand-ins, so its graph area's target is taken
    /// there. It comes with the keyboard's frame when the element that lost it had the frame. A tab
    /// still opening has no graph area's target, and the focus stays where it is; a focus the user
    /// or a view has placed meanwhile stays too.
    /// </summary>
    private void FocusAfterSwitch((KeyRegionRole Role, Control Root, bool Framed) before)
    {
        var focused = FocusManager?.GetFocusedElement();
        if (_shell is not { Dialogs.IsOpen: false }
            || !(focused is null || ReferenceEquals(focused, this) || focused is Visual { IsEffectivelyVisible: false } || focused is InputElement { IsEffectivelyEnabled: false }))
        {
            return;
        }

        var target = before.Role != KeyRegionRole.GraphArea && _shell.ActiveTab.HasRepository
            && before.Root.IsEffectivelyVisible && before.Root.IsAttachedToVisualTree()
            ? KeyRegionNavigator.MarkedTargetIn(before.Role, before.Root)
            : null;
        target ??= this.GetVisualDescendants().OfType<CommitGraphView>().FirstOrDefault(view => view.IsEffectivelyVisible)?.GraphAreaFocusTarget();
        if (target is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
        {
            target.Focus(before.Framed ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
    }

    /// <summary>
    /// The window's bubbling pass (phase 5 design 1.3, D163), which the window's constructor adds:
    /// the toolbar's Undo and Redo by the platform's keys, after a text box or the conflict
    /// resolver had them first (<see cref="KeyRouter.OnBubble"/>).
    /// </summary>
    private void OnKeyBubble(object? sender, KeyEventArgs e) => Router.OnBubble(sender, e);

    /// <summary>
    /// The window's tunnelling pass, with <c>handledEventsToo</c>, which the window's constructor
    /// adds: the router's fixed set (<see cref="KeyRouter.OnTunnel"/>, 6.1, 6.3).
    /// </summary>
    private void OnKeyTunnel(object? sender, KeyEventArgs e) => Router.OnTunnel(sender, e);

    private TextBox? VisibleTextBox(string name) =>
        this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(box => box.Name == name && box.IsEffectivelyVisible && box.IsEffectivelyEnabled);
}
