using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake's git work (docs/phase-7a-design.md 2.3): its repositories' folders, the ref sync of
/// pull requests' heads (D267), and the reads of the create and commits routes. Git runs outside
/// the fake's lock, isolated from the machine.
/// </summary>
public sealed partial class FakeGitHub
{
    /// <summary>
    /// Adds the repository <paramref name="owner"/>/<paramref name="name"/> at
    /// <c>&lt;root&gt;/&lt;owner&gt;/&lt;name&gt;.git</c>: with <paramref name="sourceRepository"/>
    /// one <c>git clone --bare --no-hardlinks</c> of it; else an existing bare repository there in
    /// place, or a new empty one (<c>git init --bare --initial-branch main</c>). Its id is 3001 and
    /// up, its default branch read from <c>HEAD</c>, its owner a pusher when a user. The server, when
    /// there is one, serves it at once.
    /// </summary>
    /// <param name="forkOf">The full name of the repository it is a fork of (<c>parent</c> on A3).</param>
    public async Task<FakeRepository> AddRepositoryAsync(string owner, string name, string? sourceRepository = null, bool isPrivate = false, string? forkOf = null, string? description = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!IsPlain(owner) || !IsPlain(name) || name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"A repository's owner and name are of letters, digits, '.', '-' and '_': '{owner}/{name}'.", nameof(name));
        }

        var bare = Path.Combine(_root, owner, name + ".git");
        if (sourceRepository is not null)
        {
            if (Directory.Exists(bare))
            {
                throw new ArgumentException($"{bare} exists already: a repository cloned from {sourceRepository} needs a new folder.", nameof(sourceRepository));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(bare)!);
            await GitOkAsync(_root, null, ct, "clone", "--quiet", "--bare", "--no-hardlinks", "--", sourceRepository, bare).ConfigureAwait(false);
        }
        else if (!File.Exists(Path.Combine(bare, "HEAD")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(bare)!);
            await GitOkAsync(_root, null, ct, "init", "--quiet", "--bare", "--initial-branch", "main", bare).ConfigureAwait(false);
        }

        FakeRepository repository;
        lock (_gate)
        {
            repository = new FakeRepository(_nextRepositoryId++, owner, name, isPrivate, forkOf, description, DefaultBranchOf(bare), bare);
            AddRepositoryLocked(repository);
        }

        _server?.Serve(repository.FullName);
        return repository;
    }

    /// <summary>
    /// Brings the <c>refs/pull/&lt;n&gt;/head</c> of <paramref name="fullName"/>'s pull requests in
    /// step with their heads (D267), as the fake does before a list, a create and git's ref
    /// listings: one <c>for-each-ref</c> in it and one per fork with open pull requests; then one
    /// <c>update-ref --stdin</c> for the heads in the repository itself and one fetch per fork of
    /// the forks' branches (the objects copied, as GitHub's shared network does). A deleted fork's
    /// head is its existing ref; an open pull request with none is an <see cref="Errors"/> line.
    /// </summary>
    public async Task SyncRefsAsync(string fullName, CancellationToken ct = default)
    {
        var repository = Repository(fullName);
        if (repository.BarePath is null)
        {
            return;
        }

        var gate = LockOf(repository);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SyncLockedAsync(repository, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The server's hook before git lists a repository's refs: a sync when it has pull requests. Never throws; a failure is an <see cref="Errors"/> line.</summary>
    private async Task SyncBeforeGitAsync(string fullName, CancellationToken ct)
    {
        try
        {
            if (PullRequests(fullName).Count > 0)
            {
                await SyncRefsAsync(fullName, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordError($"The ref sync of {fullName} before git's request failed: {ex.Message}");
        }
    }

    /// <summary>The lock of one repository's ref sync and creates, so that two never interleave.</summary>
    private SemaphoreSlim LockOf(FakeRepository repository) => _repositoryLocks.GetOrAdd(repository.FullName, _ => new SemaphoreSlim(1, 1));

    /// <summary><see cref="SyncRefsAsync"/> under the repository's lock; returns its refs as read before the sync.</summary>
    private async Task<IReadOnlyDictionary<string, string>> SyncLockedAsync(FakeRepository repository, CancellationToken ct)
    {
        var bare = repository.BarePath!;
        var refs = await ReadRefsAsync(bare, ct, "refs/heads/", "refs/pull/").ConfigureAwait(false);
        var pulls = PullRequests(repository.FullName);
        lock (_gate)
        {
            _branches[repository.FullName] = refs;
        }

        if (pulls.Count == 0)
        {
            return refs;
        }

        var forkHeads = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var forkName in pulls.Where(pull => pull.IsOpen && pull.HeadRepository is not null).Select(pull => pull.HeadRepository!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            FakeRepository? fork;
            lock (_gate)
            {
                fork = RepositoryLocked(forkName);
            }

            if (fork?.BarePath is { } forkBare)
            {
                forkHeads[forkName] = await ReadRefsAsync(forkBare, ct, "refs/heads/").ConfigureAwait(false);
            }
        }

        var updates = new StringBuilder();
        var fetches = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pull in pulls)
        {
            var pullRef = PullRef(pull.Number);
            refs.TryGetValue(pullRef, out var existing);
            string? head = null;
            if (pull.IsOpen && pull.HeadRepository is null)
            {
                refs.TryGetValue("refs/heads/" + pull.HeadRef, out head);
            }
            else if (pull.IsOpen && forkHeads.TryGetValue(pull.HeadRepository!, out var heads))
            {
                heads.TryGetValue("refs/heads/" + pull.HeadRef, out head);
            }

            if (head is null)
            {
                // A deleted fork, a closed pull request or a head branch that is gone: the ref stays.
                if (existing is null)
                {
                    if (pull.IsOpen)
                    {
                        RecordError($"{pull}: no head branch {pull.HeadRef} and no {pullRef} to keep.");
                    }

                    continue;
                }

                pull.HeadSha = existing;
                continue;
            }

            if (head != existing)
            {
                if (pull.HeadRepository is null)
                {
                    updates.Append(CultureInfo.InvariantCulture, $"update {pullRef} {head}\n");
                }
                else
                {
                    if (!fetches.TryGetValue(pull.HeadRepository, out var refspecs))
                    {
                        fetches.Add(pull.HeadRepository, refspecs = []);
                    }

                    refspecs.Add($"+refs/heads/{pull.HeadRef}:{pullRef}");
                }
            }

            pull.HeadSha = head;
        }

        if (updates.Length > 0)
        {
            await GitOkAsync(bare, updates.ToString(), ct, "update-ref", "--stdin").ConfigureAwait(false);
        }

        foreach (var (forkName, refspecs) in fetches)
        {
            await GitOkAsync(bare, null, ct, ["-c", "gc.auto=0", "fetch", "--quiet", "--no-tags", Repository(forkName).BarePath!, .. refspecs]).ConfigureAwait(false);
        }

        return refs;
    }

    /// <summary>The refs under <paramref name="prefixes"/> of a bare repository, by full name: one <c>for-each-ref</c>.</summary>
    private async Task<IReadOnlyDictionary<string, string>> ReadRefsAsync(string bare, CancellationToken ct, params string[] prefixes)
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

    /// <summary>
    /// The commits of A6 (2.4): one <c>git log</c> from <paramref name="revision"/> in the bare
    /// repository; null when git does not know the revision.
    /// </summary>
    private async Task<IReadOnlyList<FakeCommit>?> LogAsync(string bare, string revision, CancellationToken ct)
    {
        var result = await GitAsync(bare, null, ct, "log", "--format=%H%x00%an%x00%ae%x00%aI%x00%cn%x00%ce%x00%cI%x00%s", revision, "--").ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        var commits = new List<FakeCommit>();
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\0');
            if (fields.Length >= 8)
            {
                commits.Add(new FakeCommit(
                    fields[0],
                    fields[1],
                    fields[2],
                    DateTimeOffset.Parse(fields[3], CultureInfo.InvariantCulture),
                    fields[4],
                    fields[5],
                    DateTimeOffset.Parse(fields[6], CultureInfo.InvariantCulture),
                    fields[7]));
            }
        }

        return commits;
    }

    /// <summary>How many commits <paramref name="head"/> has that <paramref name="baseRef"/> lacks: <c>rev-list --count</c>.</summary>
    private async Task<int> AheadAsync(string bare, string baseRef, string head, CancellationToken ct)
    {
        var counted = await GitOkAsync(bare, null, ct, "rev-list", "--count", $"{baseRef}..{head}").ConfigureAwait(false);
        return int.Parse(counted.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a bare repository has no branch, read from its files (a <c>git log</c> there exits 128).</summary>
    private static bool IsEmpty(string bare)
    {
        var heads = Path.Combine(bare, "refs", "heads");
        if (Directory.Exists(heads) && Directory.EnumerateFiles(heads, "*", SearchOption.AllDirectories).Any())
        {
            return false;
        }

        var packed = Path.Combine(bare, "packed-refs");
        return !File.Exists(packed) || !File.ReadLines(packed).Any(line => line.Contains(" refs/heads/", StringComparison.Ordinal));
    }

    /// <summary>The branch a bare repository's <c>HEAD</c> names; <c>main</c> when it names none.</summary>
    private static string DefaultBranchOf(string bare)
    {
        const string Prefix = "ref: refs/heads/";
        var head = File.ReadAllText(Path.Combine(bare, "HEAD")).Trim();
        return head.StartsWith(Prefix, StringComparison.Ordinal) ? head[Prefix.Length..] : "main";
    }

    private static string PullRef(int number) => string.Create(CultureInfo.InvariantCulture, $"refs/pull/{number}/head");

    private static bool IsPlain(string segment) =>
        segment is not ("." or "..") && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    private Task<GitResult> GitAsync(string folder, string? input, CancellationToken ct, params string[] arguments) =>
        _runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = _environment, StandardInput = input }, ct);

    private async Task<string> GitOkAsync(string folder, string? input, CancellationToken ct, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = _environment, StandardInput = input };
        return (await _runner.RunAsync(command, ct).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput;
    }

    /// <summary>One commit of <see cref="LogAsync"/>.</summary>
    private sealed record FakeCommit(string Sha, string AuthorName, string AuthorEmail, DateTimeOffset AuthorDate, string CommitterName, string CommitterEmail, DateTimeOffset CommitterDate, string Subject);
}
