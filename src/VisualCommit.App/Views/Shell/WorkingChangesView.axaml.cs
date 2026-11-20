using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    /// row. The focus comes with the keyboard's frame, as Tab gives it.
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
        FocusReturn.AfterRun(command, row, () => RowFocusTarget(topLevel, list, other, place), NavigationMethod.Tab);
    }

    /// <summary>Where the focus goes after a row's action (<see cref="OnRowButtonClick"/>).</summary>
    private InputElement? RowFocusTarget(TopLevel? topLevel, ItemsControl list, ItemsControl other, int place)
    {
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
        return list.ContainerFromIndex(index)?.GetVisualDescendants().OfType<InputElement>()
            .FirstOrDefault(element => element is { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } && KeyboardNavigation.GetIsTabStop(element));
    }

    /// <summary>
    /// A commit starts while Amend shows the keyboard's frame: Amend, disabled while the commit runs
    /// (<see cref="WorkingChangesViewModel.CanToggleAmend"/>), gets the focus back once it ends. Seen
    /// as the commit starts, before Amend is disabled: the panel's state says so first.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkingChangesViewModel.IsCommitting) && _viewModel is { IsCommitting: true } viewModel
            && AmendCheckBox.IsFocused && AmendCheckBox.Classes.Contains(FocusVisibleClass))
        {
            FocusReturn.AfterRun(viewModel.CommitCommand, AmendCheckBox, () => AmendCheckBox, NavigationMethod.Tab);
        }
    }
}
