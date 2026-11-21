using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Hosting;

namespace VisualCommit.App.Controls;

/// <summary>
/// The graph in UI Automation (D435, which amends D49): a list whose one child is the row a screen
/// reader is given, the working-changes row while it is selected, else the selected commit (the
/// focus row while several are selected), else none. There is no peer per row, and with no
/// automation client there is no peer at all: the control only asks whether its peer exists.
/// </summary>
/// <remarks>
/// <para>
/// The row is a polite live region. Its name is raised as changed only when the exposed row becomes
/// another (another commit, the working-changes row, a row after none), wherever the keyboard focus
/// is, and once when the graph takes the focus: a live region speaks every name change, so a new
/// text for the same row (a fetch's new label, another time zone) raises nothing and is read fresh
/// when asked. A new hint for the same row (a search's mark, a count) raises its help text change.
/// </para>
/// <para>
/// None of this takes part in drawing or layout: the peers read the control's state when asked.
/// </para>
/// </remarks>
public sealed partial class CommitGraphControl
{
    /// <summary>The automation id of the graph's one exposed row.</summary>
    public const string SelectedRowAutomationId = "GraphSelectedRow";

    /// <summary>The class name UI Automation gives the exposed row.</summary>
    public const string SelectedRowClassName = "CommitGraphRow";

    /// <summary>An update of the peers is posted for this dispatcher turn and has not run yet.</summary>
    private bool _automationUpdatePosted;

    protected override AutomationPeer OnCreateAutomationPeer() => new CommitGraphAutomationPeer(this);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ControlAutomationPeer.FromElement(this) is CommitGraphAutomationPeer peer)
        {
            peer.FocusArrived();
        }

        ScheduleAutomationUpdate();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        ScheduleAutomationUpdate();
    }

    /// <summary>Whether a change of <paramref name="property"/> may change the exposed row, its words or its hint.</summary>
    private static bool AffectsExposedRow(AvaloniaProperty property) =>
        property == SelectedIndexProperty
        || property == SelectionProperty
        || property == IsWorkingRowSelectedProperty
        || property == ShowsWorkingRowProperty
        || property == DataProperty
        || property == WorkingRowTextProperty
        || property == DatesProperty
        || property == SearchProperty
        || property == PullRequestsProperty;

    /// <summary>
    /// Posts one update of the peers for this dispatcher turn, at background priority: a selection
    /// sets two or three properties in a row, which must make one change. Without a peer, which
    /// only an automation client makes, nothing is posted.
    /// </summary>
    private void ScheduleAutomationUpdate()
    {
        if (_automationUpdatePosted || ControlAutomationPeer.FromElement(this) is not CommitGraphAutomationPeer)
        {
            return;
        }

        _automationUpdatePosted = true;
        Dispatcher.UIThread.Post(RunAutomationUpdate, DispatcherPriority.Background);
    }

    private void RunAutomationUpdate()
    {
        _automationUpdatePosted = false;
        (ControlAutomationPeer.FromElement(this) as CommitGraphAutomationPeer)?.Update();
    }

    /// <summary>The exposed row now: which it is, and its commit row (-1 for the working-changes row and for none).</summary>
    private (ExposedRowKey Key, int Index) ExposedRow()
    {
        if (ShowsWorkingRow && IsWorkingRowSelected)
        {
            return (ExposedRowKey.WorkingRow, -1);
        }

        var index = FocusRow;
        return index >= 0 && Data is { } data ? (new ExposedRowKey(false, data.CommitAt(index).Sha), index) : (ExposedRowKey.None, -1);
    }

    /// <summary>The exposed row's name, or null for none.</summary>
    private string? ExposedRowName()
    {
        var (key, index) = ExposedRow();
        if (key.IsWorkingRow)
        {
            return GraphRowSpeech.WorkingRowName(WorkingRowText ?? DefaultWorkingRowText);
        }

        if (index < 0 || Data is not { } data)
        {
            return null;
        }

        var dates = Dates ?? FallbackDates.Value;
        return GraphRowSpeech.Name(data.CommitAt(index), data.LabelsAt(index), dates.Format, RequestWordsOf);
    }

    /// <summary>The exposed row's hint: empty for the working-changes row and for none.</summary>
    private string ExposedRowHint()
    {
        var (_, index) = ExposedRow();
        if (index < 0 || Data is not { } data)
        {
            return string.Empty;
        }

        var selection = CurrentSelection;
        var count = selection.IsMultiple ? selection.Count : 1;
        var matches = SearchStateOf(data, index, ActiveSearch) == GraphSearchRowState.Marked;
        return GraphRowSpeech.Hint(data.CommitAt(index), count, matches);
    }

    /// <summary>Where the exposed row is drawn, in the control's coordinates, clipped to the control; empty when none is in view.</summary>
    private Rect ExposedRowBoundsInControl()
    {
        var (key, index) = ExposedRow();
        var row = key.IsWorkingRow ? WorkingRowBounds
            : index >= 0 ? RowBounds(index)
            : default;
        var clipped = row.Intersect(new Rect(Bounds.Size));
        return clipped.Width > 0 && clipped.Height > 0 ? clipped : default;
    }

    /// <summary>Scrolls the exposed row into view, as the keys do for the row they select.</summary>
    private void BringExposedRowIntoView()
    {
        var (key, index) = ExposedRow();
        if (key.IsWorkingRow)
        {
            ScrollDisplayRowIntoView(0);
        }
        else if (index >= 0)
        {
            ScrollIntoView(index);
        }
    }

    /// <summary>Selects the exposed row alone, as a click on it does.</summary>
    private void SelectExposedRowAlone()
    {
        var (key, index) = ExposedRow();
        if (key.IsWorkingRow)
        {
            SelectDisplayRow(0);
        }
        else if (index >= 0)
        {
            SelectDisplayRow(index + RowOffset);
        }
    }

    /// <summary>
    /// A label's open request in words, as its tooltip line names it (<see cref="LabelTipLine"/>):
    /// <c>pull request #12: Add the login form</c>; null for a label without one.
    /// </summary>
    private string? RequestWordsOf(RefLabel label)
    {
        if (PullRequestOf(label) is not { } pull)
        {
            return null;
        }

        var provider = pull.BaseRepository.Provider;
        return GraphRowSpeech.Request(pull.IsDraft, HostingTexts.Request(provider), HostingTexts.Number(provider, pull.Number), pull.Title);
    }

    /// <summary>Which row is exposed: the working-changes row, a commit by its SHA, or none.</summary>
    private readonly record struct ExposedRowKey(bool IsWorkingRow, string? Sha)
    {
        public static ExposedRowKey None => default;

        public static ExposedRowKey WorkingRow => new(true, null);

        public bool Exists => IsWorkingRow || Sha is not null;
    }

    /// <summary>
    /// The graph's peer: a list that may select several rows and needs none, whose children and
    /// selection are the one exposed row. It keeps one row peer for its life and raises the changes
    /// a client is told of when the exposed row becomes another (<see cref="Update"/>).
    /// </summary>
    private sealed class CommitGraphAutomationPeer : ControlAutomationPeer, ISelectionProvider
    {
        private readonly CommitGraphRowAutomationPeer _row;

        /// <summary>The row exposed at the last update, or when the peer was made.</summary>
        private ExposedRowKey _lastKey;

        /// <summary>The last name raised as the row's new name; null before the first.</summary>
        private string? _lastRaisedName;

        /// <summary>The row's hint at the last update, or when the peer was made.</summary>
        private string _lastHint;

        /// <summary>The graph took the keyboard focus since the last update.</summary>
        private bool _focusArrived;

        public CommitGraphAutomationPeer(CommitGraphControl owner)
            : base(owner)
        {
            _row = new CommitGraphRowAutomationPeer(owner, this);

            // A client that makes the peer reads the row as it is: only a later change is news.
            _lastKey = owner.ExposedRow().Key;
            _lastHint = owner.ExposedRowHint();
        }

        public bool CanSelectMultiple => true;

        public bool IsSelectionRequired => false;

        private new CommitGraphControl Owner => (CommitGraphControl)base.Owner;

        public IReadOnlyList<AutomationPeer> GetSelection() => Owner.ExposedRow().Key.Exists ? [_row] : [];

        /// <summary>Called when the graph takes the keyboard focus: the next update speaks the row once.</summary>
        public void FocusArrived() => _focusArrived = true;

        /// <summary>
        /// Compares the exposed row with the last one and raises what a client is told: for a row
        /// after none, the children anew, the selection and the row's name; for none after a row, the
        /// children anew and the selection; for another row, the selection and the name; for the same
        /// row, a new hint as a help text change, and its name once when the graph took the focus.
        /// </summary>
        public void Update()
        {
            var owner = Owner;
            var key = owner.ExposedRow().Key;
            var focusArrived = _focusArrived && owner.IsFocused;
            _focusArrived = false;
            var hint = owner.ExposedRowHint();

            if (key != _lastKey)
            {
                var hadRow = _lastKey.Exists;
                _lastKey = key;
                _lastHint = hint;
                if (hadRow != key.Exists)
                {
                    InvalidateChildren();
                }

                RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);
                if (key.Exists)
                {
                    RaiseRowName(hadRow ? _lastRaisedName : null);
                }

                return;
            }

            if (!key.Exists)
            {
                return;
            }

            if (hint != _lastHint)
            {
                _row.RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, _lastHint, hint);
                _lastHint = hint;
            }

            if (focusArrived)
            {
                RaiseRowName(null);
            }
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => Owner.ExposedRow().Key.Exists ? [_row] : null;

        /// <summary>Raises the row's name change, which the Windows bridge turns into a live region change.</summary>
        private void RaiseRowName(string? oldName)
        {
            var name = _row.GetName();
            _row.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, oldName, name);
            _lastRaisedName = name;
        }
    }

    /// <summary>
    /// The exposed row's peer: a list item over the graph itself, the public way to its screen
    /// bounds, named and hinted by <see cref="GraphRowSpeech"/> when read. It has no children and
    /// takes nothing of the graph's own automation properties but the focus.
    /// </summary>
    private sealed class CommitGraphRowAutomationPeer(CommitGraphControl owner, CommitGraphAutomationPeer graph)
        : ControlAutomationPeer(owner), ISelectionItemProvider
    {
        public bool IsSelected => true;

        public ISelectionProvider SelectionContainer => graph;

        private new CommitGraphControl Owner => (CommitGraphControl)base.Owner;

        public void Select() => Owner.SelectExposedRowAlone();

        public void AddToSelection()
        {
            // The row is selected already; another row joins by its own gestures.
        }

        public void RemoveFromSelection()
        {
            // The exposed row is a selected row; it leaves only when another is selected.
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetAutomationIdCore() => SelectedRowAutomationId;

        protected override string GetClassNameCore() => SelectedRowClassName;

        protected override string? GetNameCore() => Owner.ExposedRowName();

        /// <summary>The hint, never the base's fallback to the graph's tooltip, which names what the pointer is over.</summary>
        protected override string GetHelpTextCore() => Owner.ExposedRowHint();

        protected override AutomationLiveSetting GetLiveSettingCore() => AutomationLiveSetting.Polite;

        protected override AutomationPeer? GetLabeledByCore() => null;

        protected override string? GetAcceleratorKeyCore() => null;

        protected override string? GetAccessKeyCore() => null;

        protected override string? GetItemStatusCore() => null;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        protected override AutomationPeer GetParentCore() => graph;

        /// <summary>The row's drawn bounds, clipped to the graph, in the root's coordinates as the base gives the graph's.</summary>
        protected override Rect GetBoundingRectangleCore()
        {
            var bounds = Owner.ExposedRowBoundsInControl();
            if (bounds == default || Owner.VisualRoot is not { } root || Owner.TransformToVisual(root) is not { } transform)
            {
                return default;
            }

            return bounds.TransformToAABB(transform);
        }

        protected override bool IsOffscreenCore() => base.IsOffscreenCore() || Owner.ExposedRowBoundsInControl() == default;

        protected override bool HasKeyboardFocusCore() => Owner.IsFocused;

        protected override void SetFocusCore() => Owner.Focus();

        protected override void BringIntoViewCore() => Owner.BringExposedRowIntoView();

        /// <summary>The graph's keyboard menu for the row, as Shift+F10 asks for it.</summary>
        protected override bool ShowContextMenuCore()
        {
            var request = new ContextRequestedEventArgs();
            Owner.RaiseEvent(request);
            return request.Handled;
        }
    }
}
