using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Collects the commits <c>git log</c> streams into pages for
/// <see cref="IGitRepository.LoadCommitsAsync"/>, and merges the stashes in on the way (D47, D54).
/// <para>
/// The first page is handed over as soon as it holds <see cref="FirstPageSize"/> commits, so
/// that the graph can draw while the rest loads; later pages hold <see cref="PageSize"/>. A
/// stash goes in just before the first commit that is older than the stash, or before the
/// commit it was made on, whichever comes first: either way it lies above its base, as every
/// commit in <c>--date-order</c> lies above its parents. A commit of the same second as the
/// stash stays above it.
/// </para>
/// <para>
/// A graph that hides or solos branches (docs/phase-5-design.md 8.2, D178) may leave out the
/// commit a stash was made on, and a stash placed by date would then wait at the top for a base
/// that never comes. So for such a graph the pager keeps the commits it knows will be shown: the
/// shown refs' tips and HEAD's given at the start, and the parents of every commit added. A stash
/// whose base is known is placed as above; any other waits and goes in just before its base, when
/// the base comes. One whose base never came is left out (amending D54 while a filter is on).
/// Without a filter nothing changes.
/// </para>
/// <para>
/// Pages are handed over on the thread that adds the commit that fills them, under a lock, so
/// two are never handed over at once. Once <see cref="Finish"/> has run, nothing more is accepted.
/// </para>
/// </summary>
internal sealed class CommitPager
{
    public const int FirstPageSize = 100;
    public const int PageSize = 2000;

    private readonly Lock _gate = new();
    private readonly Action<IReadOnlyList<CommitInfo>> _onPage;
    private readonly List<StashEntry> _pendingStashes;

    /// <summary>For a filtered graph, the commits known to be shown; null without a filter, when every stash's base is.</summary>
    private readonly HashSet<string>? _shown;

    private List<CommitInfo> _page = new(FirstPageSize);
    private int _pageLimit = FirstPageSize;
    private bool _finished;

    /// <param name="stashes">The stashes to merge in, newest first.</param>
    /// <param name="onPage">Receives each full page.</param>
    public CommitPager(IEnumerable<StashEntry> stashes, Action<IReadOnlyList<CommitInfo>> onPage)
        : this(stashes, shownTips: null, onPage)
    {
    }

    /// <param name="stashes">The stashes to merge in, newest first.</param>
    /// <param name="shownTips">
    /// For a graph that hides or solos branches, the commits the shown refs and HEAD point to:
    /// git shows each of them. Null without a filter.
    /// </param>
    /// <param name="onPage">Receives each full page.</param>
    public CommitPager(IEnumerable<StashEntry> stashes, IEnumerable<string>? shownTips, Action<IReadOnlyList<CommitInfo>> onPage)
    {
        _pendingStashes = [.. stashes];
        _onPage = onPage;
        _shown = shownTips is null ? null : new HashSet<string>(shownTips, StringComparer.Ordinal);
    }

    /// <summary>Adds the next commit git printed, after any stash that belongs above it.</summary>
    public void Add(CommitInfo commit)
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            for (var i = 0; i < _pendingStashes.Count;)
            {
                var stash = _pendingStashes[i];
                if (stash.BaseSha == commit.Sha || (stash.Date > commit.CommitDate && IsShown(stash.BaseSha)))
                {
                    _pendingStashes.RemoveAt(i);
                    Append(ToCommit(stash));
                }
                else
                {
                    i++;
                }
            }

            Append(commit);

            // Git shows a commit's parents after it. Once no stash waits, nothing needs them.
            if (_shown is not null && _pendingStashes.Count > 0)
            {
                foreach (var parent in commit.Parents)
                {
                    _shown.Add(parent);
                }
            }
        }
    }

    /// <summary>
    /// Adds the stashes that found no place (their base is not in the history, and they are older
    /// than every commit), or leaves them out when the graph is filtered (their base is not shown),
    /// and hands over the last page, if it holds anything.
    /// </summary>
    public void Finish()
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (_shown is null)
            {
                foreach (var stash in _pendingStashes)
                {
                    Append(ToCommit(stash));
                }
            }

            _pendingStashes.Clear();
            if (_page.Count > 0)
            {
                HandOver();
            }
        }
    }

    /// <summary>Whether git is known to show <paramref name="sha"/>: always without a filter.</summary>
    private bool IsShown(string sha) => _shown is null || _shown.Contains(sha);

    private static CommitInfo ToCommit(StashEntry stash) =>
        new(stash.Sha, [stash.BaseSha], stash.AuthorName, stash.AuthorEmail, stash.AuthorDate, stash.Date, stash.Message, CommitKind.Stash);

    private void Append(CommitInfo commit)
    {
        _page.Add(commit);
        if (_page.Count >= _pageLimit)
        {
            HandOver();
        }
    }

    private void HandOver()
    {
        var page = _page;
        _page = new List<CommitInfo>(PageSize);
        _pageLimit = PageSize;
        _onPage(page);
    }
}
