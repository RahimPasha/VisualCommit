using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// Drags a branch onto a branch inside the window (docs/phase-3-design.md 4g, D87): a pointer
/// drag, not the operating system's drag and drop, so that both harnesses drive and capture it
/// with plain mouse input. One per window, made by its <see cref="DragLayer"/>, which draws the
/// dragged pill and the target's outline.
/// </summary>
/// <remarks>
/// <para>
/// A source asks for a drag on the press (<see cref="TryBegin"/>): the graph's branch labels from
/// the graph control, the left panel's branch rows (marked with <see cref="RefDrag"/>) from here.
/// The press does what a click does before that (the graph selects its row). The drag starts
/// when the pointer has moved more than <see cref="Threshold"/> pixels with the left button held:
/// the service then captures the pointer on the drag layer, so the moves and the release come
/// here, the left panel's button gets no click and the graph changes its selection no further.
/// </para>
/// <para>
/// Targets are local branches other than the source, and remote branches when the source is a
/// local branch; tags, stashes, HEAD and the "+N" label are none. Releasing over a target raises
/// <see cref="Dropped"/> and opens the menu the active repository's
/// <see cref="RepositoryViewModel.BranchDropMenu"/> makes for it at the release point; releasing
/// anywhere else, Esc or the window losing the pointer cancels.
/// </para>
/// </remarks>
public sealed class BranchDragService : IEscapeHandler, IDisposable
{
    /// <summary>How far, in logical pixels, the pointer moves from the press with the button held before a drag starts.</summary>
    public const double Threshold = 4;

    private static readonly ConditionalWeakTable<TopLevel, BranchDragService> Services = [];

    private readonly TopLevel _topLevel;
    private readonly DragLayer _layer;
    private readonly List<Func<Point, DropTarget?>> _targets = [];
    private DragSource? _pending;
    private IPointer? _pointer;
    private Point _pressedAt;
    private bool _ending;
    private bool _draggedSincePress;

    /// <param name="topLevel">The window the drags happen in.</param>
    /// <param name="layer">Its drag layer, which captures the pointer while a branch is dragged.</param>
    public BranchDragService(TopLevel topLevel, DragLayer layer)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(layer);
        _topLevel = topLevel;
        _layer = layer;

        // Tunnelling, and also when handled: the window sees every press, move and release before
        // the control under the pointer, which handles its own presses.
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        layer.PointerCaptureLost += OnCaptureLost;
        Services.AddOrUpdate(topLevel, this);
    }

    /// <summary>The branch being dragged, or null while no drag runs.</summary>
    public DragSource? Source { get; private set; }

    /// <summary>The valid target under the pointer, outlined; null over anything else.</summary>
    public DropTarget? Target { get; private set; }

    /// <summary>Where the pointer is, in the window's coordinates, while a branch is dragged.</summary>
    public Point Position { get; private set; }

    /// <summary>The lane colour number (0 to 7) of the dragged branch's label: that of its commit's row in the graph.</summary>
    public int SourceColor { get; private set; }

    public bool IsDragging => Source is not null;

    /// <summary>
    /// Whether the latest press came after a drag that started with the press before it. Avalonia
    /// counts that drag's press and this one as the two clicks of a double-click when they are
    /// close enough in time and place, but the first was no click: a double-tap this press raises
    /// is not a double-click (4c), and the graph's labels and the left panel's rows ignore it.
    /// </summary>
    public bool PressFollowsDrag { get; private set; }

    /// <summary>Raised when the drag starts, moves, finds or leaves a target, or ends: the drag layer redraws.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when a branch is dropped on a target, before its menu opens.</summary>
    public event EventHandler<BranchDrop>? Dropped;

    /// <summary>The window's drag service, or null when <paramref name="visual"/> is in no window with a drag layer.</summary>
    public static BranchDragService? For(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        return TopLevel.GetTopLevel(visual) is { } topLevel && Services.TryGetValue(topLevel, out var service) ? service : null;
    }

    /// <summary>
    /// A source was pressed with the left button: it is dragged once the pointer moves more than
    /// <see cref="Threshold"/> with the button held. Returns false when the press cannot start a
    /// drag (another button, or a drag already runs).
    /// </summary>
    public bool TryBegin(DragSource source, PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(e);
        if (IsDragging || !e.GetCurrentPoint(_topLevel).Properties.IsLeftButtonPressed)
        {
            return false;
        }

        _pending = source;
        _pointer = e.Pointer;
        _pressedAt = e.GetPosition(_topLevel);
        return true;
    }

    /// <summary>
    /// Adds a way to find drop targets: a function from a point of the window to the target there.
    /// Asked before the graph's labels and the left panel's rows, which are found by themselves.
    /// Disposing the result removes it.
    /// </summary>
    public IDisposable RegisterTarget(Func<Point, DropTarget?> hitTest)
    {
        ArgumentNullException.ThrowIfNull(hitTest);
        _targets.Add(hitTest);
        return new Removal(() => _targets.Remove(hitTest));
    }

    /// <summary>Esc during a drag cancels it (4g); otherwise Esc goes on down the chain.</summary>
    public bool HandleEscape()
    {
        if (!IsDragging)
        {
            return false;
        }

        End();
        return true;
    }

    /// <summary>Stops a drag that runs, or one that waits for the threshold, without dropping.</summary>
    public void Cancel()
    {
        _pending = null;
        if (IsDragging)
        {
            End();
        }
    }

    public void Dispose()
    {
        Cancel();
        _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        _topLevel.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        _layer.PointerCaptureLost -= OnCaptureLost;
        if (Services.TryGetValue(_topLevel, out var registered) && ReferenceEquals(registered, this))
        {
            Services.Remove(_topLevel);
        }
    }

    /// <summary>The target at a point of the window, if it is a valid one for the branch being dragged.</summary>
    public DropTarget? TargetAt(Point point)
    {
        var target = FindTarget(point);
        return target is null || Source is not { } source || !IsValid(source, target) ? null : target;
    }

    /// <summary>Whether <paramref name="target"/> takes a drop of <paramref name="source"/> (4g).</summary>
    public static bool IsValid(DragSource source, DropTarget target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        return !string.Equals(source.RefName, target.RefName, StringComparison.Ordinal) && !(target.IsRemote && source.IsRemote);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsDragging)
        {
            // Another button pressed during the drag goes nowhere.
            e.Handled = true;
            return;
        }

        // Seen before the press reaches the control under the pointer and its double-tap.
        PressFollowsDrag = _draggedSincePress;
        _draggedSincePress = false;
        _pending = null;
        if (!e.GetCurrentPoint(_topLevel).Properties.IsLeftButtonPressed || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        // A branch row of the left panel (RefDrag.Source); the graph's labels ask for themselves.
        // A toggle of a hidden or soloed row arms no drag (RefDrag.IsExcluded, docs/phase-5-design.md 8.3).
        for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control excluded && RefDrag.GetIsExcluded(excluded))
            {
                return;
            }

            if (visual is Control control && RefDrag.GetSource(control) is RefItemRow row)
            {
                if (SourceOf(row, control) is { } source)
                {
                    TryBegin(source, e);
                }

                return;
            }
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }

        var position = e.GetPosition(_topLevel);
        if (IsDragging)
        {
            Position = position;
            Target = TargetAt(position);
            Changed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (_pending is not { } pending)
        {
            return;
        }

        if (!e.GetCurrentPoint(_topLevel).Properties.IsLeftButtonPressed)
        {
            _pending = null;
            return;
        }

        var moved = position - _pressedAt;
        if (Math.Sqrt((moved.X * moved.X) + (moved.Y * moved.Y)) > Threshold)
        {
            Start(pending, e.Pointer, position);
            e.Handled = true;
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsDragging || !ReferenceEquals(e.Pointer, _pointer))
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _pending = null;
            }

            return;
        }

        e.Handled = true;
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var position = e.GetPosition(_topLevel);
        var source = Source!;
        var target = TargetAt(position);
        End();
        if (target is not null)
        {
            Drop(new BranchDrop(source.RefName, target.RefName, position.X, position.Y));
        }
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        // The window lost the pointer (deactivated, another capture): the drag ends without a drop.
        if (IsDragging && !_ending)
        {
            End();
        }
    }

    private void Start(DragSource source, IPointer pointer, Point position)
    {
        _pending = null;
        _draggedSincePress = true;
        Source = source;
        SourceColor = source.Sha is { } sha && GraphOf()?.LabelColorOf(sha) is { } color ? color : 0;
        Position = position;
        pointer.Capture(_layer);
        source.SetDimmed?.Invoke(true);
        Target = TargetAt(position);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void End()
    {
        var source = Source;
        Source = null;
        Target = null;
        _pending = null;
        _ending = true;
        try
        {
            if (_pointer?.Captured is { } captured && ReferenceEquals(captured, _layer))
            {
                _pointer.Capture(null);
            }
        }
        finally
        {
            _ending = false;
        }

        source?.SetDimmed?.Invoke(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A drop on a target: tells whoever listens, then opens the repository's drop menu at the release point, when it has one.</summary>
    private void Drop(BranchDrop drop)
    {
        Dropped?.Invoke(this, drop);
        if (_topLevel.DataContext is MainWindowViewModel shell
            && shell.ActiveTab.Repository?.BranchDropMenu is { } makeMenu
            && makeMenu(drop) is { } menu)
        {
            shell.Menu.Open(menu, new MenuPosition(drop.X, drop.Y));
        }
    }

    private DropTarget? FindTarget(Point point)
    {
        foreach (var hitTest in _targets)
        {
            if (hitTest(point) is { } registered)
            {
                return registered;
            }
        }

        for (var visual = _topLevel.InputHitTest(point) as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is CommitGraphControl graph)
            {
                return _topLevel.TranslatePoint(point, graph) is { } inGraph ? graph.DropTargetAt(inGraph) : null;
            }

            if (visual is Control control && RefDrag.GetSource(control) is RefItemRow row)
            {
                return TargetOf(row, control);
            }
        }

        return null;
    }

    private CommitGraphControl? GraphOf() =>
        _topLevel.GetVisualDescendants().OfType<CommitGraphControl>().FirstOrDefault(graph => graph.IsEffectivelyVisible);

    /// <summary>A left panel row as a drag source: a local or remote branch, nothing else.</summary>
    private static DragSource? SourceOf(RefItemRow row, Control control) =>
        BranchOf(row) is { } branch
            ? new DragSource(branch.RefName, row.ToolTip, branch.IsRemote, row.IsHead, row.TargetSha, dimmed => control.Opacity = dimmed ? CommitGraphControl.DraggedSourceOpacity : 1)
            : null;

    /// <summary>A left panel row as a drop target: a local or remote branch; its outline is the row's bounds.</summary>
    private static DropTarget? TargetOf(RefItemRow row, Control control) =>
        BranchOf(row) is { } branch
            ? new DropTarget(branch.RefName, row.ToolTip, branch.IsRemote, control, new Rect(control.Bounds.Size), IsLabel: false)
            : null;

    /// <summary>The full ref name of a branch row (its key is <c>ref:</c> and the full name) and whether it is a remote branch.</summary>
    private static (string RefName, bool IsRemote)? BranchOf(RefItemRow row) =>
        row.Kind is RefItemKind.LocalBranch or RefItemKind.RemoteBranch && row.Key.StartsWith(RefKeyPrefix, StringComparison.Ordinal)
            ? (row.Key[RefKeyPrefix.Length..], row.Kind == RefItemKind.RemoteBranch)
            : null;

    private const string RefKeyPrefix = "ref:";

    private sealed class Removal(Action remove) : IDisposable
    {
        private Action? _remove = remove;

        public void Dispose() => Interlocked.Exchange(ref _remove, null)?.Invoke();
    }
}

/// <summary>What is dragged (4g): a local or remote branch.</summary>
/// <param name="RefName">Its full ref name.</param>
/// <param name="Name">The name its pill shows: <c>feature/alpha</c>, <c>origin/main</c>.</param>
/// <param name="IsRemote">A remote branch: its pill is outlined, and it can only be dropped on a local branch.</param>
/// <param name="IsCurrent">The branch HEAD is on: its pill has the check mark.</param>
/// <param name="Sha">The commit it points to, whose row gives the pill its lane colour; null when unknown.</param>
/// <param name="SetDimmed">Draws the source at half opacity (true) while it is dragged, and back at full (false).</param>
public sealed record DragSource(string RefName, string Name, bool IsRemote, bool IsCurrent, string? Sha, Action<bool>? SetDimmed);

/// <summary>Where a branch can be dropped (4g): a branch label in the graph or a branch row of the left panel.</summary>
/// <param name="RefName">The target branch's full ref name.</param>
/// <param name="Name">Its name as shown.</param>
/// <param name="IsRemote">A remote branch: only a local branch can be dropped on it.</param>
/// <param name="Owner">The control it is drawn in.</param>
/// <param name="Bounds">Where, in <paramref name="Owner"/>'s coordinates: what the drag layer outlines.</param>
/// <param name="IsLabel">A label (outlined around it) rather than a row (outlined inside it).</param>
public sealed record DropTarget(string RefName, string Name, bool IsRemote, Visual Owner, Rect Bounds, bool IsLabel);
