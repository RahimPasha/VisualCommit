using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 3's additions to the driver (docs/phase-3-design.md 7.4): clicks with modifier keys, a
/// drag in separate steps, the open dialog, typing into a box, waiting for the operation queue,
/// the fakes of the file launcher and the clipboard, and the open context menu (<c>Menu()</c>,
/// <c>ClickMenuItem</c>, <c>MenuItemBounds</c>, from the menu adapter's package WP5).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>Where the mouse was pressed or last moved with the button held, for the next held move to start from.</summary>
    private Point? _mousePosition;

    /// <summary>
    /// The app's file launcher: records "Open file" and "Show in folder" instead of opening
    /// anything (2.32).
    /// </summary>
    public FakeFileLauncher Launcher { get; }

    /// <summary>The app's clipboard: records every "Copy …" instead of changing the machine's clipboard (2.32).</summary>
    public FakeClipboard Clipboard { get; }

    /// <summary>
    /// The modifier that adds a row to a selection with a click (4h): the platform's command
    /// modifier as the headless platform reports it (<c>GetPlatformSettings().HotkeyConfiguration
    /// .CommandModifiers</c>; <c>TopLevel.PlatformSettings</c> is not public in Avalonia 12.1.3),
    /// never a test of the operating system, so that a Ctrl+click of a
    /// check is the platform's own gesture on every CI job. Control when the platform names none.
    /// </summary>
    public KeyModifiers CommandModifier =>
        Window.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers is { } modifiers && modifiers != KeyModifiers.None
            ? modifiers
            : KeyModifiers.Control;

    /// <summary>Moves the mouse to the centre of a control and clicks the left button with <paramref name="modifiers"/> held (Ctrl+click, Shift+click).</summary>
    public void Click(Visual visual, KeyModifiers modifiers) => Click(BoundsOf(visual).Center, modifiers);

    /// <summary>
    /// Moves the mouse to a window position and clicks the left button with
    /// <paramref name="modifiers"/> held: the press, the release and the move before them all
    /// carry the keys, as a real click with a key held does.
    /// </summary>
    public void Click(Point position, KeyModifiers modifiers)
    {
        var keys = Raw(modifiers);
        Window.MouseMove(position, keys);
        Window.MouseDown(position, MouseButton.Left, keys);
        Window.MouseUp(position, MouseButton.Left, keys);
        Settle();
    }

    /// <summary>
    /// Moves the mouse to a window position and presses the left button, without releasing it:
    /// the start of a drag that the test goes on with <see cref="MoveMouseHeld"/> and ends with
    /// <see cref="ReleaseMouse"/>, so that it can look at the app (and capture it) in the middle.
    /// </summary>
    public void PressMouse(Point position, KeyModifiers modifiers = KeyModifiers.None)
    {
        var keys = Raw(modifiers);
        Window.MouseMove(position, keys);
        Window.MouseDown(position, MouseButton.Left, keys);
        _mousePosition = position;
        Settle();
    }

    /// <summary>
    /// Moves the mouse with the left button held from where it is to <paramref name="position"/>,
    /// in <paramref name="steps"/> even steps, so that the app sees a movement and not a jump
    /// (and a drag starts once the movement passes its threshold).
    /// </summary>
    public void MoveMouseHeld(Point position, int steps = 1, KeyModifiers modifiers = KeyModifiers.None)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        var keys = Raw(modifiers) | RawInputModifiers.LeftMouseButton;
        var from = _mousePosition ?? position;
        for (var step = 1; step <= steps; step++)
        {
            Window.MouseMove(from + ((position - from) * step / steps), keys);
            Settle();
        }

        _mousePosition = position;
    }

    /// <summary>Releases the left button at <paramref name="position"/>: the drop that ends <see cref="PressMouse"/>.</summary>
    public void ReleaseMouse(Point position, KeyModifiers modifiers = KeyModifiers.None)
    {
        var keys = Raw(modifiers);
        Window.MouseMove(position, keys | RawInputModifiers.LeftMouseButton);
        Window.MouseUp(position, MouseButton.Left, keys);
        _mousePosition = position;
        Settle();
    }

    /// <summary>
    /// A point in the Message column of the active graph's commit row <paramref name="row"/>
    /// (counted as <c>SelectedIndex</c> counts, without the working-changes row), in the window:
    /// where a check clicks a row (4h).
    /// </summary>
    public Point GraphRowPoint(int row)
    {
        var graph = Find<CommitGraphControl>("GraphRows");
        var x = graph.Columns.Message.IsVisible ? graph.Columns.Message.X + 30 : graph.Columns.Graph.Right + 10;
        return graph.TranslatePoint(new Point(x, graph.RowBounds(row).Center.Y), Window)
            ?? throw new InvalidOperationException("The graph is not in the window.");
    }

    /// <summary>
    /// Where the active graph draws the label <paramref name="text"/> (as drawn: a name, a name cut
    /// with "…", or "+N") of commit row <paramref name="row"/>, in the window: what a check
    /// presses to drag a branch, or drops onto (4g).
    /// </summary>
    public Rect GraphLabelBounds(int row, string text)
    {
        var graph = Find<CommitGraphControl>("GraphRows");
        var labels = graph.LabelsInRow(row);
        var label = labels.FirstOrDefault(candidate => candidate.Text == text)
            ?? throw new InvalidOperationException($"Row {row} has no label \"{text}\"; it has: {string.Join(", ", labels.Select(candidate => candidate.Text))}.");
        var topLeft = graph.TranslatePoint(label.Bounds.TopLeft, Window)
            ?? throw new InvalidOperationException("The graph is not in the window.");
        return new Rect(topLeft, label.Bounds.Size);
    }

    /// <summary>The window's branch drag (4g): whether a drag runs, its source, its target.</summary>
    public BranchDragService BranchDrag =>
        Find<DragLayer>("DragLayer").Service ?? throw new InvalidOperationException("The drag layer has no drag service: it is in no window.");

    /// <summary>
    /// The view model of the dialog on top (the one that takes the input, D104), as
    /// <typeparamref name="T"/>. Throws when no dialog is open or the top one is of another kind.
    /// </summary>
    public T Dialog<T>()
        where T : DialogViewModel
    {
        Settle();
        return Session.Services.DialogHost.Current switch
        {
            T dialog => dialog,
            null => throw new InvalidOperationException($"No dialog is open; a {typeof(T).Name} was expected."),
            var other => throw new InvalidOperationException($"The dialog on top is a {other.GetType().Name} (\"{other.Title}\"), not a {typeof(T).Name}."),
        };
    }

    /// <summary>
    /// The view of the dialog on top (<c>DialogHostViewModel.Current</c>, the one that takes the
    /// input, D104) as the dialog layer draws it, or null when no dialog is open.
    /// </summary>
    public Control? TopDialogView
    {
        get
        {
            Settle();
            return TopDialogViewNow();
        }
    }

    /// <summary>The open context menu's presenter (4a), or null when no menu is open; it does not wait for one.</summary>
    public Control? OpenMenuView
    {
        get
        {
            Settle();
            return MenuPresenters().FirstOrDefault();
        }
    }

    /// <summary>
    /// Finds the control whose automation id is <paramref name="automationId"/> inside
    /// <paramref name="scope"/> (the scope itself included), such as <see cref="TopDialogView"/>
    /// or <see cref="OpenMenuView"/>: a visible control before a hidden one.
    /// </summary>
    public Control FindByAutomationId(Visual scope, string automationId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return FindIn(scope, automationId)
            ?? throw new InvalidOperationException($"No control with the automation id '{automationId}' is in the {scope.GetType().Name}.");
    }

    /// <summary>
    /// The search of <see cref="FindByAutomationId(string)"/>: the open menu, the dialog on top,
    /// the window. It looks at the window as it is, without letting the UI thread run first.
    /// </summary>
    private Control? FindWhereShown(string automationId)
    {
        ArgumentNullException.ThrowIfNull(automationId);
        return (MenuPresenters().FirstOrDefault() is { } menu ? FindIn(menu, automationId) : null)
            ?? (TopDialogViewNow() is { } dialog ? FindIn(dialog, automationId) : null)
            ?? FindIn(Window, automationId);
    }

    private Control? TopDialogViewNow() =>
        Session.Services.DialogHost.Current is { } current
            ? Window.GetVisualDescendants().OfType<ContentPresenter>()
                .FirstOrDefault(presenter => ReferenceEquals(presenter.Content, current) && presenter.Child is not null)?.Child
            : null;

    private static Control? FindIn(Visual scope, string automationId)
    {
        var matches = (scope is Control self ? new[] { self } : [])
            .Concat(scope.GetVisualDescendants().OfType<Control>())
            .Where(control => AutomationProperties.GetAutomationId(control) == automationId)
            .ToList();
        return matches.FirstOrDefault(control => control.IsEffectivelyVisible) ?? matches.FirstOrDefault();
    }

    /// <summary>Waits until the dialog on top is a <typeparamref name="T"/> and returns its view model.</summary>
    public async Task<T> WaitForDialogAsync<T>(TimeSpan? timeout = null)
        where T : DialogViewModel
    {
        await WaitForAsync(() => Session.Services.DialogHost.Current is T, $"a {typeof(T).Name} on top", timeout);
        return Dialog<T>();
    }

    /// <summary>
    /// Types <paramref name="text"/> into the text box whose automation id is
    /// <paramref name="automationId"/>, as a user would after clicking into it: the box takes the
    /// keyboard focus, the caret goes to the end of what it holds, and the text arrives as typed
    /// characters (so the box's bindings and validation see each one). What the box held stays in
    /// front of it.
    /// </summary>
    public void TypeInto(string automationId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var control = FindByAutomationId(automationId);
        var box = control as TextBox
            ?? control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The control with the automation id '{automationId}' is a {control.GetType().Name}, not a text box.");
        if (!box.IsEffectivelyEnabled || !box.IsEffectivelyVisible)
        {
            throw new InvalidOperationException($"The text box '{automationId}' cannot be typed into: it is {(box.IsEffectivelyVisible ? "disabled" : "not visible")}.");
        }

        box.Focus(NavigationMethod.Pointer);
        Settle();
        box.CaretIndex = box.Text?.Length ?? 0;
        Type(text);
    }

    /// <summary>
    /// Waits until <paramref name="repository"/>'s operation queue (the active tab's when null)
    /// has nothing running and nothing waiting: every operation queued so far has ended, its
    /// refresh included (design 1.3), and what the views show of it has been bound.
    /// </summary>
    /// <exception cref="InvalidOperationException">No repository was given and the active tab has none.</exception>
    /// <exception cref="TimeoutException">The queue was still busy when the time was up.</exception>
    public async Task WaitForIdleAsync(RepositoryViewModel? repository = null, TimeSpan? timeout = null)
    {
        var repo = repository
            ?? Session.Shell.ActiveTab.Repository
            ?? throw new InvalidOperationException("The active tab has no repository whose operations could be waited for.");
        var queue = repo.Queue;
        await WaitForAsync(
            () => queue.Current is null && queue.Waiting == 0 && queue.WhenIdleAsync().IsCompleted,
            $"the operations of {repo.Name} to end",
            timeout);

        // The queue's Changed and Finished go to the views through the UI thread.
        Settle();
    }

    /// <summary>Whether a context menu is in the window now, without waiting for one.</summary>
    public bool IsMenuOpen
    {
        get
        {
            Settle();
            return MenuPresenters().Any();
        }
    }

    /// <summary>How many context menus are in the window now (4a: never more than one).</summary>
    public int MenuCount
    {
        get
        {
            Settle();
            return MenuPresenters().Count();
        }
    }

    /// <summary>
    /// The open context menu (4a): its header, its items with label, enabled, destructive and
    /// highlighted, and its separators, in order. Waits for the menu by polling until it is in the
    /// window, drawn and in the same place twice in a row (the first menu of a process can take
    /// over a second to be placed), never for a fixed time.
    /// </summary>
    /// <exception cref="TimeoutException">No menu was open and placed within <paramref name="timeout"/> (<see cref="DefaultWaitTimeout"/> when null).</exception>
    public MenuSnapshot Menu(TimeSpan? timeout = null)
    {
        var presenter = WaitForMenuPresenter(timeout);
        string? header = null;
        string? headerToolTip = null;
        var entries = new List<MenuEntrySnapshot>();
        foreach (var entry in presenter.Items.OfType<Control>())
        {
            switch (entry)
            {
                case Avalonia.Controls.MenuItem item when item.Classes.Contains(MenuLayer.HeaderClass):
                    header = AutomationProperties.GetName(item);
                    headerToolTip = ToolTip.GetTip(item) as string;
                    break;
                case Separator separator:
                    entries.Add(new MenuEntrySnapshot("—", true, true, false, false, null, null, BoundsOf(separator)));
                    break;
                case Avalonia.Controls.MenuItem item:
                    entries.Add(new MenuEntrySnapshot(
                        item.Header as string ?? AutomationProperties.GetName(item) ?? string.Empty,
                        false,
                        item.IsEffectivelyEnabled,
                        item.Classes.Contains(MenuLayer.DangerClass),
                        item.IsSelected,
                        ToolTip.GetTip(item) as string,
                        TextColorOf(item),
                        BoundsOf(item)));
                    break;
            }
        }

        return new MenuSnapshot(header, headerToolTip, entries, BoundsOf(presenter));
    }

    /// <summary>
    /// Clicks the open menu's item labelled <paramref name="label"/> with the mouse, moved there in
    /// one move (so it crosses no other item), and lets the UI thread run the action the click
    /// posts. Throws when the menu has no such item or it is disabled.
    /// </summary>
    public void ClickMenuItem(string label)
    {
        var item = MenuItemControl(label);
        if (!item.IsEffectivelyEnabled)
        {
            throw new InvalidOperationException($"The menu item \"{label}\" is disabled: a click on it does nothing.");
        }

        Click(BoundsOf(item).Center);

        // The item closes the menu, then posts its action to the UI thread (4a).
        Settle();
    }

    /// <summary>Where the open menu's item labelled <paramref name="label"/> is, in the window's logical pixels: to rest the pointer on it.</summary>
    public Rect MenuItemBounds(string label) => BoundsOf(MenuItemControl(label));

    private Avalonia.Controls.MenuItem MenuItemControl(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var presenter = WaitForMenuPresenter(null);
        var matches = presenter.Items.OfType<Avalonia.Controls.MenuItem>()
            .Where(item => !item.Classes.Contains(MenuLayer.HeaderClass) && item.Header as string == label)
            .ToList();
        return matches switch
        {
            [var item] => item,
            [] => throw new InvalidOperationException($"The open menu has no item \"{label}\"; it has: {Menu()}."),
            _ => throw new InvalidOperationException($"The open menu has more than one item \"{label}\"."),
        };
    }

    private IEnumerable<MenuFlyoutPresenter> MenuPresenters() =>
        Window.GetVisualDescendants().OfType<MenuFlyoutPresenter>()
            .Where(presenter => AutomationProperties.GetAutomationId(presenter) == MenuLayer.MenuAutomationId && presenter.IsEffectivelyVisible);

    private MenuFlyoutPresenter WaitForMenuPresenter(TimeSpan? timeout)
    {
        var limit = timeout ?? DefaultWaitTimeout;
        var clock = Stopwatch.StartNew();
        Rect? last = null;
        while (true)
        {
            Settle();
            var presenters = MenuPresenters().ToList();
            if (presenters.Count > 1)
            {
                throw new InvalidOperationException($"{presenters.Count} context menus are open; there is never more than one (4a).");
            }

            if (presenters is [var presenter] && presenter.IsArrangeValid && presenter.Bounds.Width > 0 && presenter.Bounds.Height > 0)
            {
                var bounds = BoundsOf(presenter);
                if (last == bounds)
                {
                    return presenter;
                }

                last = bounds;
            }
            else
            {
                last = null;
            }

            if (clock.Elapsed >= limit)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Timed out after {limit.TotalSeconds:0.###} s waiting for a context menu to open."));
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>The colour an item's label is drawn in: its header presenter's foreground.</summary>
    private static Color? TextColorOf(Avalonia.Controls.MenuItem item)
    {
        var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault(part => part.Name == "PART_HeaderPresenter");
        return (presenter?.Foreground ?? item.Foreground) is ISolidColorBrush brush ? brush.Color : null;
    }

    private static RawInputModifiers Raw(KeyModifiers modifiers)
    {
        var raw = RawInputModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            raw |= RawInputModifiers.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            raw |= RawInputModifiers.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            raw |= RawInputModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            raw |= RawInputModifiers.Meta;
        }

        return raw;
    }
}
