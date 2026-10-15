using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Session;

namespace VisualCommit.App.Services;

/// <summary>
/// What each repository's graph hides and solos, kept in the session (docs/phase-5-design.md 8.4,
/// D159, D178): <see cref="SessionState.GraphFilters"/>, keyed by the repository's working tree as
/// the tabs and the recent list are, apart from the tabs so that closing a tab keeps it, the
/// <see cref="SessionState.MaxGraphFilters"/> most recently used first. An entry that hides and
/// solos nothing is removed when it is written (FD14), and one whose folder no longer exists when
/// the app starts (<see cref="DropMissingFolders"/>). Written whenever a filter changes.
/// </summary>
public sealed class GraphFilterStore
{
    private readonly ISessionStore _session;
    private readonly TimeProvider _clock;

    public GraphFilterStore(ISessionStore session, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The filter of the repository at <paramref name="repositoryPath"/>, or null when it hides and solos nothing.</summary>
    public RepositoryGraphFilter? Find(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        return (_session.Current.GraphFilters ?? []).FirstOrDefault(entry => entry is not null && SamePath(entry.RepositoryPath, repositoryPath));
    }

    /// <summary>
    /// Writes <paramref name="filter"/> as the repository's, used now, and returns it as kept (null
    /// when it hides and solos nothing, which removes the entry, FD14). The list keeps the most
    /// recently used <see cref="SessionState.MaxGraphFilters"/>.
    /// </summary>
    public RepositoryGraphFilter? Write(string repositoryPath, RepositoryGraphFilter? filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        var kept = GraphFilterChanges.IsEmpty(filter) ? null : filter! with { RepositoryPath = repositoryPath, LastUsed = _clock.GetUtcNow() };
        _session.Update(state =>
        {
            var others = (state.GraphFilters ?? []).Where(entry => entry is not null && !SamePath(entry.RepositoryPath, repositoryPath));
            IEnumerable<RepositoryGraphFilter> entries = kept is null ? others : others.Append(kept);
            var bounded = entries.OrderByDescending(entry => entry.LastUsed).Take(SessionState.MaxGraphFilters).ToList();
            return state.GraphFilters is not null && bounded.SequenceEqual(state.GraphFilters) ? state : state with { GraphFilters = bounded };
        });
        return kept;
    }

    /// <summary>The repository was opened with a filter: it counts as used now, for the bound (8.4).</summary>
    public RepositoryGraphFilter? Touch(string repositoryPath) =>
        Find(repositoryPath) is { } filter ? Write(repositoryPath, filter) : null;

    /// <summary>At start-up (8.4): the entries whose folder no longer exists, and any left empty, are dropped.</summary>
    public void DropMissingFolders() =>
        _session.Update(state =>
        {
            var kept = (state.GraphFilters ?? [])
                .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.RepositoryPath) && !GraphFilterChanges.IsEmpty(entry) && Directory.Exists(entry.RepositoryPath))
                .ToList();
            return state.GraphFilters is not null && kept.Count == state.GraphFilters.Count ? state : state with { GraphFilters = kept };
        });

    /// <summary>Whether two paths name one working tree, as the recent list compares them.</summary>
    public static bool SamePath(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
