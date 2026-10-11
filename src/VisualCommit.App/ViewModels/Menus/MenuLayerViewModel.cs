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
}

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
    public MenuLayerViewModel(IAppLog log, Func<string?> repositoryName)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(repositoryName);
        _log = log;
        _repositoryName = repositoryName;
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

    /// <summary>Closes the open menu. Returns whether one was open (Esc's chain).</summary>
    public bool Close()
    {
        if (Current is null)
        {
            return false;
        }

        Current = null;
        return true;
    }
}
