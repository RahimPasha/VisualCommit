using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>How a menu was asked for, which decides where it opens (4a) and what the log says (D106).</summary>
public enum MenuOpenedBy
{
    /// <summary>A right-click: the menu opens 2 right of and 2 below the pointer.</summary>
    Pointer,

    /// <summary>Shift+F10 or the Menu key: the menu opens with its top-left at the given point (the focused row's bottom-left corner).</summary>
    Keyboard,

    /// <summary>
    /// A button's own menu (the dashboard's <c>DashboardMore</c>, docs/phase-6-design.md 2.5, WD10):
    /// the menu opens with its top-left at the given point (the button's bottom-left corner) and is
    /// never flipped to the point's other side, only moved to stay inside the window. Logged as a
    /// pointer's opening (a click).
    /// </summary>
    Button,
}

/// <summary>
/// A request for a menu that opens once a read completes (<see cref="MenuLayerViewModel.Request"/>):
/// whose menu it is and which request of the layer it was.
/// </summary>
public readonly record struct MenuRequest(object Owner, int Number);

/// <summary>
/// The window's menu layer (docs/phase-3-design.md 4a, D105): the one place a context menu is
/// opened, at a point of the window, and at most one menu open at a time. The view
/// (<c>Views/Shell/MenuLayer</c>) draws <see cref="Current"/> with Avalonia's stock menu flyout and
/// clears it through <see cref="Close"/> when the menu closes for any reason.
/// </summary>
public sealed partial class MenuLayerViewModel : ObservableObject
{
    private readonly IAppLog _log;
    private readonly Func<string?> _repositoryName;
    private readonly Func<object, bool> _mayOpenLater;
    private int _requests;

    /// <summary>A layer that logs nothing.</summary>
    public MenuLayerViewModel()
        : this(NullAppLog.Instance, () => null)
    {
    }

    /// <summary>
    /// A layer that logs every menu it opens at Debug as
    /// <c>&lt;repo&gt;: Context menu opened: &lt;header&gt; by pointer</c> (or <c>by keyboard</c>),
    /// the line the real-window pass counts (D106).
    /// </summary>
    /// <param name="log">Where to log.</param>
    /// <param name="repositoryName">The active tab's repository name, read at each open; null leaves the prefix out.</param>
    /// <param name="mayOpenLater">
    /// Whether a menu that waited for a read (<see cref="OpenRequested"/>) may still open for its
    /// owner: the window's says so while the owner is the active tab's repository and no dialog is
    /// open. Null: always.
    /// </param>
    public MenuLayerViewModel(IAppLog log, Func<string?> repositoryName, Func<object, bool>? mayOpenLater = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(repositoryName);
        _log = log;
        _repositoryName = repositoryName;
        _mayOpenLater = mayOpenLater ?? (_ => true);
    }

    /// <summary>The open menu, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial MenuModel? Current { get; private set; }

    /// <summary>Where the open menu opened, in the window's coordinates.</summary>
    [ObservableProperty]
    public partial MenuPosition Position { get; private set; }

    /// <summary>How the open menu was asked for.</summary>
    [ObservableProperty]
    public partial MenuOpenedBy OpenedBy { get; private set; }

    public bool IsOpen => Current is not null;

    /// <summary>Opens <paramref name="menu"/> at the pointer's <paramref name="position"/>, closing any other.</summary>
    public void Open(MenuModel menu, MenuPosition position) => Open(menu, position, MenuOpenedBy.Pointer);

    /// <summary>
    /// Opens <paramref name="menu"/> at <paramref name="position"/>, closing any other: for
    /// <see cref="MenuOpenedBy.Pointer"/> the pointer, for <see cref="MenuOpenedBy.Keyboard"/>
    /// the bottom-left corner of the focused row (4a).
    /// </summary>
    public void Open(MenuModel menu, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(menu);

        // A menu open already goes first, so that the view sees the new one as a menu of its own.
        Close();
        Position = position;
        OpenedBy = openedBy;
        Current = menu;

        var repository = _repositoryName();
        var how = openedBy == MenuOpenedBy.Keyboard ? "keyboard" : "pointer";
        var prefix = string.IsNullOrEmpty(repository) ? string.Empty : repository + ": ";
        _log.Debug($"{prefix}Context menu opened: {menu.HeaderText ?? "(no header)"} by {how}");
    }

    /// <summary>
    /// Asks for a menu of <paramref name="owner"/> (a repository) that opens only once a read
    /// completes (a drop's menu, a local branch's with its fast-forward): the result is what
    /// <see cref="OpenRequested"/> takes then. A later request, a menu opened, and
    /// <see cref="Close"/> (Esc, another tab, a dialog, a press outside a menu) each drop the
    /// requests made before them.
    /// </summary>
    public MenuRequest Request(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new MenuRequest(owner, ++_requests);
    }

    /// <summary>
    /// Opens <paramref name="menu"/> for <paramref name="request"/> once its read has completed,
    /// only if nothing has dropped the request since (<see cref="Request"/>) and its owner may
    /// still show a menu (its tab is active, no dialog is open). Returns whether it opened.
    /// </summary>
    public bool OpenRequested(MenuRequest request, MenuModel menu, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (request.Number != _requests || request.Owner is null || !_mayOpenLater(request.Owner))
        {
            return false;
        }

        Open(menu, position, openedBy);
        return true;
    }

    /// <summary>
    /// Closes the open menu and drops every menu request still waiting for its read. Returns
    /// whether a menu was open (Esc's chain goes on when none was).
    /// </summary>
    public bool Close()
    {
        _requests++;
        if (Current is null)
        {
            return false;
        }

        Current = null;
        return true;
    }
}
