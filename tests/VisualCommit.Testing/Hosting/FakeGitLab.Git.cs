using System.Globalization;
using System.Text;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake's git work (docs/phase-7b-design.md 4.5): the ref sync of merge requests' heads (D267's
/// pattern with GitLab's ref), the branches the create checks and the branch routes read, and a
/// branch's delete. Git runs outside the fake's lock, isolated from the machine.
/// </summary>
public sealed partial class FakeGitLab
{
    /// <summary>
    /// Brings <paramref name="target"/>'s <c>refs/merge-requests/&lt;iid&gt;/head</c> in step with its
    /// merge requests (4.5), as the fake does before <c>mrs</c>, <c>mr</c>, <c>create</c> and git's ref
    /// listing: an open one's at its source branch's tip, by one <c>update-ref --stdin</c> for those in
    /// the project itself and one fetch per fork (a fork's head ref is the target's, MEASURED r6 4.2); a
    /// deleted one's removed (ASSUMED); a closed or merged one's kept, as is an open one's whose source
    /// branch is gone. No <c>/merge</c>, which gitlab.com has only once computed (MEASURED r6 4.2).
    /// </summary>
    public async Task SyncRefsAsync(FakeGitLabProject target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.BarePath is null || target.Folder is null)
        {
            return;
        }

        var gate = LockOf(target.Folder);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SyncLockedAsync(target, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary><see cref="SyncRefsAsync"/> under the target's lock; returns the target's branches as read before it.</summary>
    private async Task<IReadOnlyDictionary<string, string>> SyncLockedAsync(FakeGitLabProject target, CancellationToken ct)
    {
        var bare = target.BarePath!;
        var refs = await ReadRefsAsync(bare, ct, "refs/heads/", "refs/merge-requests/").ConfigureAwait(false);
        // Each request's state is read once, under the lock: a PUT or DELETE that lands while git runs
        // (they take the lock alone, not the target's) waits for the next sync instead of meeting a
        // fork whose heads were never read.
        List<(FakeMergeRequest Request, bool Open, bool Deleted)> requests;
        lock (Gate)
        {
            requests = [.. _mergeRequests.Where(request => request.Target == target).Select(request => (request, request.State == "opened", request.Deleted))];
        }

        var forkHeads = new Dictionary<FakeGitLabProject, IReadOnlyDictionary<string, string>>();
        foreach (var fork in requests.Where(entry => !entry.Deleted && entry.Open && entry.Request.Source != target).Select(entry => entry.Request.Source).Distinct())
        {
            forkHeads[fork] = fork.BarePath is { } forkBare ? await ReadRefsAsync(forkBare, ct, "refs/heads/").ConfigureAwait(false) : new Dictionary<string, string>();
        }

        var updates = new StringBuilder();
        var fetches = new Dictionary<FakeGitLabProject, List<string>>();
        var gone = new List<FakeMergeRequest>();
        foreach (var (request, open, deleted) in requests)
        {
            var headRef = HeadRef(request.Iid);
            refs.TryGetValue(headRef, out var existing);
            if (deleted)
            {
                if (existing is not null)
                {
                    updates.Append(CultureInfo.InvariantCulture, $"delete {headRef} {existing}\n");
                }

                gone.Add(request);
                continue;
            }

            refs.TryGetValue("refs/heads/" + request.TargetBranch, out var targetTip);
            string? head = null;
            if (open)
            {
                var heads = request.Source == target ? refs : forkHeads[request.Source];
                heads.TryGetValue("refs/heads/" + request.SourceBranch, out head);
            }

            if (head is not null && head != existing)
            {
                if (request.Source == target)
                {
                    updates.Append(CultureInfo.InvariantCulture, $"update {headRef} {head}\n");
                }
                else
                {
                    if (!fetches.TryGetValue(request.Source, out var refspecs))
                    {
                        fetches.Add(request.Source, refspecs = []);
                    }

                    refspecs.Add($"+refs/heads/{request.SourceBranch}:{headRef}");
                }
            }

            lock (Gate)
            {
                request.HeadSha = head ?? existing;
                request.TargetSha = targetTip;
            }
        }

        if (updates.Length > 0)
        {
            await GitOkAsync(bare, updates.ToString(), ct, "update-ref", "--stdin").ConfigureAwait(false);
        }

        foreach (var (fork, refspecs) in fetches)
        {
            if (fork.BarePath is { } forkBare)
            {
                await GitOkAsync(bare, null, ct, ["-c", "gc.auto=0", "fetch", "--quiet", "--no-tags", forkBare, .. refspecs]).ConfigureAwait(false);
            }
        }

        lock (Gate)
        {
            _mergeRequests.RemoveAll(gone.Contains);
        }

        return refs;
    }

    /// <summary>A project's branches (<c>refs/heads/</c>), each with its tip and subject, by name: one <c>for-each-ref</c>; none without a bare repository.</summary>
    private async Task<IReadOnlyList<(string Name, string Sha, string Subject)>> BranchesAsync(FakeGitLabProject project, CancellationToken ct)
    {
        if (project.BarePath is null)
        {
            return [];
        }

        var listed = await GitOkAsync(project.BarePath, null, ct, "for-each-ref", "--format=%(objectname)%00%(refname)%00%(subject)", "refs/heads/").ConfigureAwait(false);
        var branches = new List<(string Name, string Sha, string Subject)>();
        foreach (var line in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\0');
            if (fields.Length >= 3 && fields[1].StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                branches.Add((fields[1]["refs/heads/".Length..], fields[0], fields[2]));
            }
        }

        return [.. branches.OrderBy(branch => branch.Name, StringComparer.Ordinal)];
    }

    /// <summary>Whether <paramref name="project"/> has the branch <paramref name="name"/>.</summary>
    private async Task<bool> HasBranchAsync(FakeGitLabProject project, string name, CancellationToken ct) =>
        project.BarePath is not null && (await ReadRefsAsync(project.BarePath, ct, "refs/heads/" + name).ConfigureAwait(false)).ContainsKey("refs/heads/" + name);

    /// <summary>Deletes the branch <paramref name="name"/> of <paramref name="project"/> (<c>update-ref -d</c>).</summary>
    private Task DeleteBranchAsync(FakeGitLabProject project, string name, CancellationToken ct) =>
        GitOkAsync(project.BarePath!, null, ct, "update-ref", "-d", "refs/heads/" + name);

    /// <summary>The refs under <paramref name="prefixes"/> of a bare repository, by full name: one <c>for-each-ref</c>.</summary>
    private async Task<Dictionary<string, string>> ReadRefsAsync(string bare, CancellationToken ct, params string[] prefixes)
    {
        var listed = await GitOkAsync(bare, null, ct, ["for-each-ref", "--format=%(objectname) %(refname)", .. prefixes]).ConfigureAwait(false);
        var refs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0)
            {
                refs[line[(space + 1)..].TrimEnd('\r')] = line[..space];
            }
        }

        return refs;
    }

    /// <summary>Runs git in <paramref name="folder"/>, isolated; its output, or an exception naming the command and git's error.</summary>
    private async Task<string> GitOkAsync(string folder, string? input, CancellationToken ct, params string[] arguments)
    {
        var result = await GitAsync(folder, input, null, ct, arguments).ConfigureAwait(false);
        return result.Succeeded
            ? result.StandardOutput
            : throw new InvalidOperationException($"git {string.Join(' ', arguments)} in {folder} failed ({result.ExitCode}): {result.StandardError.Trim()}");
    }

    private static string HeadRef(int iid) => string.Create(CultureInfo.InvariantCulture, $"refs/merge-requests/{iid}/head");
}
