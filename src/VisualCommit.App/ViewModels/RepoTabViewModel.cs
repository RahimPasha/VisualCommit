using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// One tab (D45): a repository, or the welcome page when it has none. The window's regions show
/// the active tab's left panel, graph area, details and branch.
/// </summary>
public sealed partial class RepoTabViewModel : ObservableObject, IDisposable
{
    public const string EmptyTitle = "New tab";

    private readonly TabServices _services;
    private readonly LeftPanelViewModel _emptyLeftPanel = new();
    private readonly CommitDetailsViewModel _emptyDetails;
    private bool _disposed;

    public RepoTabViewModel(TabServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _emptyDetails = new CommitDetailsViewModel(null, services.Settings, services.Dates, services.Log);
        Welcome = new WelcomeViewModel(this, services);
    }

    /// <summary>The repository's name, or "New tab".</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = EmptyTitle;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>The tab shows a close button: every tab but a lone empty one.</summary>
    [ObservableProperty]
    public partial bool CanClose { get; set; }

    /// <summary>The repository open in this tab, or null for the welcome page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRepository), nameof(ShowsWelcome), nameof(LeftPanel), nameof(Details), nameof(BranchText))]
    public partial RepositoryViewModel? Repository { get; private set; }

    public bool HasRepository => Repository is not null;

    /// <summary>A repository is being opened: the graph area shows neither the welcome page nor a graph yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWelcome))]
    public partial bool IsOpening { get; private set; }

    /// <summary>The graph area shows the welcome page.</summary>
    public bool ShowsWelcome => Repository is null && !IsOpening;

    public WelcomeViewModel Welcome { get; }

    public LeftPanelViewModel LeftPanel => Repository?.LeftPanel ?? _emptyLeftPanel;

    public CommitDetailsViewModel Details => Repository?.Details ?? _emptyDetails;

    /// <summary>What the status bar shows for this tab.</summary>
    public string BranchText => Repository?.BranchText ?? "No repository";

    /// <summary>The working tree of the open repository, or null.</summary>
    public string? RepositoryPath => Repository?.Path;

    /// <summary>The folder of a tab restored from the session that has not been opened yet, or null.</summary>
    public string? PendingPath { get; private set; }

    /// <summary>
    /// The folder of a tab restored from the session that could not be opened (a drive not
    /// mounted yet, git missing), or null. The session keeps it, so the tab comes back on the next
    /// start, until the user opens something else in the tab or closes it.
    /// </summary>
    public string? FailedPath { get; private set; }

    /// <summary>The folder the session keeps for this tab, or null for an empty tab.</summary>
    public string? SavedPath => RepositoryPath ?? PendingPath ?? FailedPath;

    /// <summary>Raised after a repository was opened, initialised or cloned in this tab.</summary>
    public event EventHandler? RepositoryOpened;

    /// <summary>
    /// Shows a tab restored from the session under its folder's name, before git has been found
    /// and the repository can be opened. <see cref="OpenAsync"/> with <see cref="PendingPath"/> opens it.
    /// </summary>
    public void ShowPending(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        PendingPath = path;
        IsOpening = true;
        Title = FolderName(path);
    }

    /// <summary>
    /// Opens the repository at <paramref name="path"/> in this tab. Returns false and shows the
    /// reason on the welcome page when it cannot be opened. A tab restored from the session
    /// passes <paramref name="remember"/> false, so restoring does not reorder the recent list.
    /// </summary>
    public async Task<bool> OpenAsync(string path, bool remember = true)
    {
        var sinceOpening = Stopwatch.StartNew();
        var previousTitle = Title;
        IsOpening = true;
        Title = FolderName(path);
        var restoring = PendingPath is not null;
        try
        {
            var repository = await _services.Repositories.OpenAsync(path);
            if (_disposed)
            {
                // The tab was closed while the repository was being opened.
                return false;
            }

            PendingPath = null;
            IsOpening = false;
            await ShowAsync(repository, sinceOpening, remember);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_disposed)
            {
                return false;
            }

            if (restoring)
            {
                FailedPath = path;
            }

            PendingPath = null;
            IsOpening = false;
            Title = Repository is null ? EmptyTitle : previousTitle;
            Welcome.ShowError(ex is GitException git ? git.StandardError.Trim() : ex.Message);
            if (ex is not NotARepositoryException)
            {
                _services.Log.Error($"Opening {path} failed.", ex);
            }

            return false;
        }
    }

    /// <summary>Shows a repository that was just opened, initialised or cloned, and loads it.</summary>
    public async Task ShowAsync(IGitRepository repository, Stopwatch? sinceOpening = null, bool remember = true)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (_disposed)
        {
            // Closed while the repository was being opened, initialised or cloned.
            return;
        }

        FailedPath = null;
        var previous = Repository;
        var opened = new RepositoryViewModel(repository, _services.Settings, _services.Dates, _services.Log, sinceOpening);
        opened.PropertyChanged += OnRepositoryPropertyChanged;
        Repository = opened;
        Title = repository.Name;
        if (previous is not null)
        {
            previous.PropertyChanged -= OnRepositoryPropertyChanged;
            previous.Dispose();
        }

        if (remember)
        {
            _services.Recent.Add(repository.WorkingDirectory, repository.Name);
        }

        RepositoryOpened?.Invoke(this, EventArgs.Empty);
        await opened.StartAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Welcome.Dispose();
        if (Repository is { } repository)
        {
            repository.PropertyChanged -= OnRepositoryPropertyChanged;
            repository.Dispose();
        }
    }

    private static string FolderName(string path) =>
        System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));

    private void OnRepositoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryViewModel.BranchText))
        {
            OnPropertyChanged(nameof(BranchText));
        }
    }
}
