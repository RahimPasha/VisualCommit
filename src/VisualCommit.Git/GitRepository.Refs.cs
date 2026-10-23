using System.Collections.Concurrent;
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
    /// describe what an annotated tag points to. A branch's upstream is read as its full ref, its
    /// remote's name and its name on the remote, never cut out of <c>%(upstream:short)</c>, which
    /// is wrong for a local upstream (D101); the short form is kept for the left panel's tooltip.
    /// </summary>
    private const string RefFormat =
        "%(refname)%1f%(objecttype)%1f%(objectname)%1f%(*objecttype)%1f%(*objectname)" +
        "%1f%(upstream)%1f%(upstream:short)%1f%(upstream:track)%1f%(symref)" +
        "%1f%(upstream:remotename)%1f%(upstream:remoteref)";

    private const int RefFieldCount = 11;

    /// <summary>The fields <c>git stash list</c> prints for each stash; the subject of a stash's reflog entry (<c>%gs</c>) is its message.</summary>
    private const string StashFormat = "%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%gs";

    private const int StashFieldCount = 7;

    /// <summary>The fetch lines already logged as skipped (<c>&lt;remote&gt;\n&lt;value&gt;</c>), so that each refs read does not log them again.</summary>
    private readonly ConcurrentDictionary<string, byte> _skippedFetchLines = new(StringComparer.Ordinal);

    public async Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default)
    {
        // Five reads that do not depend on one another, run together so that the snapshot is
        // taken in about the time of one.
        var refsTask = ReadOutputAsync(cancellationToken, "for-each-ref", "--format=" + RefFormat, "refs/heads", "refs/remotes", "refs/tags");
        var remotesTask = ReadRemoteKeysAsync(cancellationToken);
        var stashesTask = ReadOutputAsync(
            cancellationToken,
            "stash", "list", "--no-show-signature", "--no-color", "--encoding=UTF-8", "--format=" + StashFormat);
        var headRefTask = ReadHeadRefAsync(cancellationToken);
        var headShaTask = ReadHeadShaAsync(cancellationToken);
        await Task.WhenAll((Task[])[refsTask, remotesTask, stashesTask, headRefTask, headShaTask]).ConfigureAwait(false);

        var (remotes, refspecs) = await remotesTask.ConfigureAwait(false);
        var headRef = await headRefTask.ConfigureAwait(false);
        var refs = await ParseRefsAsync(await refsTask.ConfigureAwait(false), remotes, refspecs, headRef, cancellationToken).ConfigureAwait(false);
        var stashes = ParseStashes(await stashesTask.ConfigureAwait(false));

        var branchName = headRef is null ? null : headRef.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? headRef[HeadsPrefix.Length..] : headRef;
        var head = new HeadState(branchName, HeadCommit(refs, await headShaTask.ConfigureAwait(false)));
        return new RepoRefs(head, refs, stashes, remotes) { Refspecs = refspecs };
    }

    /// <summary>
    /// The remotes and their fetch lines in one read, in place of <c>git remote</c>
    /// (docs/phase-6-design.md 10.2): <c>git config --null --get-regexp ^remote\.</c>, without
    /// <c>--local</c>, since git fetches with global lines too. Each entry is
    /// <c>&lt;key&gt;\n&lt;value&gt;\0</c> in git's reading order, the section and the variable in
    /// lower case and the remote's name (the subsection, between <c>remote.</c> and the key's last
    /// dot) with its case and dots kept. The names are those <c>git remote</c> lists: every
    /// subsection of a <c>remote.&lt;name&gt;.&lt;var&gt;</c> key, each once, sorted ordinally; a
    /// key without one (<c>remote.pushdefault</c>) and a name starting with <c>/</c> (which git
    /// skips with a warning) name none, and neither lists a legacy <c>.git/remotes</c> file.
    /// The <c>.fetch</c> values are the lines. Every value the mapping leaves out but an empty one
    /// is logged as skipped, once per repository object (10.1): a key without a value and a line
    /// git refuses, with which git's own reads fail too (<c>for-each-ref</c>: <c>bad config
    /// variable</c> or <c>invalid refspec</c>, 128, so this read's snapshot is never made and the
    /// warning names the line), and a line git takes that names no source
    /// (<c>:refs/remotes/origin/x</c>). An empty value, which git takes and which fetches into
    /// <c>FETCH_HEAD</c> only, maps nothing and is not logged. Exit 1 is no remote and no line.
    /// </summary>
    private async Task<(List<string> Remotes, FetchRefspecs Refspecs)> ReadRemoteKeysAsync(CancellationToken cancellationToken)
    {
        const string Prefix = "remote.";
        var command = Read("config", "--null", "--get-regexp", @"^remote\.");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<(string Remote, string Value)>();
        if (result.ExitCode != 1)
        {
            foreach (var entry in result.EnsureSuccess(command).StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                // A key without a value ("[remote "x"] fetch") has no line feed.
                var newline = entry.IndexOf('\n', StringComparison.Ordinal);
                var key = newline < 0 ? entry : entry[..newline];
                var dot = key.LastIndexOf('.');
                if (!key.StartsWith(Prefix, StringComparison.Ordinal) || dot <= Prefix.Length || key[Prefix.Length] == '/')
                {
                    continue;
                }

                var name = key[Prefix.Length..dot];
                names.Add(name);
                if (RemoteArguments.TrimSuffix(key[Prefix.Length..], ".fetch") is not null)
                {
                    // Logged: a key without a value, and a value the mapping cannot take (one git
                    // refuses, or one without a source). Not an empty value, which git takes (it
                    // fetches into FETCH_HEAD only) and which maps nothing.
                    var value = newline < 0 ? string.Empty : entry[(newline + 1)..];
                    if ((newline < 0 || (value.Length > 0 && FetchRefspec.Parse(name, value) is null))
                        && _skippedFetchLines.TryAdd(name + "\n" + value, 0))
                    {
                        _log.Warning($"Skipped a fetch line of the remote {name} that git refuses or that names no source: {value}");
                    }

                    lines.Add((name, value));
                }
            }
        }

        var remotes = names.ToList();
        remotes.Sort(StringComparer.Ordinal);
        return (remotes, FetchRefspecs.Parse(lines));
    }

    /// <summary>
    /// HEAD's commit in a snapshot of the refs: that of the branch HEAD is on, as
    /// <c>for-each-ref</c> listed it, so that HEAD and its branch come from one read. The reads
    /// run side by side, and a commit made meanwhile (in a terminal) moves the branch between
    /// them: <c>rev-parse HEAD</c> could see the new commit and <c>for-each-ref</c> the old one,
    /// a snapshot of no moment. Its fingerprint differs from the last one, so the graph would
    /// load again, with the new commit's row from git log and its branch's label still on the
    /// row below (the phase 5 gate's f11f). <paramref name="revParsed"/> (<c>rev-parse HEAD</c>)
    /// stands for a detached HEAD, an unborn branch, and a branch made after the list was read.
    /// </summary>
    private static string? HeadCommit(List<GitRef> refs, string? revParsed) =>
        refs.Find(gitRef => gitRef.IsHead) is { } branch ? branch.TargetSha : revParsed;

    public async Task<HeadState> ReadHeadAsync(CancellationToken cancellationToken = default)
    {
        var headRef = ReadHeadRefAsync(cancellationToken);
        var headSha = ReadHeadShaAsync(cancellationToken);
        await Task.WhenAll(headRef, headSha).ConfigureAwait(false);
        var name = await headRef.ConfigureAwait(false);
        var branchName = name is null ? null : name.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? name[HeadsPrefix.Length..] : name;
        return new HeadState(branchName, await headSha.ConfigureAwait(false));
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

    private async Task<List<GitRef>> ParseRefsAsync(
        string output, IReadOnlyList<string> remotes, FetchRefspecs refspecs, string? headRef, CancellationToken cancellationToken)
    {
        // A remote branch's remote is the one whose fetch lines fill it (10.3). When two remotes
        // or none do, it is the longest configured name that its name starts with: remote names
        // may contain slashes ("team/origin"); without one, its first part.
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
            var (upstreamRemote, upstreamRemoteRef) = (fields[9], fields[10]);

            GitRef gitRef;
            if (fullName.StartsWith(HeadsPrefix, StringComparison.Ordinal))
            {
                var (ahead, behind, gone) = ParseTrack(track);
                var hasUpstream = upstream.Length > 0;
                gitRef = new GitRef(
                    fullName,
                    fullName[HeadsPrefix.Length..],
                    RefKind.LocalBranch,
                    objectName,
                    Upstream: hasUpstream && upstreamShort.Length > 0 ? upstreamShort : null,
                    Ahead: ahead,
                    Behind: behind,
                    UpstreamGone: gone,
                    IsHead: fullName == headRef,
                    UpstreamRef: hasUpstream ? upstream : null,
                    UpstreamRemote: hasUpstream && upstreamRemote.Length > 0 ? upstreamRemote : null,
                    UpstreamRemoteRef: hasUpstream && upstreamRemoteRef.Length > 0 ? upstreamRemoteRef : null);
            }
            else if (fullName.StartsWith(RemotesPrefix, StringComparison.Ordinal))
            {
                if (symref.Length > 0)
                {
                    continue; // refs/remotes/origin/HEAD: it only names the remote's default branch.
                }

                var name = fullName[RemotesPrefix.Length..];
                var remote = refspecs.SourceOf(fullName) is { Kind: RemoteSourceKind.Mapped or RemoteSourceKind.AmbiguousRefs, Remote: { } mapped }
                    ? mapped
                    : RemoteBranchNames.RemoteOf(name, remotes);
                gitRef = new GitRef(fullName, name, RefKind.RemoteBranch, objectName, RemoteName: remote);
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
            if (fields.Length != StashFieldCount || !TryParseIds(fields[0], fields[1], out var sha, out var parents))
            {
                _log.Warning($"Skipped a line of git stash list that could not be read: {lines[index]}");
                continue;
            }

            // A date git could not print is read as the epoch, as in the log (GitDates.ParseOrEpoch).
            stashes.Add(new StashEntry(
                index,
                sha,
                parents.Count > 0 ? parents[0] : string.Empty,
                fields[6],
                fields[2],
                fields[3],
                GitDates.ParseOrEpoch(fields[4]),
                GitDates.ParseOrEpoch(fields[5])));
        }

        return stashes;
    }
}
