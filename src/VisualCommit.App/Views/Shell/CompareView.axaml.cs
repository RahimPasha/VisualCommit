using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Shell;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// A comparison in the right panel (docs/phase-5-design.md 6.4; see <see cref="CompareViewModel"/>).
/// The code-behind opens a listed file's menu (<c>ForCompareFile</c>, FD21): a right-click opens
/// the menu only, selecting no row and opening no diff, as on a commit's file. Its root is a UI
/// Automation control element (a pane named by its title), as the multi-selection view's is.
/// </summary>
public partial class CompareView : UserControl
{
    public CompareView()
    {
        InitializeComponent();
        CompareFileList.AddHandler(ContextRequestedEvent, OnFileContextRequested);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CompareAutomationPeer(this);

    private void OnFileContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled
            || DataContext is not CompareViewModel comparison
            || ContextMenuRequests.Find<Button>(e.Source, button => button.Classes.Contains("fileRow")) is not { DataContext: ChangedFileRow { File: { } file } } row
            || ContextMenuRequests.ActiveRepository(this) is not ({ } layer, { } repository))
        {
            return;
        }

        var (position, openedBy) = ContextMenuRequests.Where(e, row);
        e.Handled = repository.OpenCompareFileMenu(layer, comparison, file, position, openedBy);
    }

    private sealed class CompareAutomationPeer(CompareView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
