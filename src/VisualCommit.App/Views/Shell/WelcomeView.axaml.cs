using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.App.Views.Shell;

public partial class WelcomeView : UserControl
{
    private WelcomeViewModel? _viewModel;

    public WelcomeView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as WelcomeViewModel);
        WorkspaceList.AddHandler(ContextRequestedEvent, OnWorkspaceContextRequested);

        // The browser's run disables the button that opened it, so the window cannot give the
        // focus back to it when the browser closes: it gets it once the run has ended, with no
        // frame, unless the focus is somewhere in the form by then. The palette's way to the
        // browser keeps the focus where the window put it.
        CloneFromGitHubButton.Click += (_, _) =>
        {
            if (_viewModel is { } viewModel)
            {
                FocusReturn.AfterRun(viewModel.BrowseCommand, CloneForm, () => CloneFromGitHubButton, NavigationMethod.Unspecified);
            }
        };

        // Pressed by the keys, they go with what they end (D462): Clone stands again where Cancel
        // clone stood, and the form's Cancel gives the focus back to the Clone that opened the form;
        // while a clone that Clone started runs, Clone is disabled and Cancel clone stands beside it.
        FocusReturn.Instead(CloneCancelButton, () => CloneStartButton);
        FocusReturn.Instead(CloneCloseButton, () => CloneRepoButton);
        FocusReturn.Instead(CloneStartButton, () => CloneCancelButton);
    }

    private void Watch(WelcomeViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The clone form opens with the URL field ready for typing.
        if (e.PropertyName == nameof(WelcomeViewModel.IsCloneFormOpen) && _viewModel?.IsCloneFormOpen == true)
        {
            Dispatcher.UIThread.Post(() => CloneUrlBox.Focus(), DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// A workspace item's menu (docs/phase-6-design.md 13.1, D106): a right-click opens it at the
    /// pointer, Shift+F10 and the Menu key at the focused item's bottom-left corner.
    /// </summary>
    private void OnWorkspaceContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { } viewModel || ContextMenuRequests.Layer(this) is not { } layer)
        {
            return;
        }

        var button = ContextMenuRequests.Find<Button>(e.Source, candidate => candidate.DataContext is WorkspaceItem);
        if (button?.DataContext is not WorkspaceItem item)
        {
            return;
        }

        button.Focus(NavigationMethod.Pointer);
        var (position, openedBy) = ContextMenuRequests.Where(e, button);
        layer.Open(viewModel.MenuFor(item), position, openedBy);
        e.Handled = true;
    }
}
