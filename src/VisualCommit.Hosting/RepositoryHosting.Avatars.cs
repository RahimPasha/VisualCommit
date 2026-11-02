using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting;

// The authors' avatars of one repository tab (docs/phase-7a-design.md 7.9; D252, D291, D311,
// ruling 22): the e-mails the graph gives, matched through the repository's map, each looked up
// through a commit of its own when the map lacks it (D339), then the images from the session's
// cache. One run at a time; a call during a run folds into one more with the latest set. GitHub's
// alone (D349, D367; docs/phase-7b-design.md 9.8): only while the listed repository is GitHub's and
// GitHub's account is signed in, of GitHub remotes' authors; else nothing is read or asked.
public sealed partial class RepositoryHosting
{
    // On the UI thread; _avatarGeneration is also read where a run ends.
    private Task? _avatarRun;
    private IReadOnlyList<AuthorCommit>? _avatarNext;
    private int _avatarGeneration;

    // On the UI thread: linked to the tab's lifetime, in each run's token; cancelled and replaced at
    // each StopAvatars, so that a run in flight asks GitHub nothing more (9.8).
    private CancellationTokenSource? _avatarStop;

    /// <summary>The avatars' run in progress, for the tests; read on the UI thread.</summary>
    internal Task AvatarRun => _avatarRun ?? Task.CompletedTask;

    /// <summary>
    /// On the UI thread: the authors of graph commits that a GitHub remote's branch reaches (D311),
    /// each with the newest such commit and that remote's repository (D339), copied, lower-cased and
    /// one per e-mail (the first); an author whose repository is another provider's is left out, and
    /// GitHub is asked about no other address. Unless the listed repository is GitHub's and GitHub's
    /// account is signed in, nothing happens (docs/phase-7b-design.md 9.8).
    /// </summary>
    public void ShowAvatarsFor(IReadOnlyCollection<AuthorCommit> authors)
    {
        ArgumentNullException.ThrowIfNull(authors);
        if (_disposed || !AvatarsWanted)
        {
            return;
        }

        IReadOnlyList<AuthorCommit> set = [.. authors
            .Where(author => author is not null && !string.IsNullOrWhiteSpace(author.Email) && author.Repository?.Provider == HostingProvider.GitHub)
            .Select(author => author with { Email = author.Email.ToLowerInvariant() })
            .DistinctBy(author => author.Email, StringComparer.Ordinal)];
        if (_avatarRun is not null)
        {
            _avatarNext = set;
            return;
        }

        StartAvatars(set);
    }

    /// <summary>The listed repository is GitHub's and GitHub's account is signed in (9.8).</summary>
    private bool AvatarsWanted => _candidate?.Provider == HostingProvider.GitHub && GitHubSignedIn;

    // On the UI thread: GitHub's sign-out (the session cancels the run too), no GitHub repository
    // listed any more, or the tab closed: the run in flight is cancelled, so that GitHub is asked
    // nothing more for it and nothing is saved under avatars/ (D367), and what it still brings is
    // dropped.
    private void StopAvatars()
    {
        DropAvatarRun();
        CancelAvatarRun();
    }

    // On the UI thread, StopAvatars' first half, which Update does before it gives A3 reads up: what
    // the run in flight still brings is dropped, an A3 read of it given up or refused ends it
    // quietly, and the set asked for meanwhile goes.
    private void DropAvatarRun()
    {
        Interlocked.Increment(ref _avatarGeneration);
        _avatarNext = null;
    }

    // On the UI thread, StopAvatars' second half: the run in flight is cancelled.
    private void CancelAvatarRun()
    {
        if (_avatarStop is { } stop)
        {
            _avatarStop = null;
            stop.Cancel();
            stop.Dispose();
        }
    }

    private void DisposeAvatars() => StopAvatars();

    private void StartAvatars(IReadOnlyList<AuthorCommit> set)
    {
        if (_candidate is not { } candidate || set.Count == 0)
        {
            return;
        }

        // Null when the session started nothing (signed out meanwhile, or closed): the next set then
        // starts a run of its own instead of folding into none.
        var generation = _avatarGeneration;
        _avatarStop ??= CancellationTokenSource.CreateLinkedTokenSource(_lifetime);
        _avatarRun = _session.RunAvatarWork(async ct =>
        {
            AuthorAvatars? avatars = null;
            try
            {
                avatars = await ReadAvatarsAsync(candidate, set, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested || generation != Volatile.Read(ref _avatarGeneration))
            {
                // Signed out, stopped or closed, or about to be stopped (its A3 given up or refused
                // first, Update): nothing is applied.
            }
            catch (Exception ex)
            {
                // AvatarCache's body read can still throw from the HTTP stack (a body that does not
                // decompress): the exception by its type and codes alone, never its text (D382).
                _session.Log.Warning($"{_name}: the authors' avatars could not be read ({Http.HostingHttpClient.Describe(ex)}).");
            }

            _session.Post(() => FinishAvatars(generation, avatars));
        }, _avatarStop.Token);
    }

    // On the UI thread: the run's avatars applied while still wanted; then the set asked for meanwhile.
    private void FinishAvatars(int generation, AuthorAvatars? avatars)
    {
        _avatarRun = null;
        if (_disposed)
        {
            return;
        }

        if (avatars is not null && generation == _avatarGeneration && AvatarsWanted)
        {
            Apply(avatars);
        }

        if (_avatarNext is { } next)
        {
            _avatarNext = null;
            if (AvatarsWanted)
            {
                StartAvatars(next);
            }
        }
    }

    // Off the UI thread (7.9 steps 1 to 4): the listed repository and its branch, the map brought
    // up to date by one A6 when anything is due, then the images, four at a time. Null when A3 or
    // A6 failed (logged by the session's call).
    private async Task<AuthorAvatars?> ReadAvatarsAsync(HostedRepository candidate, IReadOnlyList<AuthorCommit> authors, CancellationToken ct)
    {
        IReadOnlyList<string> emails = [.. authors.Select(author => author.Email)];
        var read = await ReadInfoAsync(candidate, ct).ConfigureAwait(false);
        if (read.Value is not { } info)
        {
            return null;
        }

        var listed = ListedOf(candidate, info);
        var branch = info is { IsFork: true, Parent: not null } ? info.ParentDefaultBranch ?? info.DefaultBranch : info.DefaultBranch;
        var maps = _session.AuthorMaps;
        var (commitsDue, due) = maps.Plan(listed, emails);
        if (commitsDue || due.Count > 0)
        {
            // The map stays the listed repository's; each address due is looked up through its own
            // commit, on the remote whose branch reaches it (D339).
            var byEmail = authors.ToDictionary(author => author.Email, StringComparer.Ordinal);
            IReadOnlyList<AuthorCommit> lookups = [.. due.Select(email => byEmail[email])];
            HostingResult<AuthorMap>? answer = null;
            try
            {
                answer = await _session.CallGitHubAsync(
                    $"reading the authors of {listed.FullName}",
                    null,
                    (provider, token, linked) => provider.ReadAuthorsAsync(token, listed, commitsDue ? branch : null, lookups, linked),
                    ct).ConfigureAwait(false);
            }
            finally
            {
                maps.Merge(listed, answer?.Value, commitsDue, due, ct);
            }

            if (!answer.Succeeded)
            {
                return null;
            }
        }

        ct.ThrowIfCancellationRequested();
        var known = maps.Known(listed);
        var cache = _session.AvatarCache;
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        using var four = new SemaphoreSlim(4, 4);
        await Task.WhenAll(emails.Select(async email =>
        {
            if (!known.TryGetValue(email, out var avatar) || avatar is null)
            {
                return;
            }

            await four.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (await cache.FileOfAsync(avatar, ct).ConfigureAwait(false) is { } file)
                {
                    lock (files)
                    {
                        files[email] = file;
                    }
                }
            }
            finally
            {
                four.Release();
            }
        })).ConfigureAwait(false);
        return new AuthorAvatars(files);
    }
}
