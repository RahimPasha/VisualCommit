using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;

namespace VisualCommit.App.Views.Shell;

public partial class CommitGraphView : UserControl
{
    private RepoTabViewModel? _tab;
    private RepositoryViewModel? _repository;

    public CommitGraphView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_tab is not null)
        {
            _tab.PropertyChanged -= OnTabPropertyChanged;
        }

        _tab = DataContext as RepoTabViewModel;
        if (_tab is not null)
        {
            _tab.PropertyChanged += OnTabPropertyChanged;
        }

        FollowRepository();
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepoTabViewModel.Repository))
        {
            FollowRepository();
        }
    }

    private void FollowRepository()
    {
        if (_repository is not null)
        {
            _repository.GraphFocusRequested -= OnGraphFocusRequested;
        }

        _repository = _tab?.Repository;
        if (_repository is not null)
        {
            _repository.GraphFocusRequested += OnGraphFocusRequested;
        }
    }

    /// <summary>
    /// The list or the resolver closed (docs/phase-4-design.md 2.2, 11.1): the focus goes to the
    /// banner's Continue when asked for and it can take it, otherwise to the graph. Posted, so that
    /// the graph is visible again first.
    /// </summary>
    private void OnGraphFocusRequested(object? sender, GraphFocusRequest request) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (request == GraphFocusRequest.ContinueFirst
                    && this.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "BannerContinueButton") is { } next
                    && next.IsEffectivelyVisible
                    && next.IsEffectivelyEnabled
                    && next.Focus(NavigationMethod.Tab))
                {
                    return;
                }

                this.GetVisualDescendants().OfType<CommitGraphControl>().FirstOrDefault()?.Focus(NavigationMethod.Unspecified);
            },
            DispatcherPriority.Loaded);
}
