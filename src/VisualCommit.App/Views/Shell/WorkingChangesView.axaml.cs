using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The stage panel of the active tab (C4): see <see cref="ViewModels.Panels.WorkingChangesViewModel"/>.
/// The code-behind opens a file's menu (docs/phase-3-design.md 4b): a right-click, the Menu key or
/// Shift+F10 on a file row opens the menu of that file in its list, and nothing else: the list's
/// selection and the open diff stay as they are. Folder rows of the tree have no menu. It also
/// gives the keyboard's focus a place after an action that took it away: after a row's Stage,
/// Unstage or Discard pressed by the keyboard, and after a commit during which a focused Amend was
/// disabled.
/// </summary>
public partial class WorkingChangesView : UserControl
{
    private const string RowClass = "stageRow";

    /// <summary>The pseudo-class of an element that shows the keyboard's frame (its focus came by Tab or the arrows).</summary>
    private const string FocusVisibleClass = ":focus-visible";

    private WorkingChangesViewModel? _viewModel;

    public WorkingChangesView()
    {
        InitializeComponent();
        UnstagedFileList.AddHandler(ContextRequestedEvent, (_, e) => OnContextRequested(e, FileMenuPlace.Unstaged));
        StagedFileList.AddHandler(ContextRequestedEvent, (_, e) => OnContextRequested(e, FileMenuPlace.Staged));

        // A row's buttons raise Click before their command runs.
        UnstagedFileList.AddHandler(Button.ClickEvent, (_, e) => OnRowButtonClick(e, UnstagedFileList, StagedFileList));
        StagedFileList.AddHandler(Button.ClickEvent, (_, e) => OnRowButtonClick(e, StagedFileList, UnstagedFileList));

        // Tab and Shift+Tab walk the lists' rows in their order, also those not made yet.
        UnstagedFileList.AddHandler(KeyDownEvent, (_, e) => OnListKeyDown(e, UnstagedFileList), RoutingStrategies.Tunnel);
        StagedFileList.AddHandler(KeyDownEvent, (_, e) => OnListKeyDown(e, StagedFileList), RoutingStrategies.Tunnel);

        // These may ask first, their buttons disabled meanwhile: one pressed by the keys gets the
        // focus back when its question is cancelled (D75 for Restore).
        FocusReturn.AfterKeyboardPress(StageAllButton);
        FocusReturn.AfterKeyboardPress(DiscardAllButton);
        FocusReturn.AfterKeyboardPress(RestoreDiscardButton);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        _viewModel = DataContext as WorkingChangesViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
        }

        base.OnDataContextChanged(e);
    }

    private void OnContextRequested(ContextRequestedEventArgs e, FileMenuPlace place)
    {
        if (e.Handled
            || ContextMenuRequests.Find<Panel>(e.Source, panel => panel.Classes.Contains(RowClass)) is not { DataContext: ChangedFileRow { File: { } file } } row
            || ContextMenuRequests.ActiveRepository(this) is not ({ } layer, { } repository)
            || !ReferenceEquals(repository.Changes, DataContext))
        {
            return;
        }

        var (position, openedBy) = ContextMenuRequests.Where(e, row);
        e.Handled = repository.OpenFileMenu(layer, file, place, position, openedBy);
    }

    /// <summary>
    /// A row's Stage, Unstage or Discard pressed while it showed the keyboard's frame (Enter or
    /// Space): once its run has ended and the lists show its result, the focus goes to the row now
    /// at its place in that list, else that list's last row, else the other list's first row, else
    /// the summary; when the panel itself has gone (the tree is clean), to the graph. A row's place
    /// is its index in the list. A Discard cancelled in its question gives the focus back to its
    /// row. The focus comes with the keyboard's frame, as Tab gives it. This happens only when the
    /// run took the focus away: a focus the user moved meanwhile, to the summary or elsewhere, stays,
    /// and after a switch to another tab the panel's lists are that tab's and nothing is done here
    /// (the window places a focus that the switch itself took away).
    /// </summary>
    private void OnRowButtonClick(RoutedEventArgs e, ItemsControl list, ItemsControl other)
    {
        if (e.Source is not Button { Command: IAsyncRelayCommand command } button
            || AutomationProperties.GetAutomationId(button) is not ("RowStageButton" or "RowDiscardButton" or "RowUnstageButton")
            || !button.Classes.Contains(FocusVisibleClass)
            || button.GetVisualAncestors().OfType<Panel>().FirstOrDefault(panel => panel.Classes.Contains(RowClass)) is not { } row
            || row.GetVisualAncestors().OfType<Control>().FirstOrDefault(control => list.IndexFromContainer(control) >= 0) is not { } container)
        {
            return;
        }

        var place = list.IndexFromContainer(container);
        var topLevel = TopLevel.GetTopLevel(this);
        var armed = DataContext;
        var pressedPath = (row.DataContext as ChangedFileRow)?.Key;

        // The pressed button keeps the focus while its row stays (a failed action); a list that
        // reuses the row's controls for another row leaves it on that row, which counts as lost.
        bool StillOnItsRow() =>
            ReferenceEquals(DataContext, armed) && row.IsAttachedToVisualTree()
            && row.DataContext is ChangedFileRow { Key: var path } && path == pressedPath;
        FocusReturn.AfterRun(
            command,
            row,
            () => RowFocusTarget(armed, topLevel, list, other, place),
            NavigationMethod.Tab,
            FocusReturn.LostFrom(topLevel, button, StillOnItsRow));
    }

    /// <summary>
    /// Where the focus goes after a row's action (<see cref="OnRowButtonClick"/>); nowhere when the
    /// panel shows another tab's lists, its data no longer <paramref name="armed"/>, the one the
    /// action was pressed in.
    /// </summary>
    private InputElement? RowFocusTarget(object? armed, TopLevel? topLevel, ItemsControl list, ItemsControl other, int place)
    {
        if (!ReferenceEquals(DataContext, armed))
        {
            return null;
        }

        if (!this.IsAttachedToVisualTree() || !IsEffectivelyVisible)
        {
            // The panel went with the change it showed: the graph area's own target, the graph.
            return topLevel?.GetVisualDescendants().OfType<CommitGraphView>().FirstOrDefault(view => view.IsEffectivelyVisible)?.GraphAreaFocusTarget();
        }

        if (list.ItemCount > 0)
        {
            return RowAt(list, Math.Min(place, list.ItemCount - 1));
        }

        return other.ItemCount > 0 ? RowAt(other, 0) : CommitSummary;
    }

    /// <summary>The own button (or folder button) of the row at <paramref name="index"/> of <paramref name="list"/>, brought into view.</summary>
    private static InputElement? RowAt(ItemsControl list, int index)
    {
        list.ScrollIntoView(index);
        return list.ContainerFromIndex(index) is { } container ? TabStops(container).FirstOrDefault() : null;
    }

    /// <summary>The elements of a row that Tab stops at now, in the visual tree's order: its own button, and its action buttons while they show.</summary>
    private static List<InputElement> TabStops(Control container) =>
        [.. container.GetVisualDescendants().OfType<InputElement>()
            .Where(element => element is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } && KeyboardNavigation.GetIsTabStop(element))];

    /// <summary>
    /// Tab from a row's last stop, or Shift+Tab from its first, goes to the next or previous row in
    /// the list's order: the row is brought into view and its own button takes the focus, as Tab
    /// gives it. The list makes only the rows near its view and reuses the controls of the others,
    /// so its controls' order is not its rows' order, which the keyboard's own navigation follows.
    /// Past the list's last row Tab goes to the panel's next stop, past its first Shift+Tab to the
    /// one before, the other list's rows taken in their order; beyond the panel, the keyboard's own
    /// navigation goes on.
    /// </summary>
    private void OnListKeyDown(KeyEventArgs e, ItemsControl list)
    {
        if (e.Handled || e.Key != Key.Tab || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None
            || e.Source is not Control focused
            || focused.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(control => list.IndexFromContainer(control) >= 0) is not { } container)
        {
            return;
        }

        var backwards = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var stops = TabStops(container);
        if (!ReferenceEquals(backwards ? stops.FirstOrDefault() : stops.LastOrDefault(), focused))
        {
            return;
        }

        var next = list.IndexFromContainer(container) + (backwards ? -1 : 1);
        if (next >= 0 && next < list.ItemCount)
        {
            e.Handled = true;
            FocusRow(list, next);
            return;
        }

        if (StopBeside(list, backwards) is { } beside)
        {
            e.Handled = true;
            if (beside is ItemsControl other)
            {
                FocusRow(other, backwards ? other.ItemCount - 1 : 0);
            }
            else
            {
                ((InputElement)beside).Focus(NavigationMethod.Tab);
            }
        }
    }

    /// <summary>Brings row <paramref name="index"/> of <paramref name="list"/> into view and gives its first stop, its own button, the focus as Tab does.</summary>
    private static void FocusRow(ItemsControl list, int index)
    {
        list.ScrollIntoView(index);
        if (list.ContainerFromIndex(index) is { } made && TabStops(made).FirstOrDefault() is { } target)
        {
            target.Focus(NavigationMethod.Tab);
            return;
        }

        // Not made yet: once the layout has made it.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (list.ContainerFromIndex(index) is { } later && TabStops(later).FirstOrDefault() is { } element)
                {
                    element.Focus(NavigationMethod.Tab);
                }
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The panel's stop after <paramref name="list"/>, or with <paramref name="backwards"/> before it,
    /// in the visual tree's order: a shown and enabled tab stop outside the lists, or the other list
    /// as a whole when it has rows; null when there is none in the panel.
    /// </summary>
    private object? StopBeside(ItemsControl list, bool backwards)
    {
        ItemsControl[] lists = [UnstagedFileList, StagedFileList];
        var stops = new List<object>();
        foreach (var visual in this.GetVisualDescendants())
        {
            if (visual is ItemsControl candidate && lists.Contains(candidate))
            {
                stops.Add(candidate);
            }
            else if (visual is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } element
                && KeyboardNavigation.GetIsTabStop(element) && !lists.Any(inside => inside.IsVisualAncestorOf(element)))
            {
                stops.Add(element);
            }
        }

        var at = stops.IndexOf(list);
        for (var i = at + (backwards ? -1 : 1); i >= 0 && i < stops.Count; i += backwards ? -1 : 1)
        {
            if (stops[i] is not ItemsControl other || (other.ItemCount > 0 && other.IsEffectivelyVisible))
            {
                return stops[i];
            }
        }

        return null;
    }

    /// <summary>
    /// A commit starts while Amend has the focus: Amend, disabled while the commit runs
    /// (<see cref="WorkingChangesViewModel.CanToggleAmend"/>), gets the focus back once it ends,
    /// with the keyboard's frame if it had it, unless the user has moved the focus meanwhile or the
    /// panel shows another tab's. The user reaches this by the palette's commit: the commit button,
    /// clicked or pressed by the keys, has the focus itself, and Mod+Enter commits from the
    /// message's boxes alone; after the palette's close D104 has given Amend the focus back without
    /// the frame. Seen as the commit starts, before Amend is disabled: the panel's state says so
    /// first.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkingChangesViewModel.IsCommitting) && _viewModel is { IsCommitting: true } viewModel
            && AmendCheckBox.IsFocused)
        {
            var armed = DataContext;
            FocusReturn.AfterRun(
                viewModel.CommitCommand,
                AmendCheckBox,
                () => ReferenceEquals(DataContext, armed) ? AmendCheckBox : null,
                AmendCheckBox.Classes.Contains(FocusVisibleClass) ? NavigationMethod.Tab : NavigationMethod.Unspecified,
                FocusReturn.LostFrom(TopLevel.GetTopLevel(this), AmendCheckBox, static () => false));
        }
    }
}
