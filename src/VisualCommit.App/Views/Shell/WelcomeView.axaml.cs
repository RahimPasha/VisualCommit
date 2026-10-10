using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using VisualCommit.App.ViewModels;

namespace VisualCommit.App.Views.Shell;

public partial class WelcomeView : UserControl
{
    private WelcomeViewModel? _viewModel;

    public WelcomeView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as WelcomeViewModel);
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
}
