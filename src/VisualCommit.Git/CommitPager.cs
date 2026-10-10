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
    private List<CommitInfo> _page = new(FirstPageSize);
    private int _pageLimit = FirstPageSize;
    private bool _finished;

    /// <param name="stashes">The stashes to merge in, newest first.</param>
    /// <param name="onPage">Receives each full page.</param>
    public CommitPager(IEnumerable<StashEntry> stashes, Action<IReadOnlyList<CommitInfo>> onPage)
    {
        _pendingStashes = [.. stashes];
        _onPage = onPage;
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
                if (stash.Date > commit.CommitDate || stash.BaseSha == commit.Sha)
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
        }
    }

    /// <summary>
    /// Adds the stashes that found no place (their base is not in the history, and they are older
    /// than every commit) and hands over the last page, if it holds anything.
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
            foreach (var stash in _pendingStashes)
            {
                Append(ToCommit(stash));
            }

            _pendingStashes.Clear();
            if (_page.Count > 0)
            {
                HandOver();
            }
        }
    }

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
