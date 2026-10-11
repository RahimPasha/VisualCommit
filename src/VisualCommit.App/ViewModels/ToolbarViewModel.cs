using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The toolbar's buttons for one tab (docs/phase-3-design.md 4d, D90): Fetch, Pull, Push, Branch,
/// Stash and Pop act on the tab's repository; a tab without one has a toolbar whose commands are
/// all disabled. A placeholder of work package WP0, so that <see cref="RepoTabViewModel.Toolbar"/>
/// and <see cref="RepositoryViewModel.Toolbar"/> exist: every command is disabled. WP6 owns this
/// file and builds the buttons' behaviour, their enabled states and the tooltips with the reasons.
/// </summary>
public sealed partial class ToolbarViewModel : ObservableObject
{
    /// <param name="repository">The tab's repository, or null for a tab without one.</param>
    public ToolbarViewModel(RepositoryViewModel? repository) => Repository = repository;

    /// <summary>The repository the buttons act on, or null.</summary>
    public RepositoryViewModel? Repository { get; }

    /// <summary>Why Fetch is disabled, or null while it is enabled (its tooltip, <c>ToolTip.ShowOnDisabled</c>).</summary>
    public string? FetchToolTip => null;

    public string? PullToolTip => null;

    public string? PushToolTip => null;

    public string? BranchToolTip => null;

    public string? StashToolTip => null;

    public string? PopToolTip => null;

    private static bool CanAct() => false;

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void Fetch()
    {
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void Pull()
    {
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void Push()
    {
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void Branch()
    {
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void Stash()
    {
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void Pop()
    {
    }
}
