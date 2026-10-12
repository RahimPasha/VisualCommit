using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;

namespace VisualCommit.App.Views.Conflicts;

/// <summary>
/// The conflict resolver (docs/phase-4-design.md section 11): see <see cref="ConflictResolverViewModel"/>.
/// One view serves every tab, as the diff's does. The code-behind connects what bindings cannot:
/// the result's document, caret and scroll, kept in the view model while another tab's resolver
/// is shown; the bars' buttons; the resolver's own undo and redo, its keys and Esc (11.10); the
/// double-click guard (11.1); and its menus (11.12).
/// </summary>
public partial class ConflictResolverView : UserControl
{
    private ConflictResolverViewModel? _viewModel;
    private IDisposable? _escape;
    private bool _focusPending;

    public ConflictResolverView()
    {
        InitializeComponent();
        Panes.ChoiceRequested += OnChoiceRequested;

        // Undo and redo are the resolver's own, wherever the focus is inside it (11.5); a press of
        // a double-click that lands on a resolver that just opened is ignored (11.1).
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);

        // Alt with the arrows, in the bubble phase without handledEventsToo: the editor's own uses
        // of these keys win inside the result (11.10).
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        AddHandler(ContextRequestedEvent, OnContextRequested);
        LayoutUpdated += (_, _) => TryFocusPending();
    }

    /// <summary>The result editor (<c>ConflictResult</c>): the test surface of the harnesses.</summary>
    public ConflictResultEditor ResultEditor => Result;

    /// <summary>The ours and theirs panes (<c>ConflictOurs</c>, <c>ConflictTheirs</c>): the test surface of the harnesses.</summary>
    public ConflictPanes ConflictPanes => Panes;

    /// <summary>
    /// Esc (11.10; <see cref="EscapeOrder.Resolver"/>): while the focus is in the result, the
    /// resolver takes Esc and does nothing, so a slip never loses work; otherwise it closes,
    /// asking first when the result is modified. Declines while the resolver is not shown.
    /// </summary>
    public bool HandleEscape()
    {
        if (_viewModel is not { IsOpen: true } viewModel || !IsEffectivelyVisible)
        {
            return false;
        }

        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
            && (focused == Result || focused.GetVisualAncestors().Contains(Result)))
        {
            return true;
        }

        viewModel.CloseResolverCommand.Execute(null);
        return true;
    }

    /// <summary>A control element of its own in UI Automation (<c>ConflictResolver</c>), so that the real-window pass can wait for it.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ConflictResolverAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            _escape = window.AddEscapeHandler(new EscapeHandler(HandleEscape), EscapeOrder.Resolver);
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
        // The document, caret and scroll belong to the view model: one view serves every tab.
        if (_viewModel is { } old)
        {
            Keep(old);
            old.PropertyChanged -= OnViewModelPropertyChanged;
            old.NavigationRequested -= OnNavigationRequested;
        }

        _viewModel = DataContext as ConflictResolverViewModel;
        base.OnDataContextChanged(e);
        if (_viewModel is { } viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.NavigationRequested += OnNavigationRequested;
            ShowDocument(viewModel, restore: true);
        }
        else
        {
            Result.Document = null;
        }
    }

    /// <summary>Keeps the result's caret and both scroll offsets in the view model.</summary>
    private void Keep(ConflictResolverViewModel viewModel)
    {
        if (!ReferenceEquals(Result.Document, viewModel.Document) || viewModel.Document is null)
        {
            return;
        }

        viewModel.CaretOffset = Result.CaretOffset;
        viewModel.ResultScroll = (Result.ScrollOffset.X, Result.ScrollOffset.Y);
        viewModel.PanesScroll = (Panes.ScrollOffset.X, Panes.ScrollOffset.Y);
    }

    /// <summary>
    /// Shows the view model's document in the result: where it was when another tab's resolver
    /// took the view (<paramref name="restore"/>), or, the first time, at the block last reached
    /// with the focus in the result.
    /// </summary>
    private void ShowDocument(ConflictResolverViewModel viewModel, bool restore)
    {
        Result.Document = viewModel.Document;
        Result.MarkerSize = viewModel.MarkerSize;
        if (viewModel.Document is not { } document)
        {
            return;
        }

        Result.TextArea.Caret.Offset = Math.Clamp(viewModel.CaretOffset, 0, document.TextLength);
        if (restore && viewModel.ResultScroll is var (x, y) && viewModel.PanesScroll is var (panesX, panesY))
        {
            Result.ScrollToOffset(new Vector(x, y));
            Panes.ScrollToOffset(new Vector(panesX, panesY));
            return;
        }

        if (viewModel.CurrentBlock is { } block)
        {
            Panes.RevealBlock(block);
            Result.RevealLine(document.GetLineByOffset(Result.CaretOffset).LineNumber);
        }

        RequestFocus();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ConflictResolverViewModel viewModel && e.PropertyName == nameof(ConflictResolverViewModel.Document)
            && !ReferenceEquals(Result.Document, viewModel.Document))
        {
            ShowDocument(viewModel, restore: false);
        }
    }

    /// <summary>The view model reached a block, made a choice or read a file: the caret moves, the panes and the result scroll (11.5).</summary>
    private void OnNavigationRequested(object? sender, ConflictNavigation navigation)
    {
        if (!ReferenceEquals(sender, _viewModel) || Result.Document is not { } document)
        {
            return;
        }

        Result.TextArea.ClearSelection();
        Result.TextArea.Caret.Offset = Math.Clamp(navigation.CaretOffset, 0, document.TextLength);
        if (navigation.RevealBlock is { } block)
        {
            Panes.RevealBlock(block);
        }

        if (navigation.RevealLine is { } line)
        {
            Result.RevealLine(line);
        }
        else
        {
            Result.TextArea.Caret.BringCaretToView();
        }

        if (navigation.FocusResult)
        {
            RequestFocus();
        }
    }

    /// <summary>A bar's button: the view model replaces the block; the focus goes to the result without a focus outline (11.5).</summary>
    private void OnChoiceRequested(object? sender, ConflictChoiceEventArgs e) => _viewModel?.Choose(e.Block, e.Choice);

    /// <summary>Gives the result the keyboard focus, now or once it is shown.</summary>
    private void RequestFocus()
    {
        _focusPending = true;
        TryFocusPending();
    }

    private void TryFocusPending()
    {
        if (!_focusPending || _viewModel is not { ShowsText: true } || !Result.IsEffectivelyVisible || !(Result.Bounds.Height > 0))
        {
            return;
        }

        _focusPending = false;
        Result.TextArea.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>The platform's undo and redo (Avalonia's <c>HotkeyConfiguration</c>), taken before the editor sees them, wherever the focus is in the resolver (11.5).</summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _viewModel is not { ShowsText: true } viewModel || this.GetPlatformSettings()?.HotkeyConfiguration is not { } keys)
        {
            return;
        }

        if (keys.Undo.Any(gesture => gesture.Matches(e)))
        {
            e.Handled = true;
            Step(viewModel, undo: true);
        }
        else if (keys.Redo.Any(gesture => gesture.Matches(e)))
        {
            e.Handled = true;
            Step(viewModel, undo: false);
        }
    }

    private void Step(ConflictResolverViewModel viewModel, bool undo)
    {
        if (undo ? viewModel.Undo() : viewModel.Redo())
        {
            Result.TextArea.Caret.BringCaretToView();
        }
    }

    /// <summary>For <see cref="ConflictResolverViewModel.DoubleClickGuard"/> after opening, a press that is the second of a double-click is ignored (11.1).</summary>
    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount >= 2 && _viewModel is { IsInDoubleClickGuard: true })
        {
            e.Handled = true;
        }
    }

    /// <summary>Alt+Up and Alt+Down: the previous and next conflict; Alt+Left and Alt+Right: the previous and next file (11.10).</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _viewModel is not { IsOpen: true } viewModel || e.KeyModifiers != KeyModifiers.Alt)
        {
            return;
        }

        var command = e.Key switch
        {
            Key.Up => viewModel.PreviousConflictCommand,
            Key.Down => viewModel.NextConflictCommand,
            Key.Left => viewModel.PreviousFileCommand,
            Key.Right => (System.Windows.Input.ICommand)viewModel.NextFileCommand,
            _ => null,
        };

        if (command is null)
        {
            return;
        }

        e.Handled = true;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>
    /// The resolver's menus (11.12): the header opens the conflicted file's menu, the result its
    /// own [Undo, Redo] — [Cut, Copy, Paste, Select all], a pane [Copy].
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { IsOpen: true } viewModel || ContextMenuRequests.Layer(this) is not { } layer)
        {
            return;
        }

        MenuModel? menu = null;
        Visual corner = this;
        if (Within(e.Source, Result))
        {
            corner = ResultTitleStrip;
            menu = ConflictMenus.ForConflictResult(
                viewModel.FileName,
                new ConflictResultMenuState(viewModel.CanUndo, viewModel.CanRedo, !Result.TextArea.Selection.IsEmpty, viewModel.IsBusy),
                new ConflictResultMenuActions(
                    () => Step(viewModel, undo: true),
                    () => Step(viewModel, undo: false),
                    Result.Cut,
                    Result.Copy,
                    Result.Paste,
                    Result.SelectAll));
        }
        else if (Panes.SideOf(e.Source) is { } side)
        {
            corner = side == ConflictPaneSide.Ours ? OursTitleStrip : TheirsTitleStrip;
            menu = ConflictMenus.ForConflictPane(
                side == ConflictPaneSide.Ours ? viewModel.OursTitle : viewModel.TheirsTitle,
                Panes.HasSelection(side),
                () => Panes.Copy(side));
        }
        else if (Within(e.Source, ResolverHeader))
        {
            corner = ResolverHeader;
            menu = viewModel.FileMenu?.Invoke(new ChangedFile(viewModel.Path, FileChangeKind.Conflicted));
        }

        if (menu is null)
        {
            return;
        }

        var (position, openedBy) = ContextMenuRequests.Where(e, corner);
        layer.Open(menu, position, openedBy);
        e.Handled = true;
    }

    private static bool Within(object? source, Visual container) =>
        source is Visual visual && (visual == container || visual.GetVisualAncestors().Contains(container));

    private sealed class ConflictResolverAutomationPeer(ConflictResolverView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
