using Avalonia.Controls;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The stage panel of the active tab (C4): see <see cref="ViewModels.Panels.WorkingChangesViewModel"/>.
/// The code-behind opens a file's menu (docs/phase-3-design.md 4b): a right-click, the Menu key or
/// Shift+F10 on a file row opens the menu of that file in its list, and nothing else: the list's
/// selection and the open diff stay as they are. Folder rows of the tree have no menu.
/// </summary>
public partial class WorkingChangesView : UserControl
{
    private const string RowClass = "stageRow";

    public WorkingChangesView()
    {
        InitializeComponent();
        UnstagedFileList.AddHandler(ContextRequestedEvent, (_, e) => OnContextRequested(e, FileMenuPlace.Unstaged));
        StagedFileList.AddHandler(ContextRequestedEvent, (_, e) => OnContextRequested(e, FileMenuPlace.Staged));
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
}
