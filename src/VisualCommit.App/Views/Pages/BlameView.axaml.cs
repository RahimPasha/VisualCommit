using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VisualCommit.App.Controls.Blame;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views.Pages;

/// <summary>
/// The blame page (docs/phase-5-design.md 4.2): see <see cref="BlameViewModel"/>. The code-behind
/// fills the text and the gutter from the view model, keeps the gutter's width by the page's
/// (130, or 200 from a page 700 wide), turns a click on a run, in the gutter or on its text, into
/// picking it and a double-click into "Show in graph", opens a run's menu (a right-click picks
/// the run first, BD9), gives the text the keyboard focus when the page opens (BD10), and closes
/// the page on Esc (<see cref="EscapeOrder.Page"/>) unless a diff covers it or a text box has the focus.
/// </summary>
public partial class BlameView : UserControl
{
    private BlameViewModel? _viewModel;
    private IDisposable? _escape;

    public BlameView()
    {
        InitializeComponent();
        BlameGutter.Editor = BlameText;
        BlameGutter.RunPressed += (_, e) => OnRunPressed(e.Run, e.ClickCount);
        SizeChanged += (_, e) => FitGutter(e.NewSize.Width);

        // A press on the text picks its line's run; AvaloniaEdit's own handlers place the
        // selection and may mark the press handled, so this one sees it first.
        BlameText.AddHandler(PointerPressedEvent, OnTextPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            _escape = window.AddEscapeHandler(new EscapeHandler(HandleEscape), EscapeOrder.Page);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _escape?.Dispose();
        _escape = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as BlameViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            FitGutter(Bounds.Width);
        }

        ShowText();
        base.OnDataContextChanged(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BlameViewModel.Runs) or nameof(BlameViewModel.Lines) or nameof(BlameViewModel.Highlighting):
                ShowText();
                break;
            case nameof(BlameViewModel.SelectedRun):
                BlameGutter.SelectedRun = _viewModel?.SelectedRun?.Index;
                break;
            case nameof(BlameViewModel.IsWide):
                FitGutter(Bounds.Width);
                break;
        }
    }

    /// <summary>The text and the gutter's runs as the view model has them now.</summary>
    private void ShowText()
    {
        if (_viewModel is not { } viewModel)
        {
            BlameText.Show([], null, highlighting: false);
            BlameGutter.Runs = [];
            BlameGutter.SelectedRun = null;
            return;
        }

        BlameText.Show(viewModel.Text.Split('\n'), viewModel.Request.Path, viewModel.Highlighting);
        BlameGutter.Runs = [.. viewModel.Runs.Select(run => new BlameGutterRun(
            run.FirstLine,
            run.LastLine,
            run.IsEven,
            run.Commit.IsUncommitted ? string.Empty : run.ShortSha,
            run.Commit.AuthorName,
            viewModel.DateOf(run),
            run.Commit.IsUncommitted,
            viewModel.ToolTipOf(run)))];
        BlameGutter.SelectedRun = viewModel.SelectedRun?.Index;
    }

    /// <summary>The gutter is 200 wide and shows the author on a page at least 700 wide, 130 otherwise (4.2).</summary>
    private void FitGutter(double pageWidth)
    {
        if (_viewModel is { } viewModel)
        {
            viewModel.IsWide = pageWidth >= BlameViewModel.WideGutterFromWidth;
            BlameBody.ColumnDefinitions[0].Width = new GridLength(viewModel.GutterWidth);
            BlameGutter.ShowsAuthor = viewModel.IsWide;
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_viewModel is { IsClosed: false } && IsEffectivelyVisible)
                {
                    BlameText.TextArea.Focus(NavigationMethod.Unspecified);
                }
            },
            DispatcherPriority.Loaded);

    private void OnTextPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(BlameText);
        if (point.Properties.IsLeftButtonPressed && BlameText.LineAt(point.Position.Y) is { } line && BlameGutter.RunOfLine(line) is { } run)
        {
            OnRunPressed(run, e.ClickCount);
        }
    }

    /// <summary>A click picks the run (its commit in the right panel, 11.3); a double-click shows it in the graph (4.2).</summary>
    private void OnRunPressed(int index, int clickCount)
    {
        if (_viewModel is not { IsClosed: false } viewModel || index >= viewModel.Runs.Count)
        {
            return;
        }

        var run = viewModel.Runs[index];
        if (clickCount >= 2)
        {
            viewModel.ShowInGraph(run);
        }
        else
        {
            viewModel.Pick(run);
        }
    }

    /// <summary>A run's menu (BD8, BD9): a right-click on the gutter or the text picks the run, then opens it at the pointer; the Menu key at the top of the text, for the picked run.</summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { IsClosed: false } viewModel || !viewModel.ShowsText || ContextMenuRequests.Layer(this) is not { } layer
            || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        BlameRun? run;
        MenuPosition position;
        MenuOpenedBy openedBy;
        if (e.TryGetPosition(BlameGutter, out var inGutter))
        {
            run = BlameGutter.RunAt(inGutter.Y) is { } index ? viewModel.Runs[index] : null;
            e.TryGetPosition(topLevel, out var pointer);
            (position, openedBy) = (new MenuPosition(pointer.X, pointer.Y), MenuOpenedBy.Pointer);
        }
        else
        {
            run = viewModel.SelectedRun;
            var corner = BlameText.TranslatePoint(default, topLevel) ?? default;
            (position, openedBy) = (new MenuPosition(corner.X, corner.Y), MenuOpenedBy.Keyboard);
        }

        if (run is null)
        {
            return;
        }

        viewModel.Pick(run);
        layer.Open(viewModel.MenuFor(run), position, openedBy);
        e.Handled = true;
    }

    /// <summary>Esc (11.1): the page closes, unless a diff covers it or a text box has the focus.</summary>
    private bool HandleEscape()
    {
        if (_viewModel is not { IsClosed: false } viewModel || !IsEffectivelyVisible
            || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return false;
        }

        viewModel.Close();
        return true;
    }
}
