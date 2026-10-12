using Avalonia.Controls;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

public partial class RightPanelView : UserControl
{
    /// <summary>
    /// The part of the details area that the subject, body and rows may take at most before they
    /// scroll: the file list keeps the rest, so a long message cannot push it out of sight.
    /// </summary>
    private const double HeaderShare = 0.6;

    /// <summary>The header part may always be this tall, however small the panel, so that the subject stays readable.</summary>
    private const double MinimumHeaderHeight = 120;

    public RightPanelView()
    {
        InitializeComponent();
        DetailsContent.SizeChanged += (_, e) =>
            DetailsHeaderScroller.MaxHeight = Math.Max(MinimumHeaderHeight, e.NewSize.Height * HeaderShare);

        // A commit's changed file has a menu of its own (docs/phase-3-design.md 4b): open file,
        // show in folder, copy path. It opens the menu only; the file's diff opens on a click.
        FileList.AddHandler(ContextRequestedEvent, OnFileContextRequested);
    }

    private void OnFileContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled
            || ContextMenuRequests.Find<Button>(e.Source, button => button.Classes.Contains("fileRow")) is not { DataContext: ChangedFileRow { File: { } file } } row
            || ContextMenuRequests.ActiveRepository(this) is not ({ } layer, { } repository)
            || !ReferenceEquals(repository.Details, DataContext))
        {
            return;
        }

        var (position, openedBy) = ContextMenuRequests.Where(e, row);
        e.Handled = repository.OpenFileMenu(layer, file, FileMenuPlace.CommitFiles, position, openedBy);
    }
}
