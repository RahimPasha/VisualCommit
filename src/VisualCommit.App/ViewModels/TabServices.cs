using System.Collections.ObjectModel;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>What every repo tab uses: the app's services and the shared list of recent repositories.</summary>
public sealed class TabServices
{
    public TabServices(
        IRepositoryProvider repositories,
        IFolderPicker folders,
        ISettingsStore settings,
        ISessionStore session,
        DateDisplay dates,
        IAppLog? log = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dates);
        Repositories = repositories;
        Folders = folders;
        Settings = settings;
        Dates = dates;
        Log = log ?? NullAppLog.Instance;
        Recent = new RecentRepositories(session, clock ?? TimeProvider.System);
    }

    public IRepositoryProvider Repositories { get; }

    public IFolderPicker Folders { get; }

    public ISettingsStore Settings { get; }

    public DateDisplay Dates { get; }

    public IAppLog Log { get; }

    public RecentRepositories Recent { get; }
}

/// <summary>A repository in the welcome page's recent list.</summary>
/// <param name="Path">Its working tree.</param>
/// <param name="Name">Its folder's name.</param>
public sealed record RecentRepositoryItem(string Path, string Name);

/// <summary>
/// The recently opened repositories (D42, D46), newest first and at most
/// <see cref="SessionState.MaxRecent"/>, shared by every tab's welcome page and kept in the
/// session file.
/// </summary>
public sealed class RecentRepositories
{
    private readonly ISessionStore _session;
    private readonly TimeProvider _clock;

    public RecentRepositories(ISessionStore session, TimeProvider clock)
    {
        _session = session;
        _clock = clock;
        foreach (var recent in session.Current.Recent.Take(SessionState.MaxRecent))
        {
            Items.Add(new RecentRepositoryItem(recent.Path, recent.Name));
        }
    }

    /// <summary>The list the welcome pages show.</summary>
    public ObservableCollection<RecentRepositoryItem> Items { get; } = [];

    /// <summary>Puts a repository at the top of the list, removing an older entry for the same folder, and saves the list.</summary>
    public void Add(string path, string name)
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (SamePath(Items[i].Path, path))
            {
                Items.RemoveAt(i);
            }
        }

        Items.Insert(0, new RecentRepositoryItem(path, name));
        while (Items.Count > SessionState.MaxRecent)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        var now = _clock.GetUtcNow();
        _session.Update(state => state with
        {
            Recent = [.. Items.Select(item => new RecentRepository(item.Path, item.Name, SamePath(item.Path, path)
                ? now
                : state.Recent.FirstOrDefault(old => SamePath(old.Path, item.Path))?.LastOpened ?? now))],
        });
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(
            System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(a)),
            System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(b)),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
