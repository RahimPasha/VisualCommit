using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting;

// The authors' avatars of one repository tab (docs/phase-7a-design.md 7.9; D252, D291, D311,
// ruling 22): the e-mails the graph gives, matched through the repository's map, then the images
// from the session's cache. One run at a time; a call during a run folds into one more with the
// latest set. Signed out, nothing is read or asked.
public sealed partial class RepositoryHosting
{
    // On the UI thread.
    private Task? _avatarRun;
    private IReadOnlyList<string>? _avatarNext;
    private int _avatarGeneration;

    /// <summary>The avatars' run in progress, for the tests; read on the UI thread.</summary>
    internal Task AvatarRun => _avatarRun ?? Task.CompletedTask;

    /// <summary>
    /// On the UI thread: the authors of graph commits that a GitHub remote's branch reaches (D311),
    /// copied and lower-cased; GitHub is asked about no other address. Signed out, nothing happens.
    /// </summary>
    public void ShowAvatarsFor(IReadOnlyCollection<string> emails)
    {
        ArgumentNullException.ThrowIfNull(emails);
        if (_disposed || !IsSignedIn)
        {
            return;
        }

        IReadOnlyList<string> set = [.. emails.Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email.ToLowerInvariant()).Distinct(StringComparer.Ordinal)];
        if (_avatarRun is not null)
        {
            _avatarNext = set;
            return;
        }

        StartAvatars(set);
    }

    // On the UI thread: a sign-out (the session cancels the run itself); what a run still brings is dropped.
    private void StopAvatars()
    {
        _avatarGeneration++;
        _avatarNext = null;
    }

    private void DisposeAvatars() => StopAvatars();

    private void StartAvatars(IReadOnlyList<string> set)
    {
        if (_candidate is not { } candidate || set.Count == 0)
        {
            return;
        }

        var generation = _avatarGeneration;
        _avatarRun = _session.RunAvatarWork(async ct =>
        {
            AuthorAvatars? avatars = null;
            try
            {
                avatars = await ReadAvatarsAsync(candidate, set, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Signed out or closed: nothing is applied.
            }
            catch (Exception ex)
            {
                _session.Log.Warning($"{_name}: the authors' avatars could not be read: {ex.Message}");
            }

            _session.Post(() => FinishAvatars(generation, avatars));
        }, _lifetime);
    }

    // On the UI thread: the run's avatars applied while still wanted; then the set asked for meanwhile.
    private void FinishAvatars(int generation, AuthorAvatars? avatars)
    {
        _avatarRun = null;
        if (_disposed)
        {
            return;
        }

        if (avatars is not null && generation == _avatarGeneration && IsSignedIn)
        {
            Apply(avatars);
        }

        if (_avatarNext is { } next)
        {
            _avatarNext = null;
            if (IsSignedIn)
            {
                StartAvatars(next);
            }
        }
    }

    // Off the UI thread (7.9 steps 1 to 4): the listed repository and its branch, the map brought
    // up to date by one A6 when anything is due, then the images, four at a time. Null when A3 or
    // A6 failed (logged by the session's call).
    private async Task<AuthorAvatars?> ReadAvatarsAsync(HostedRepository candidate, IReadOnlyList<string> emails, CancellationToken ct)
    {
        var read = await ReadInfoAsync(candidate, ct).ConfigureAwait(false);
        if (read.Value is not { } info)
        {
            return null;
        }

        var (listed, branch) = info is { IsFork: true, Parent: { } parent }
            ? (parent, info.ParentDefaultBranch ?? info.DefaultBranch)
            : (candidate, info.DefaultBranch);
        var maps = _session.AuthorMaps;
        var (commitsDue, due) = maps.Plan(listed, emails);
        if (commitsDue || due.Count > 0)
        {
            HostingResult<AuthorMap>? answer = null;
            try
            {
                answer = await _session.CallAsync(
                    $"reading the authors of {listed.FullName}",
                    (provider, token, linked) => provider.ReadAuthorsAsync(token, listed, commitsDue ? branch : null, due, linked),
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
