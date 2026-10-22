using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

public partial class CommitGraphView : UserControl
{
    private RepoTabViewModel? _tab;
    private RepositoryViewModel? _repository;
    private ActivityLogViewModel? _activityLog;

    public CommitGraphView()
    {
        InitializeComponent();

        // The activity log under the graph area (docs/phase-5-design.md 2.1): its row's height
        // follows the panel's state, the graph area's height and the splitter.
        ActivityLog.DataContextChanged += (_, _) => FollowActivityLog(ActivityLog.DataContext as ActivityLogViewModel);
        ActivityLogSplitter.DragCompleted += (_, _) => ActivityLogDragged();
        SizeChanged += (_, _) => FitActivityLog();
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

    private void FollowActivityLog(ActivityLogViewModel? activityLog)
    {
        if (ReferenceEquals(activityLog, _activityLog))
        {
            return;
        }

        if (_activityLog is not null)
        {
            _activityLog.PropertyChanged -= OnActivityLogChanged;
            _activityLog.Closing -= OnActivityLogClosing;
        }

        _activityLog = activityLog;
        if (activityLog is not null)
        {
            activityLog.PropertyChanged += OnActivityLogChanged;
            activityLog.Closing += OnActivityLogClosing;
        }

        FitActivityLog();
    }

    private void OnActivityLogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ActivityLogViewModel.IsShown) or nameof(ActivityLogViewModel.PreferredHeight))
        {
            FitActivityLog();
        }
    }

    /// <summary>
    /// The panel's row (2.1, AD1): while it shows, its height as <see cref="ActivityLogViewModel.Fit"/>
    /// gives it for this graph area, the splitter kept between 120 and what leaves the content above
    /// it 160; while it is hidden, nothing, so that every position of phases 0 to 4 holds.
    /// </summary>
    private void FitActivityLog()
    {
        var rows = GraphAreaGrid.RowDefinitions;
        var (content, panel) = (rows[0], rows[2]);
        content.Height = new GridLength(1, GridUnitType.Star);
        if (_activityLog is not { IsShown: true } activityLog)
        {
            content.MinHeight = 0;
            panel.MinHeight = 0;
            panel.MaxHeight = double.PositiveInfinity;
            panel.Height = GridLength.Auto;
            return;
        }

        var height = Bounds.Height;
        if (height <= 0)
        {
            // Not laid out yet: the size change that follows fits it.
            panel.Height = new GridLength(activityLog.PreferredHeight);
            return;
        }

        var fitted = ActivityLogViewModel.Fit(height, activityLog.PreferredHeight);
        var room = height - ActivityLogViewModel.SplitterHeight;
        panel.MinHeight = Math.Min(ActivityLogViewModel.MinimumHeight, fitted);
        panel.MaxHeight = Math.Max(fitted, room - ActivityLogViewModel.ContentMinimumHeight);
        content.MinHeight = Math.Clamp(room - fitted, 0, ActivityLogViewModel.ContentMinimumHeight);
        panel.Height = new GridLength(fitted);
    }

    /// <summary>
    /// The user let go of the splitter (2.1): the panel's height as dragged becomes its height,
    /// saved in the session. A click on the splitter without a move changes nothing.
    /// </summary>
    private void ActivityLogDragged()
    {
        if (_activityLog is not { IsShown: true } activityLog)
        {
            return;
        }

        var dragged = GraphAreaGrid.RowDefinitions[2].ActualHeight;
        if (Math.Abs(dragged - ActivityLogViewModel.Fit(Bounds.Height, activityLog.PreferredHeight)) > 0.5)
        {
            activityLog.SaveHeight(dragged);
        }

        FitActivityLog();
    }

    /// <summary>
    /// The panel closes with the focus inside it (2.1, AD6): the focus goes to what the graph area
    /// shows once the panel is gone, the graph or the page, the interactive list, the diff or the
    /// resolver in its place, never to the banner above it.
    /// </summary>
    private void OnActivityLogClosing(object? sender, EventArgs e)
    {
        if (ActivityLog.HasFocusWithin)
        {
            Dispatcher.UIThread.Post(FocusGraphAreaContent, DispatcherPriority.Loaded);
        }
    }

    private void FocusGraphAreaContent()
    {
        // A dashboard's list (docs/phase-6-design.md 2.4).
        if (_tab is { ShowsDashboard: true })
        {
            DashboardContent.FocusList();
            return;
        }

        switch (_repository)
        {
            case { ShowsPage: true, Page: { } page }:
                page.RequestFocus();
                return;
            case { ShowsInteractiveRebase: true, InteractiveRebase: { } list }:
                list.RequestFocus();
                return;
            case { ShowsGraph: true }:
                GraphAreaContent.GetVisualDescendants().OfType<CommitGraphControl>().FirstOrDefault()?.Focus(NavigationMethod.Unspecified);
                return;
        }

        // The diff or the resolver: as the resolver gives it on opening (phase 4's 11.5), its
        // result's text or a card's first button; otherwise the first part that takes the focus.
        var body = GraphAreaBody.Children.FirstOrDefault(child => child.IsVisible);
        var target = body?.GetVisualDescendants().OfType<ConflictResultEditor>().FirstOrDefault(editor => editor.IsEffectivelyVisible)?.TextArea
            ?? FirstFocusable(body?.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault(items => items.Name == "CardButtons"))
            ?? FirstFocusable(body);
        target?.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>
    /// The element the graph area gives the focus to (docs/phase-6-design.md 6.6), as
    /// <see cref="FocusGraphAreaContent"/> chooses it, for F6's navigation: the dashboard's list; the
    /// page's, the interactive list's or the welcome page's main element (<see cref="KeyRegion.IsMainProperty"/>);
    /// the graph; the resolver's result's text or its card's first button; otherwise the first part
    /// that takes the focus. Null when nothing shown can take it.
    /// </summary>
    public InputElement? GraphAreaFocusTarget()
    {
        if (_tab is { ShowsDashboard: true })
        {
            var list = DashboardContent.FindControl<ListBox>("DashboardList");
            return list is { IsEffectivelyVisible: true } ? list : FirstFocusable(DashboardSlot);
        }

        if (_tab is { ShowsWelcome: true })
        {
            return MainOf(this.GetVisualDescendants().OfType<WelcomeView>().FirstOrDefault()) ?? FirstFocusable(this.GetVisualDescendants().OfType<WelcomeView>().FirstOrDefault());
        }

        switch (_repository)
        {
            case { ShowsPage: true }:
                return MainOf(PageSlot) ?? FirstFocusable(PageSlot);
            case { ShowsInteractiveRebase: true }:
                var rebase = GraphAreaBody.Children.FirstOrDefault(child => child.IsVisible);
                return MainOf(rebase) ?? FirstFocusable(rebase);
            case { ShowsGraph: true }:
                return GraphAreaContent.GetVisualDescendants().OfType<CommitGraphControl>().FirstOrDefault();
            case null:
                return null;
        }

        var body = GraphAreaBody.Children.FirstOrDefault(child => child.IsVisible);
        return body?.GetVisualDescendants().OfType<ConflictResultEditor>().FirstOrDefault(editor => editor.IsEffectivelyVisible)?.TextArea
            ?? FirstFocusable(body?.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault(items => items.Name == "CardButtons"))
            ?? FirstFocusable(body);
    }

    /// <summary>The first element marked <see cref="KeyRegion.IsMainProperty"/> that is shown and can take the focus inside <paramref name="within"/>.</summary>
    private static InputElement? MainOf(Visual? within) =>
        within?.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(element => element is Control control && KeyRegion.GetIsMain(control) && element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled);

    private static InputElement? FirstFocusable(Visual? within) =>
        within?.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled);
}
