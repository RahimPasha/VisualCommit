using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.GitSetup;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The first-run screen's view. "Check again" is disabled while it searches, which drops the
/// keyboard focus: when the search ends with the focus on nothing or on the window, the button
/// takes it again, as by Tab when the button that started the search, "Check again" or "Use the Git
/// on the PATH" ("Use the Git it found"), had the keyboard outline as it was pressed (D462); or, the
/// screen gone, the graph area's focus target takes it. "Choose git.exe…" is disabled while its
/// file dialog is open and its file is probed: a cancelled pick or a refused file starts no search,
/// so the button takes the focus back itself once its run has ended, in the same way. The screen
/// hidden while it holds the focus gives it to the graph area's target too.
/// </summary>
public partial class GitSetupView : UserControl
{
    private GitSetupViewModel? _viewModel;
    private IAsyncRelayCommand? _checkAgain;

    /// <summary>The screen showed when the running search started: its end may give the focus back here.</summary>
    private bool _shownWhenSearchStarted;

    /// <summary>The button that started the search had the keyboard outline as it was pressed: the focus goes back as by Tab.</summary>
    private bool _pressedWithKeys;

    public GitSetupView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch(DataContext as GitSetupViewModel);
        GitSetupCheckAgainButton.Click += (_, _) => _pressedWithKeys = PressedWithKeys(GitSetupCheckAgainButton);

        // Its command drops the setting and always starts a search, whose end reads the flag.
        GitSetupUsePathButton.Click += (_, _) => _pressedWithKeys = PressedWithKeys(GitSetupUsePathButton);
        GitSetupChooseButton.Click += OnChooseClick;
    }

    /// <summary>Runs before the button's command: whether the press came with the keyboard outline (<c>:focus-visible</c>).</summary>
    private static bool PressedWithKeys(Button button) => button.IsFocused && button.Classes.Contains(":focus-visible");

    /// <summary>
    /// Runs before Choose's command: once the run has ended with the focus lost (a cancelled pick, a
    /// refused file), the button takes it back, as by Tab when it had the keyboard outline as it was
    /// pressed. A run that found git hides the button, and the search's end has placed the focus.
    /// </summary>
    private void OnChooseClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        var withKeys = PressedWithKeys(GitSetupChooseButton);
        FocusReturn.AfterRun(
            viewModel.ChooseCommand,
            this,
            () => GitSetupChooseButton,
            withKeys ? NavigationMethod.Tab : NavigationMethod.Unspecified,
            FocusReturn.LostFrom(TopLevel.GetTopLevel(this), GitSetupChooseButton, static () => true));
    }

    private void Watch(GitSetupViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_checkAgain is not null)
        {
            _checkAgain.PropertyChanged -= OnCheckAgainChanged;
        }

        _viewModel = viewModel;
        _checkAgain = viewModel?.CheckAgainCommand;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        if (_checkAgain is not null)
        {
            _checkAgain.PropertyChanged += OnCheckAgainChanged;
        }
    }

    private void OnCheckAgainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IAsyncRelayCommand.IsRunning) || _checkAgain is not { } command)
        {
            return;
        }

        if (command.IsRunning)
        {
            _shownWhenSearchStarted = IsEffectivelyVisible;
            return;
        }

        if (_shownWhenSearchStarted)
        {
            _shownWhenSearchStarted = false;
            var withKeys = _pressedWithKeys;
            _pressedWithKeys = false;
            Dispatcher.UIThread.Post(() => GiveFocusBack(withKeys), DispatcherPriority.Loaded);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitSetupViewModel.IsShown) && _viewModel is { IsShown: false } && IsKeyboardFocusWithin)
        {
            Dispatcher.UIThread.Post(FocusGraphArea, DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// The search ended: with the focus on nothing or the window, "Check again" takes it again, as
    /// by Tab when <paramref name="withKeys"/>, or, the screen gone, the graph area's target.
    /// </summary>
    private void GiveFocusBack(bool withKeys)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is not null && focused is not TopLevel)
        {
            return;
        }

        if (GitSetupCheckAgainButton is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }
            && GitSetupCheckAgainButton.Focus(withKeys ? NavigationMethod.Tab : NavigationMethod.Unspecified))
        {
            return;
        }

        FocusGraphArea();
    }

    private void FocusGraphArea() =>
        this.FindAncestorOfType<CommitGraphView>()?.GraphAreaFocusTarget()?.Focus(NavigationMethod.Unspecified);
}
