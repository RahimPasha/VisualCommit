using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// What the views share when they turn Avalonia's context request into a menu of the active
/// repository (docs/phase-3-design.md 4a): the window's menu layer and the repository, and where
/// the menu opens and how it was asked for.
/// </summary>
internal static class ContextMenuRequests
{
    /// <summary>The window's menu layer and its active tab's repository, or null outside a window or without a repository.</summary>
    public static (MenuLayerViewModel Layer, RepositoryViewModel Repository)? ActiveRepository(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        return TopLevel.GetTopLevel(visual) is { DataContext: MainWindowViewModel shell } && shell.ActiveTab.Repository is { } repository
            ? (shell.Menu, repository)
            : null;
    }

    /// <summary>The window's menu layer, or null outside a window.</summary>
    public static MenuLayerViewModel? Layer(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        return TopLevel.GetTopLevel(visual) is { DataContext: MainWindowViewModel shell } ? shell.Menu : null;
    }

    /// <summary>
    /// Where a menu asked for by <paramref name="e"/> opens, in the window's coordinates (4a): at
    /// the pointer for a right-click; for the Menu key or Shift+F10, which carry no position, at
    /// the bottom-left corner of <paramref name="row"/>, the focused row.
    /// </summary>
    public static (MenuPosition Position, MenuOpenedBy OpenedBy) Where(ContextRequestedEventArgs e, Visual row)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(row);
        var topLevel = TopLevel.GetTopLevel(row);
        if (topLevel is not null && e.TryGetPosition(topLevel, out var pointer))
        {
            return (new MenuPosition(pointer.X, pointer.Y), MenuOpenedBy.Pointer);
        }

        var corner = topLevel is null ? default : row.TranslatePoint(new Point(0, row.Bounds.Height), topLevel) ?? default;
        return (new MenuPosition(corner.X, corner.Y), MenuOpenedBy.Keyboard);
    }

    /// <summary>The nearest of <paramref name="source"/> and its visual ancestors that is a <typeparamref name="T"/> and matches <paramref name="match"/>, or null.</summary>
    public static T? Find<T>(object? source, Func<T, bool>? match = null)
        where T : Visual
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is T found && (match is null || match(found)))
            {
                return found;
            }
        }

        return null;
    }
}
