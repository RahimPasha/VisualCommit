using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Keep ignored files (D193, D196, D221; docs/phase-6-design.md 9.3 to 9.10): the reads that find
/// the ignored files an operation without git's own option would overwrite or delete, and the
/// pull's check inside <see cref="IntegratePullAsync(GitRef, PullIntegration, Func{string, Task}?, NetworkOptions?, PullIntegrationOptions?, CancellationToken)"/>.
/// <para>
/// Git's <c>--no-overwrite-ignore</c> is exact for a checkout and a fast-forward, does nothing for
/// a real merge, and does not exist for rebase, cherry-pick, revert or stash, each of which
/// overwrites an ignored file it writes, and a rebase can delete one (0.4). So each read lists the
/// paths the operation may write, from git's trees, and asks which files on disk at, under or
/// above them git ignores (<see cref="FindIgnoredAtAsync"/>). Every read runs with
/// <c>GIT_OPTIONAL_LOCKS=0</c> (D47) and no pathspec, ends every tree argument list with
/// <c>--</c> (a file named <c>HEAD</c> would make <c>diff-tree HEAD …</c> ambiguous, 0.4), and puts
/// lists of unbounded length on standard input, never on its command line (Windows starts no
/// process past 32,767 characters, 0.4). Each returns the ignored files in the way, distinct, in
/// byte order; a read that fails throws <see cref="GitException"/>, which the operation logs
/// before it goes on to git (9.3).
/// </para>
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// For each path an operation may write (<paramref name="written"/>: repository-relative, with
    /// <c>/</c>, as git prints them), the ignored files in its way on disk (docs/phase-6-design.md
    /// 9.3, phase 5's undo guard, D169): every folder above it that is a file or a symbolic link
    /// (an ignored file <c>logs</c> where it writes <c>logs/a.txt</c>), the path itself when it is a
    /// file or a link, and every file under it when it is a folder, links not followed (an ignored
    /// folder <c>build/</c> where it writes a file <c>build</c>). The candidates go, in byte order,
    /// to one <c>git check-ignore --stdin -z</c>, which leaves tracked files out; no candidate on
    /// disk means no process. Returns each pair of a written path and an ignored file in its way,
    /// ordered by the ignored file, then the written path, so that a merge's check can keep or
    /// drop each by the path it writes (9.4) and the others take the distinct ignored files
    /// (<see cref="IgnoredFiles"/>).
    /// <para>
    /// The disk is asked once per call about each folder and path (a list of thousands of paths,
    /// such as an unborn pull's whole tree, shares its folders), and nothing is asked under a
    /// folder that does not exist at all, under which nothing can: the candidates are those that
    /// asking about every folder of every path would find.
    /// </para>
    /// </summary>
    internal async Task<IReadOnlyList<(string Written, string Ignored)>> FindIgnoredAtAsync(IReadOnlyCollection<string> written, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(written);

        // Each written path with a candidate on disk; Ignored is the candidate until check-ignore says.
        var pairs = new List<(string Written, string Ignored)>();
        var candidates = new SortedSet<string>(StringComparer.Ordinal);
        var onDisk = new Dictionary<string, OnDisk>(StringComparer.Ordinal);
        foreach (var path in written.Distinct(StringComparer.Ordinal))
        {
            var reachable = true;
            foreach (var parent in ParentFolders(path))
            {
                var kind = KindOf(parent);
                if (kind == OnDisk.Missing)
                {
                    reachable = false;
                    break;
                }

                if (kind == OnDisk.FileOrLink)
                {
                    Add(path, parent);
                }
            }

            if (!reachable)
            {
                continue;
            }

            switch (KindOf(path))
            {
                case OnDisk.FileOrLink:
                    Add(path, path);
                    break;
                case OnDisk.Folder:
                    foreach (var file in FilesUnder(path))
                    {
                        Add(path, file);
                    }

                    break;
            }
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        var ignored = (await IgnoredAmongAsync(candidates, cancellationToken).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
        return [.. pairs
            .Where(pair => ignored.Contains(pair.Ignored))
            .Distinct()
            .OrderBy(pair => pair.Ignored, StringComparer.Ordinal)
            .ThenBy(pair => pair.Written, StringComparer.Ordinal)];

        void Add(string path, string candidate)
        {
            pairs.Add((path, candidate));
            candidates.Add(candidate);
        }

        // A file or a link first, as git takes it (a link to a folder too), then a real folder.
        OnDisk KindOf(string path)
        {
            if (!onDisk.TryGetValue(path, out var kind))
            {
                var full = WorkingTreePath(path);
                kind = IsFileOrLink(full) ? OnDisk.FileOrLink : Directory.Exists(full) ? OnDisk.Folder : OnDisk.Missing;
                onDisk[path] = kind;
            }

            return kind;
        }
    }

    /// <summary>What a working-tree path is on disk, for <see cref="FindIgnoredAtAsync"/>.</summary>
    private enum OnDisk
    {
        Missing,
        FileOrLink,
        Folder,
    }

    /// <summary>The distinct ignored files of <see cref="FindIgnoredAtAsync"/>'s pairs, in byte order.</summary>
    private static IReadOnlyList<string> IgnoredFiles(IEnumerable<(string Written, string Ignored)> pairs) =>
        [.. pairs.Select(pair => pair.Ignored).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>The folders a repository path lies in, outermost first: <c>a</c> and <c>a/b</c> for <c>a/b/c.txt</c>.</summary>
    private static IEnumerable<string> ParentFolders(string path)
    {
        for (var slash = path.IndexOf('/', StringComparison.Ordinal); slash > 0; slash = path.IndexOf('/', slash + 1))
        {
            yield return path[..slash];
        }
    }

    // ---- 9.4: merges and the fast-forward ---------------------------------------------------------

    /// <summary>
    /// 9.4: what the source has and <paramref name="into"/> lacks
    /// (<c>git diff-tree -r -z --no-renames --name-only --diff-filter=A &lt;into&gt; &lt;source&gt; --</c>),
    /// which holds every path a merge can write over an untracked file (a modify/delete conflict's
    /// too: <paramref name="into"/> deleted it), asked about on disk. Only when something is in the
    /// way, <c>git merge-base --all &lt;into&gt; &lt;source&gt;</c> and per base <c>git diff-tree -r -z
    /// --no-renames --name-only --diff-filter=AMT &lt;base&gt; &lt;source&gt; --</c>: a file stays in
    /// the way when some base's list has the path written over it (the merge writes it: an
    /// addition, or the side git leaves of a modify/delete conflict); otherwise
    /// <paramref name="into"/> deleted the path and the merge keeps the deletion (m15). A merge of an
    /// ancestor keeps nothing (U4); no base (unrelated histories, which git refuses anyway) and a
    /// fast-forward (<paramref name="into"/> is the base) keep everything; a criss-cross keeps a
    /// file any base lists. Not <c>merge-tree --write-tree</c>, which would be exact but needs Git
    /// 2.38 (0.4). Usual cost: one read, two with a candidate on disk.
    /// </summary>
    public async Task<IReadOnlyList<string>> FindIgnoredInTheWayOfMergeAsync(string into, string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(into);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var added = SplitNul(await ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=A", into, source, "--").ConfigureAwait(false));
        var pairs = await FindIgnoredAtAsync(added, cancellationToken).ConfigureAwait(false);
        if (pairs.Count == 0)
        {
            return [];
        }

        var bases = await MergeBasesAsync(into, source, cancellationToken).ConfigureAwait(false);
        if (bases.Count == 0)
        {
            return IgnoredFiles(pairs);
        }

        var lists = await Task.WhenAll(bases.Select(commit =>
            ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=AMT", commit, source, "--"))).ConfigureAwait(false);
        var writes = lists.SelectMany(SplitNul).ToHashSet(StringComparer.Ordinal);
        return IgnoredFiles(pairs.Where(pair => writes.Contains(pair.Written)));
    }

    /// <summary>Every merge base of <paramref name="one"/> and <paramref name="other"/>: <c>git merge-base --all</c>; none (exit 1) for unrelated histories.</summary>
    private async Task<IReadOnlyList<string>> MergeBasesAsync(string one, string other, CancellationToken cancellationToken)
    {
        var command = Read("merge-base", "--all", one, other);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 1 && result.StandardOutput.Trim().Length == 0
            ? []
            : SplitLines(result.EnsureSuccess(command).StandardOutput);
    }

    // ---- 9.5: cherry-pick and revert, one or a list -------------------------------------------------

    /// <summary>
    /// 9.5: one <c>git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only
    /// --diff-filter=AMT</c> (<c>-R</c> for a revert) fed each commit's full SHA on standard input,
    /// one per line: each commit's change against its first parent, which a pick applies and a
    /// revert applies reversed; a merge reverted with <c>-m 1</c> is fed <c>&lt;sha&gt; &lt;first
    /// parent&gt;</c> (a merge's line alone prints nothing). Then <see cref="FindIgnoredAtAsync"/> of
    /// every path; no second stage. It is enough: an added or modified path is written where HEAD
    /// lacks it (an addition, or the side git leaves of a modify/delete conflict, 0.4's T9 and T4),
    /// a tracked path is never ignored, and in a list the union covers a path the first pick adds
    /// and the second deletes. Usual cost: two reads.
    /// </summary>
    /// <param name="commits">The commits in the order git applies them, as full SHAs: <c>--stdin</c> resolves no name (0.4).</param>
    /// <param name="revert">Reverts, not picks.</param>
    /// <param name="mergeParent">For one merge commit reverted with <c>-m 1</c>: its first parent's full SHA.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <exception cref="ArgumentException">A commit or <paramref name="mergeParent"/> is not a full SHA, or <paramref name="mergeParent"/> comes with more than one commit.</exception>
    public async Task<IReadOnlyList<string>> FindIgnoredInTheWayOfCommitsAsync(IReadOnlyList<string> commits, bool revert, string? mergeParent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commits);
        RequireFullObjectIds(commits, nameof(commits));
        if (mergeParent is not null)
        {
            RequireFullObjectIds([mergeParent], nameof(mergeParent));
            if (commits.Count != 1)
            {
                throw new ArgumentException("A merge is reverted alone: its first parent goes with one commit.", nameof(mergeParent));
            }
        }

        if (commits.Count == 0)
        {
            return [];
        }

        IReadOnlyList<string> lines = mergeParent is null ? commits : [$"{commits[0]} {mergeParent}"];
        var written = await CommitsWriteAsync(lines, revert, cancellationToken).ConfigureAwait(false);
        return IgnoredFiles(await FindIgnoredAtAsync(written, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The paths that <paramref name="lines"/> (full SHAs, or <c>&lt;sha&gt; &lt;parent&gt;</c>) add,
    /// modify or change the type of, reversed for <paramref name="revert"/>: 9.5's <c>diff-tree
    /// --stdin</c>, one process whatever the number of commits.
    /// </summary>
    private async Task<IReadOnlyList<string>> CommitsWriteAsync(IReadOnlyList<string> lines, bool revert, CancellationToken cancellationToken)
    {
        string[] arguments = revert
            ? ["diff-tree", "--stdin", "--no-commit-id", "--root", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=AMT", "-R"]
            : ["diff-tree", "--stdin", "--no-commit-id", "--root", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=AMT"];
        var command = new GitCommand(arguments)
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            StandardInput = string.Concat(lines.Select(line => line + "\n")),
        };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return SplitNul(result.EnsureSuccess(command).StandardOutput);
    }

    private static void RequireFullObjectIds(IEnumerable<string> shas, string parameterName)
    {
        foreach (var sha in shas)
        {
            if (sha is null || !IsFullObjectId(sha))
            {
                throw new ArgumentException($"A full commit id is needed, not '{sha}': git's --stdin reads no names.", parameterName);
            }
        }
    }

    // ---- 9.6: rebase onto, "Check out and rebase" and the pull's rebase ------------------------------

    /// <summary>
    /// 9.6: four reads side by side: (1) <c>git merge-base --is-ancestor &lt;onto&gt; &lt;tip&gt;</c>;
    /// (2) <c>git diff-tree -r -z --no-renames --name-only --diff-filter=A &lt;tip&gt; &lt;onto&gt; --</c>
    /// (git's first checkout, of <paramref name="onto"/>); (3) <c>git log --no-renames --name-only -z
    /// --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT
    /// &lt;onto&gt;..&lt;tip&gt; --</c> (the commits git may replay); (4) <c>git rev-list
    /// --min-parents=2 --max-count=1 &lt;onto&gt;..&lt;tip&gt; --</c> (a merge in the range, which (3)
    /// cannot show). An ancestor <paramref name="onto"/> and no merge in the range mean git writes
    /// nothing ("Current branch {b} is up to date.", 0.4's U2): nothing in the way. Otherwise
    /// <see cref="FindIgnoredAtAsync"/> of (2) and (3): with a merge in the range git checks out
    /// <paramref name="onto"/> and replays even a branch only ahead of it, plain or with
    /// <c>--rebase-merges</c>, and an ignored file a commit of the range stops tracking is deleted
    /// (U5). With a merge in the range, also the branch points (<see cref="BranchPointsWriteAsync"/>):
    /// <c>--rebase-merges</c> starts a side branch with <c>reset &lt;its original branch
    /// point&gt;</c>, which writes that commit's tree, and from Git 2.42 <c>rebase.rebaseMerges</c>
    /// makes the app's plain <c>git rebase</c> one too, so the read does not depend on the rebase's
    /// options. A superset of what git writes (git leaves out commits whose patch is upstream, the
    /// pull's fork point maybe more; a plain rebase writes no branch point). Cost: four reads,
    /// then <c>check-ignore</c>; with a merge in the range two reads more before it.
    /// </summary>
    /// <param name="tip">What is rebased: <c>HEAD</c>, or for "Check out and rebase" the branch's full ref.</param>
    /// <param name="onto">The commit it is rebased onto: the menu's SHA, or the pull's upstream ref.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public async Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(string tip, string onto, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tip);
        ArgumentException.ThrowIfNullOrWhiteSpace(onto);
        var range = $"{onto}..{tip}";
        var ancestor = IsAncestorAsync(onto, tip, cancellationToken);
        var checkout = ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=A", tip, onto, "--");
        var replayed = ReadOutputAsync(cancellationToken, "log", "--no-renames", "--name-only", "-z", "--root", "--no-merges", "--no-show-signature", "--format=%x1e%H", "--diff-filter=AMT", range, "--");
        var merge = ReadOutputAsync(cancellationToken, "rev-list", "--min-parents=2", "--max-count=1", range, "--");
        await Task.WhenAll(ancestor, checkout, replayed, merge).ConfigureAwait(false);
        var hasMerge = (await merge.ConfigureAwait(false)).Trim().Length > 0;
        if (await ancestor.ConfigureAwait(false) && !hasMerge)
        {
            return [];
        }

        List<string> written = [.. SplitNul(await checkout.ConfigureAwait(false)), .. LoggedPaths(await replayed.ConfigureAwait(false))];
        if (hasMerge)
        {
            written.AddRange(await BranchPointsWriteAsync(range, cancellationToken).ConfigureAwait(false));
        }

        return IgnoredFiles(await FindIgnoredAtAsync(written, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 9.6 with a merge in <paramref name="range"/> (<c>&lt;onto&gt;..&lt;tip&gt;</c>): what the
    /// commits <c>git rebase --rebase-merges</c> may <c>reset</c> to write. Without
    /// <c>--rebase-cousins</c> git keeps a side branch's original branch point, a parent outside the
    /// range, and its todo's <c>reset &lt;that commit&gt;</c> writes the commit's tree (a side
    /// branch from a commit that tracks a file the upstream and the tip no longer track, merged
    /// into the tip: Git 2.36 and 2.55 overwrite the ignored file at the reset and delete it at
    /// the next <c>reset onto</c>, while the plain rebase keeps it). So (5) <c>git rev-list
    /// --topo-order --boundary &lt;onto&gt;..&lt;tip&gt; --</c>: its lines <c>-&lt;sha&gt;</c> are
    /// every parent outside the range, among them each such branch point (and the base that git
    /// resets to as <c>onto</c>, which only adds to the set), and its first line is the tip, as no
    /// commit comes before its children; then (6) one <c>git diff-tree --stdin --no-commit-id -r
    /// -z --no-renames --name-only --diff-filter=A</c> fed <c>&lt;boundary&gt; &lt;tip&gt;</c> per
    /// boundary commit (full SHAs, which <c>--stdin</c> needs): what each has and the tip lacks, as
    /// (2) for <c>&lt;onto&gt;</c>, since a path the tip tracks is no ignored file once the tip is
    /// checked out.
    /// </summary>
    private async Task<IReadOnlyList<string>> BranchPointsWriteAsync(string range, CancellationToken cancellationToken)
    {
        var lines = SplitLines(await ReadOutputAsync(cancellationToken, "rev-list", "--topo-order", "--boundary", range, "--").ConfigureAwait(false));
        var tip = lines.FirstOrDefault(line => !line.StartsWith('-'));
        var boundary = lines.Where(line => line.StartsWith('-')).Select(line => line[1..]).ToList();
        if (tip is null || boundary.Count == 0)
        {
            return [];
        }

        var command = new GitCommand("diff-tree", "--stdin", "--no-commit-id", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=A")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            StandardInput = string.Concat(boundary.Select(commit => $"{commit} {tip}\n")),
        };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return SplitNul(result.EnsureSuccess(command).StandardOutput);
    }

    /// <summary>
    /// The paths of (3)'s <c>git log --name-only -z --format=%x1e%H</c>: each commit is a
    /// <c>\x1e&lt;sha&gt;</c> field, then its paths, each ended by NUL, the first after a line feed.
    /// </summary>
    internal static IReadOnlyList<string> LoggedPaths(string output)
    {
        var paths = new List<string>();
        var afterHeader = false;
        foreach (var field in output.Split('\0'))
        {
            if (field.StartsWith('\x1e'))
            {
                afterHeader = true;
                continue;
            }

            var path = afterHeader && field.StartsWith('\n') ? field[1..] : field;
            afterHeader = false;
            if (path.Length > 0)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    // ---- 9.7: interactive rebase and squash ------------------------------------------------------------

    /// <summary>
    /// 9.7, from where git starts: git skips the todo's leading picks whose only parent is where it
    /// stands and checks out the last of them instead of <see cref="RebasePlan.Onto"/> (0.4's T5),
    /// and the <c>label</c> line before every group that changes a message stops it (T6). So, with
    /// <see cref="RebaseTodo.LeadingPicks"/>, <c>git rev-list --stdin --no-walk=unsorted --parents</c>
    /// fed their SHAs on standard input (999 leading picks would be some 41,000 characters as
    /// arguments, 0.4) moves the start from the base to each in turn while its only parent is the
    /// start. Then side by side <c>git diff-tree -r -z --no-renames --name-only --diff-filter=A
    /// &lt;tip&gt; &lt;start&gt; --</c> (the checkout) and 9.5's <c>diff-tree --stdin</c> fed every
    /// commit of <see cref="RebaseTodo.AppliedCommits"/> after the skipped ones (one after a
    /// <c>label</c> that git fast-forwards still writes its change, T6), and
    /// <see cref="FindIgnoredAtAsync"/> of the union. A squash's first group changes the message, so
    /// nothing is skipped. Cost: two or three reads, then <c>check-ignore</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The plan cannot be written (<see cref="RebaseTodo.Build"/>).</exception>
    public async Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(RebasePlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.Onto, nameof(plan));
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.ExpectedTip, nameof(plan));
        var leading = RebaseTodo.LeadingPicks(plan);
        var applied = RebaseTodo.AppliedCommits(plan);
        var start = plan.Onto;
        var skipped = 0;
        if (leading.Count > 0)
        {
            var command = new GitCommand("rev-list", "--stdin", "--no-walk=unsorted", "--parents")
            {
                WorkingDirectory = WorkingDirectory,
                Environment = ReadEnvironment,
                StandardInput = string.Concat(leading.Select(sha => sha + "\n")),
            };
            var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            foreach (var (pick, line) in leading.Zip(SplitLines(result.EnsureSuccess(command).StandardOutput)))
            {
                // "<commit> <parent>": one parent, and it is where git stands.
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length != 2
                    || !string.Equals(fields[0], pick, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(fields[1], start, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                start = pick;
                skipped++;
            }
        }

        var checkout = ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=A", plan.ExpectedTip, start, "--");
        var rest = applied.Skip(skipped).ToList();
        var picks = rest.Count > 0 ? CommitsWriteAsync(rest, revert: false, cancellationToken) : Task.FromResult<IReadOnlyList<string>>([]);
        await Task.WhenAll(checkout, picks).ConfigureAwait(false);
        IReadOnlyList<string> written = [.. SplitNul(await checkout.ConfigureAwait(false)), .. await picks.ConfigureAwait(false)];
        return IgnoredFiles(await FindIgnoredAtAsync(written, cancellationToken).ConfigureAwait(false));
    }

    // ---- 9.8: stash apply and pop -------------------------------------------------------------------

    /// <summary>
    /// 9.8: from the stash's paths as <see cref="ReadStashPathsAsync"/> reads them once (D179's
    /// count has read them before), so no process runs before <c>check-ignore</c>: what its changes
    /// write against its base (added, modified or changed in type: <see cref="StashPaths.Changed"/>
    /// less <see cref="StashPaths.Deleted"/>; a staged new file, and the stash's side of a
    /// modify/delete, which Git 2.55 overwrites and 2.36 does not), and its untracked files (for a
    /// folder above one that is an ignored file: D179's count has already refused one that
    /// exists), asked about on disk.
    /// </summary>
    public async Task<IReadOnlyList<string>> FindIgnoredInTheWayOfStashAsync(string stashCommit, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stashCommit);
        var paths = await ReadStashPathsAsync(stashCommit, cancellationToken).ConfigureAwait(false);
        var deleted = paths.Deleted.ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> written = [.. paths.Changed.Where(path => !deleted.Contains(path)), .. paths.Untracked];
        return IgnoredFiles(await FindIgnoredAtAsync(written, cancellationToken).ConfigureAwait(false));
    }

    // ---- 9.10: the pull --------------------------------------------------------------------------------

    /// <summary>
    /// The pull's check (9.10), once <paramref name="arguments"/> (the fast-forward, the merge or
    /// the rebase, <paramref name="rebased"/>) are chosen and before anything is written: with
    /// <see cref="PullIntegrationOptions.KeepIgnored"/> a fast-forward or merge gets
    /// <c>--no-overwrite-ignore</c> just before <paramref name="head"/> and the merge's check
    /// (<see cref="FindIgnoredInTheWayOfMergeAsync"/> of <c>HEAD</c> and <paramref name="head"/>), a
    /// rebase the rebase's (<see cref="FindIgnoredInTheWayOfRebaseAsync(string, string, CancellationToken)"/>
    /// of <c>HEAD</c> and the upstream), and an unborn branch (<paramref name="before"/> null), whose
    /// fast-forward writes every file of <paramref name="head"/> without git's option taking effect
    /// (git reads the tree into an empty index, 0.4), the paths of <c>git ls-tree -r -z --name-only
    /// &lt;head&gt;</c>. A read that fails is logged and the pull goes on to git, keeping the option.
    /// Returns the arguments to run and the ignored files in the way (none: integrate). Without the
    /// option, <paramref name="arguments"/> as they are and nothing in the way: git's default.
    /// </summary>
    private async Task<(string[] Arguments, IReadOnlyList<string> Ignored)> KeepIgnoredInPullAsync(
        GitRef branch,
        string head,
        string? before,
        string[] arguments,
        bool rebased,
        PullIntegrationOptions? pullOptions,
        CancellationToken cancellationToken)
    {
        if (pullOptions is not { KeepIgnored: true })
        {
            return (arguments, []);
        }

        if (!rebased)
        {
            // git merge [--ff-only | --no-edit …] <head>: the option just before the positional argument.
            var at = Array.LastIndexOf(arguments, head);
            arguments = [.. arguments[..at], .. NoOverwriteIgnore(true), .. arguments[at..]];
        }

        try
        {
            IReadOnlyList<string> ignored;
            if (before is null)
            {
                var tree = SplitNul(await ReadOutputAsync(cancellationToken, "ls-tree", "-r", "-z", "--name-only", head).ConfigureAwait(false));
                ignored = IgnoredFiles(await FindIgnoredAtAsync(tree, cancellationToken).ConfigureAwait(false));
            }
            else if (rebased)
            {
                ignored = await FindIgnoredInTheWayOfRebaseAsync("HEAD", branch.UpstreamRef!, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                ignored = await FindIgnoredInTheWayOfMergeAsync("HEAD", head, cancellationToken).ConfigureAwait(false);
            }

            return (arguments, ignored);
        }
        catch (GitException ex)
        {
            _log.Warning($"{Name}: the ignored files in the pull's way could not be read; git integrates as it would.", ex);
            return (arguments, []);
        }
    }
}
