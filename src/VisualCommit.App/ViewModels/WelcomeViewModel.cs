using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The welcome page of a tab without a repository (D42): Open, Clone and Init, the clone form
/// with its progress, and the recent repositories.
/// </summary>
public sealed partial class WelcomeViewModel : ObservableObject, IDisposable
{
    private readonly RepoTabViewModel _tab;
    private readonly TabServices _services;
    private CancellationTokenSource? _clone;
    private bool _nameTyped;
    private bool _settingName;

    public WelcomeViewModel(RepoTabViewModel tab, TabServices services)
    {
        _tab = tab;
        _services = services;
        Recent.CollectionChanged += OnRecentChanged;
    }

    /// <summary>The recent repositories, newest first. Shared by every tab.</summary>
    public ObservableCollection<RecentRepositoryItem> Recent => _services.Recent.Items;

    public bool HasRecent => Recent.Count > 0;

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

    /// <summary>Shows a failure under the buttons.</summary>
    public void ShowError(string message) => ErrorText = message;

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await _services.Folders.PickFolderAsync("Open a repository");
        if (path is null)
        {
            return;
        }

        ErrorText = null;
        await _tab.OpenAsync(path);
    }

    [RelayCommand]
    private async Task InitAsync()
    {
        var path = await _services.Folders.PickFolderAsync("Choose a folder for the new repository");
        if (path is null)
        {
            return;
        }

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

        try
        {
            var repository = await _services.Repositories.CloneAsync(url, System.IO.Path.Combine(parent, name), progress, _clone.Token);
            IsCloning = false;
            IsCloneFormOpen = false;
            await _tab.ShowAsync(repository);
        }
        catch (OperationCanceledException)
        {
            ErrorText = "Clone cancelled.";
        }
        catch (Exception ex)
        {
            if (ex is not GitException)
            {
                _services.Log.Error($"Cloning {url} failed.", ex);
            }

            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
        }
        finally
        {
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
}
