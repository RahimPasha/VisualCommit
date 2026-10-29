using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The welcome page of a tab without a repository (D42): Open, Clone and Init, the clone form
/// with its progress, the workspaces (docs/phase-6-design.md 13.1, D188) and the recent
/// repositories.
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject, IDisposable
{
    private readonly RepoTabViewModel _tab;
    private readonly TabServices _services;
    private CancellationTokenSource? _clone;
    private Task? _cloneTask;
    private bool _nameTyped;
    private bool _settingName;

    public WelcomeViewModel(RepoTabViewModel tab, TabServices services)
    {
        _tab = tab;
        _services = services;
        Recent.CollectionChanged += OnRecentChanged;
        Workspaces.CollectionChanged += OnWorkspacesChanged;
        _services.Workspaces.Changed += OnWorkspaceStoreChanged;
    }

    /// <summary>The recent repositories, newest first. Shared by every tab.</summary>
    public ObservableCollection<RecentRepositoryItem> Recent => _services.Recent.Items;

    public bool HasRecent => Recent.Count > 0;

    /// <summary>The workspaces, by name (13.1). Shared by every tab, as <see cref="Recent"/> is.</summary>
    public ObservableCollection<WorkspaceItem> Workspaces => _services.WorkspaceItems;

    /// <summary>The list shows; <c>No workspaces</c> shows while it is empty.</summary>
    public bool HasWorkspaces => Workspaces.Count > 0;

    /// <summary>What the page says about <c>workspaces.json</c> (1.3): a file set aside, one that could not be read, a newer version's, a failed save; or null.</summary>
    public string? WorkspacesNotice => _services.Workspaces.Notice;

    public bool HasWorkspacesNotice => !string.IsNullOrEmpty(WorkspacesNotice);

    /// <summary>The notice is a failed save's, in the warning tone (13.1); the others are secondary.</summary>
    public bool IsWorkspacesNoticeWarning => HasWorkspacesNotice && _services.Workspaces.SaveFailed;

    /// <summary>Workspaces can be created, renamed and deleted (the store is not read-only, 1.5).</summary>
    public bool CanEditWorkspaces => WorkspacesEditReason is null;

    /// <summary>Why they cannot: the store's read-only reason, or null.</summary>
    public string? WorkspacesEditReason => _services.WorkspaceCommands.EditReason;

    /// <summary><c>NewWorkspaceButton</c>'s tooltip and help text: what it does, or the read-only reason while disabled.</summary>
    public string NewWorkspaceToolTip => WorkspacesEditReason ?? WorkspaceTexts.NewWorkspaceToolTip;

    /// <summary>A workspace item's menu (13.1): <c>Open</c>, <c>Rename workspace…</c>, <c>Delete workspace…</c>.</summary>
    public MenuModel MenuFor(WorkspaceItem item) => WorkspaceMenus.ForWelcomeItem(item, _services.WorkspaceCommands, _tab);

    /// <summary>Why the last Open, Init or Clone failed, in git's words where git said it; or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; private set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>The clone form shows instead of the buttons and the recent list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsButtons))]
    public partial bool IsCloneFormOpen { get; private set; }

    public bool ShowsButtons => !IsCloneFormOpen;

    [ObservableProperty]
    public partial string CloneUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CloneParent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CloneName { get; set; } = string.Empty;

    /// <summary>A clone is running: the fields are disabled and the progress shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditClone))]
    [NotifyCanExecuteChangedFor(nameof(StartCloneCommand), nameof(CloseCloneFormCommand))]
    public partial bool IsCloning { get; private set; }

    public bool CanEditClone => !IsCloning;

    /// <summary>The current stage's percentage, 0 to 100.</summary>
    [ObservableProperty]
    public partial double CloneProgressValue { get; private set; }

    /// <summary>The stage and percentage as git reports them, such as "Receiving objects 45%".</summary>
    [ObservableProperty]
    public partial string CloneProgressText { get; private set; } = string.Empty;

    /// <summary>The clone that is running, or null: the app waits for it to clean up when it closes.</summary>
    public Task? RunningClone => _cloneTask is { IsCompleted: false } running ? running : null;

    /// <summary>Shows a failure under the buttons.</summary>
    public void ShowError(string message) => ErrorText = message;

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await _services.Folders.PickFolderAsync(OpenFolderTitle);
        if (path is null)
        {
            return;
        }

        // As Open repository… and its key (5.10, D215: the button's tooltip names that key): the tab
        // that already shows the folder is activated, and nothing opens it again.
        if (_services.ActivateTabShowing?.Invoke(path) == true)
        {
            return;
        }

        ErrorText = null;
        await _tab.OpenAsync(path);
    }

    /// <summary>The folder dialog's title of Open (D42), which the palette's and the key's Open ask with too (docs/phase-6-design.md 5.10).</summary>
    public const string OpenFolderTitle = "Open a repository";

    /// <summary>The folder dialog's title of Init (D42), which the palette's and the key's Init ask with too (docs/phase-6-design.md 5.10).</summary>
    public const string InitFolderTitle = "Choose a folder for the new repository";

    [RelayCommand]
    private async Task InitAsync()
    {
        var path = await _services.Folders.PickFolderAsync(InitFolderTitle);
        if (path is null)
        {
            return;
        }

        await InitAtAsync(path);
    }

    /// <summary>
    /// Makes a repository in <paramref name="path"/> and shows it in this page's tab: Init after
    /// its folder dialog, which the palette's and the key's Init call with their own dialog's answer
    /// (docs/phase-6-design.md 5.10). A failure shows under the buttons.
    /// </summary>
    public async Task InitAtAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ErrorText = null;
        try
        {
            var repository = await _services.Repositories.InitAsync(path);
            await _tab.ShowAsync(repository);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _services.Log.Error($"Initialising a repository in {path} failed.", ex);
            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(RecentRepositoryItem? item)
    {
        if (item is null)
        {
            return;
        }

        ErrorText = null;
        await _tab.OpenAsync(item.Path);
    }

    /// <summary><c>New workspace…</c> (1.6): the name dialog, then the workspace's dashboard takes this page's tab (2.2).</summary>
    [RelayCommand(CanExecute = nameof(CanEditWorkspaces))]
    private Task NewWorkspaceAsync() => _services.WorkspaceCommands.CreateAsync(_tab);

    /// <summary>A click, Enter or Space on a workspace item opens it from this page's tab (D188, 2.2).</summary>
    [RelayCommand]
    private void OpenWorkspace(WorkspaceItem? item)
    {
        if (item is not null)
        {
            _services.WorkspaceCommands.Open(item.Id, _tab);
        }
    }

    [RelayCommand]
    private void OpenCloneForm()
    {
        ErrorText = null;
        IsCloneFormOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanEditClone))]
    private void CloseCloneForm()
    {
        ErrorText = null;
        IsCloneFormOpen = false;
    }

    [RelayCommand]
    private async Task BrowseCloneParentAsync()
    {
        var path = await _services.Folders.PickFolderAsync("Choose the folder to clone into");
        if (path is not null)
        {
            CloneParent = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditClone))]
    private async Task StartCloneAsync()
    {
        var url = CloneUrl.Trim();
        var parent = CloneParent.Trim();
        var name = CloneName.Trim();
        if (url.Length == 0 || parent.Length == 0 || name.Length == 0)
        {
            ErrorText = "Fill in the repository URL, the parent folder and the folder name.";
            return;
        }

        ErrorText = null;
        IsCloning = true;
        CloneProgressValue = 0;
        CloneProgressText = "Starting";
        _clone = new CancellationTokenSource();
        var progress = new Progress<CloneProgress>(report =>
        {
            if (report.Percent is { } percent)
            {
                CloneProgressValue = percent;
                CloneProgressText = $"{report.Stage} {percent}%";
            }
            else
            {
                CloneProgressText = report.Stage;
            }
        });

        // Git's and ssh's prompts for the clone come to the app's dialogs (phase 3 design 3.3); a
        // clone runs outside any operation queue, so it opens its own prompt scope.
        var prompts = _services.Prompts.Begin("Clone", name, PromptMode.Ask, bindings: _services.Hosting.BindingsForClone(url));
        try
        {
            var cloning = _services.Repositories.CloneAsync(url, System.IO.Path.Combine(parent, name), progress, _clone.Token, prompts.Environment);
            _cloneTask = cloning;
            var repository = await cloning;
            IsCloning = false;
            IsCloneFormOpen = false;
            await _tab.ShowAsync(repository);
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Clone cancelled.";
        }
        catch (GitException) when (prompts.UserCancelled)
        {
            // The user cancelled a prompt, which made git fail: that is a cancel, not an error.
            ErrorText = "Clone cancelled.";
        }
        catch (Exception ex)
        {
            if (ex is not GitException)
            {
                _services.Log.Error($"Cloning {GitCommand.HideCredentials(url)} failed.", ex);
            }

            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
        }
        finally
        {
            prompts.Dispose();
            IsCloning = false;
            _clone.Dispose();
            _clone = null;
        }
    }

    [RelayCommand]
    private void CancelClone() => _clone?.Cancel();

    public void Dispose()
    {
        Recent.CollectionChanged -= OnRecentChanged;
        Workspaces.CollectionChanged -= OnWorkspacesChanged;
        _services.Workspaces.Changed -= OnWorkspaceStoreChanged;
        _clone?.Cancel();
    }

    partial void OnCloneUrlChanged(string value)
    {
        if (_nameTyped)
        {
            return;
        }

        _settingName = true;
        CloneName = NameFromUrl(value);
        _settingName = false;
    }

    partial void OnCloneNameChanged(string value)
    {
        if (!_settingName)
        {
            _nameTyped = value.Length > 0;
        }
    }

    /// <summary>The last part of a URL or path, without ".git": what git itself would name the folder.</summary>
    public static string NameFromUrl(string url)
    {
        var trimmed = url.Trim().TrimEnd('/', '\\');
        var last = trimmed[(trimmed.LastIndexOfAny(['/', '\\', ':']) + 1)..];
        return last.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? last[..^4] : last;
    }

    private void OnRecentChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasRecent));

    private void OnWorkspacesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasWorkspaces));

    /// <summary>A change of the store can set or clear its notice (a failed save, then one that succeeded, 1.3).</summary>
    private void OnWorkspaceStoreChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(WorkspacesNotice));
        OnPropertyChanged(nameof(HasWorkspacesNotice));
        OnPropertyChanged(nameof(IsWorkspacesNoticeWarning));
    }
}
