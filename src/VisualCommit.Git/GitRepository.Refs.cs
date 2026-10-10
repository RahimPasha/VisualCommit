using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

public sealed partial class GitRepository
{
    private const string HeadsPrefix = "refs/heads/";
    private const string RemotesPrefix = "refs/remotes/";
    private const string TagsPrefix = "refs/tags/";

    /// <summary>
    /// The fields <c>for-each-ref</c> prints for each ref, separated by the unit separator
    /// (<c>%1f</c>), which ref names cannot contain. <c>*objecttype</c> and <c>*objectname</c>
    /// describe what an annotated tag points to.
    /// </summary>
    private const string RefFormat =
        "%(refname)%1f%(objecttype)%1f%(objectname)%1f%(*objecttype)%1f%(*objectname)" +
        "%1f%(upstream)%1f%(upstream:short)%1f%(upstream:track)%1f%(symref)";

    private const int RefFieldCount = 9;

    /// <summary>The fields <c>git stash list</c> prints for each stash; the subject of a stash's reflog entry (<c>%gs</c>) is its message.</summary>
    private const string StashFormat = "%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%gs";

    private const int StashFieldCount = 7;

    public async Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default)
    {
        // Five reads that do not depend on one another, run together so that the snapshot is
        // taken in about the time of one.
        var refsTask = ReadOutputAsync(cancellationToken, "for-each-ref", "--format=" + RefFormat, "refs/heads", "refs/remotes", "refs/tags");
        var remotesTask = ReadOutputAsync(cancellationToken, "remote");
        var stashesTask = ReadOutputAsync(
            cancellationToken,
            "stash", "list", "--no-show-signature", "--no-color", "--encoding=UTF-8", "--format=" + StashFormat);
        var headRefTask = ReadHeadRefAsync(cancellationToken);
        var headShaTask = ReadHeadShaAsync(cancellationToken);
        await Task.WhenAll((Task[])[refsTask, remotesTask, stashesTask, headRefTask, headShaTask]).ConfigureAwait(false);

        var remotes = SplitLines(await remotesTask.ConfigureAwait(false));
        remotes.Sort(StringComparer.Ordinal);
        var headRef = await headRefTask.ConfigureAwait(false);
        var refs = await ParseRefsAsync(await refsTask.ConfigureAwait(false), remotes, headRef, cancellationToken).ConfigureAwait(false);
        var stashes = ParseStashes(await stashesTask.ConfigureAwait(false));

        var branchName = headRef is null ? null : headRef.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? headRef[HeadsPrefix.Length..] : headRef;
        var head = new HeadState(branchName, await headShaTask.ConfigureAwait(false));
        return new RepoRefs(head, refs, stashes, remotes);
    }

    /// <summary>The full name of the branch HEAD is on, or null when HEAD is detached.</summary>
    private async Task<string?> ReadHeadRefAsync(CancellationToken cancellationToken)
    {
        var command = Read("symbolic-ref", "-q", "HEAD");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return null; // Detached: HEAD holds a commit id, not a branch name.
        }

        var name = result.EnsureSuccess(command).StandardOutput.Trim();
        return name.Length > 0 ? name : null;
    }

    /// <summary>The commit HEAD points to, or null when its branch has no commits yet.</summary>
    private async Task<string?> ReadHeadShaAsync(CancellationToken cancellationToken)
    {
        var command = Read("rev-parse", "-q", "--verify", "HEAD");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return null; // Unborn.
        }

        var sha = result.EnsureSuccess(command).StandardOutput.Trim();
        return sha.Length > 0 ? sha : null;
    }

    private async Task<List<GitRef>> ParseRefsAsync(string output, IReadOnlyList<string> remotes, string? headRef, CancellationToken cancellationToken)
    {
        // Remote names may contain slashes ("team/origin"), so a remote branch's remote is the
        // longest configured name that its name starts with.
        var remotesLongestFirst = remotes.OrderByDescending(remote => remote.Length).ToList();
        var refs = new List<GitRef>();
        var nestedTags = new List<int>();

        foreach (var line in SplitLines(output))
        {
            var fields = line.Split('\x1f');
            if (fields.Length != RefFieldCount)
            {
                _log.Warning($"Skipped a line of git for-each-ref that could not be read: {line}");
                continue;
            }

            var (fullName, objectType, objectName, peeledType, peeledName) = (fields[0], fields[1], fields[2], fields[3], fields[4]);
            var (upstream, upstreamShort, track, symref) = (fields[5], fields[6], fields[7], fields[8]);

            GitRef gitRef;
            if (fullName.StartsWith(HeadsPrefix, StringComparison.Ordinal))
            {
                var (ahead, behind, gone) = ParseTrack(track);
                gitRef = new GitRef(
                    fullName,
                    fullName[HeadsPrefix.Length..],
                    RefKind.LocalBranch,
                    objectName,
                    Upstream: upstream.Length > 0 && upstreamShort.Length > 0 ? upstreamShort : null,
                    Ahead: ahead,
                    Behind: behind,
                    UpstreamGone: gone,
                    IsHead: fullName == headRef);
            }
            else if (fullName.StartsWith(RemotesPrefix, StringComparison.Ordinal))
            {
                if (symref.Length > 0)
                {
                    continue; // refs/remotes/origin/HEAD: it only names the remote's default branch.
                }

                var name = fullName[RemotesPrefix.Length..];
                gitRef = new GitRef(fullName, name, RefKind.RemoteBranch, objectName, RemoteName: FindRemote(name, remotesLongestFirst));
            }
            else if (fullName.StartsWith(TagsPrefix, StringComparison.Ordinal))
            {
                var annotated = objectType == "tag" && peeledName.Length > 0;
                gitRef = new GitRef(fullName, fullName[TagsPrefix.Length..], RefKind.Tag, annotated ? peeledName : objectName);
                if (annotated && peeledType == "tag")
                {
                    nestedTags.Add(refs.Count);
                }
            }
            else
            {
                continue;
            }

            refs.Add(gitRef);
        }

        if (nestedTags.Count > 0)
        {
            await PeelNestedTagsAsync(refs, nestedTags, cancellationToken).ConfigureAwait(false);
        }

        return refs;
    }

    /// <summary>
    /// <c>*objectname</c> follows a tag one step. A tag of a tag (rare, but git allows it) is
    /// followed to its end with <c>rev-parse &lt;ref&gt;^{}</c>, in one call for all of them.
    /// </summary>
    private async Task PeelNestedTagsAsync(List<GitRef> refs, List<int> indexes, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "rev-parse" };
        arguments.AddRange(indexes.Select(index => refs[index].FullName + "^{}"));
        var lines = SplitLines(await ReadOutputAsync(cancellationToken, [.. arguments]).ConfigureAwait(false));
        for (var i = 0; i < indexes.Count && i < lines.Count; i++)
        {
            refs[indexes[i]] = refs[indexes[i]] with { TargetSha = lines[i] };
        }
    }

    private static string FindRemote(string remoteBranchName, IReadOnlyList<string> remotesLongestFirst)
    {
        foreach (var remote in remotesLongestFirst)
        {
            if (remoteBranchName.Length > remote.Length
                && remoteBranchName[remote.Length] == '/'
                && remoteBranchName.StartsWith(remote, StringComparison.Ordinal))
            {
                return remote;
            }
        }

        // A remote branch whose remote is no longer configured: its first part is the best guess.
        var slash = remoteBranchName.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 ? remoteBranchName[..slash] : remoteBranchName;
    }

    /// <summary>
    /// Reads <c>%(upstream:track)</c>: <c>[ahead 1, behind 2]</c>, <c>[ahead 1]</c>,
    /// <c>[behind 2]</c>, <c>[gone]</c>, or nothing when the branch is level with its upstream or has none.
    /// </summary>
    internal static (int Ahead, int Behind, bool Gone) ParseTrack(string track)
    {
        var text = track.Trim().TrimStart('[').TrimEnd(']');
        if (text == "gone")
        {
            return (0, 0, true);
        }

        var (ahead, behind) = (0, 0);
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("ahead ", StringComparison.Ordinal))
            {
                _ = int.TryParse(part.AsSpan("ahead ".Length), NumberStyles.None, CultureInfo.InvariantCulture, out ahead);
            }
            else if (part.StartsWith("behind ", StringComparison.Ordinal))
            {
                _ = int.TryParse(part.AsSpan("behind ".Length), NumberStyles.None, CultureInfo.InvariantCulture, out behind);
            }
        }

        return (ahead, behind, false);
    }

    private List<StashEntry> ParseStashes(string output)
    {
        var stashes = new List<StashEntry>();
        var lines = SplitLines(output);
        for (var index = 0; index < lines.Count; index++)
        {
            // The message comes last, so a separator inside it cannot shift the other fields.
            var fields = lines[index].Split('\x1f', StashFieldCount);
            if (fields.Length != StashFieldCount
                || !GitDates.TryParse(fields[4], out var authorDate)
                || !GitDates.TryParse(fields[5], out var date))
            {
                _log.Warning($"Skipped a line of git stash list that could not be read: {lines[index]}");
                continue;
            }

            var parents = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            stashes.Add(new StashEntry(
                index,
                fields[0],
                parents.Length > 0 ? parents[0] : string.Empty,
                fields[6],
                fields[2],
                fields[3],
                authorDate,
                date));
        }

        return stashes;
    }
}
