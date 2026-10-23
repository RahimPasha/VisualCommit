using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;

namespace VisualCommit.App.Views;

// The window's keys (docs/phase-6-design.md 6.2, 6.3, D192, D213, D214): the router (KeyRouter)
// on the window's two passes, and what the window does for a key or a palette entry that moves
// the focus (IWindowKeyActions): the commit message, the filter, F6's regions.
public partial class MainWindow : IWindowKeyActions
{
    private readonly KeyRegionNavigator _regions = new();
    private KeyRouter? _router;
    private bool _focusFollowed;

    /// <summary>The window's key router (6.3).</summary>
    private KeyRouter Router => _router ??= new KeyRouter(this);

    /// <summary>Focuses <c>CommitSummary</c> once the stage panel is laid out (6.3: the working-changes row is selected first).</summary>
    public void FocusCommitMessage() =>
        Dispatcher.UIThread.Post(
            () => VisibleTextBox("CommitSummary")?.Focus(NavigationMethod.Unspecified),
            DispatcherPriority.Loaded);

    /// <summary>Focuses <c>FilterBox</c>, its text selected (6.1's row 33).</summary>
    public void FocusFilter()
    {
        if (VisibleTextBox("FilterBox") is { } filter && filter.Focus(NavigationMethod.Tab))
        {
            filter.SelectAll();
        }
    }

    /// <summary>F6 and Shift+F6 (6.6).</summary>
    public void MoveFocusToRegion(bool backwards) => _regions.Move(this, FocusManager?.GetFocusedElement(), backwards);

    /// <summary>
    /// The window's data context is set (once, by the session): the window's actions are its
    /// view model's (6.3), and from then on each region remembers its last focused element (6.6).
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (!_focusFollowed)
        {
            _focusFollowed = true;
            AddHandler(GotFocusEvent, (_, args) => _regions.Remember(args.Source), RoutingStrategies.Bubble, handledEventsToo: true);
        }

        if (DataContext is MainWindowViewModel shell)
        {
            shell.WindowActions = this;
        }
    }

    /// <summary>
    /// The window's bubbling pass (phase 5 design 1.3, D163): the toolbar's Undo and Redo by the
    /// platform's keys, after a text box or the conflict resolver had them first; now the router's
    /// (<see cref="KeyRouter.OnBubble"/>). The window's constructor adds it.
    /// </summary>
    private void OnGitUndoOrRedo(object? sender, KeyEventArgs e) => Router.OnBubble(sender, e);

    /// <summary>
    /// The window's tunnelling pass, with <c>handledEventsToo</c>: phase 5's Mod+F, which the
    /// window's constructor adds under this name, is now the router's whole fixed set
    /// (<see cref="KeyRouter.OnTunnel"/>, 6.1, 6.3).
    /// </summary>
    private void OnSearchKey(object? sender, KeyEventArgs e) => Router.OnTunnel(sender, e);

    private TextBox? VisibleTextBox(string name) =>
        this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(box => box.Name == name && box.IsEffectivelyVisible && box.IsEffectivelyEnabled);
}
