using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.Git;

/// <summary>
/// Remotes and the network (phase 3 design 2.16 to 2.23, 2.29): fetch, pull's integration step,
/// pushes, upstreams, remote branch and tag deletion, and the remotes themselves. Every call that
/// talks to a remote gets the operation's prompt variables (<see cref="NetworkOptions.Environment"/>)
/// and reports git's progress lines (<see cref="GitProgressParser"/>) to
/// <see cref="NetworkOptions.Progress"/>; the token stops git (1.8, 1.9).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// <c>git fetch --progress &lt;remote&gt;</c>, or for every remote (<paramref name="remote"/>
    /// null) one such call per remote, in <c>git remote</c>'s order, leaving out the remotes with
    /// <c>remote.&lt;name&gt;.skipFetchAll</c> set: not <c>git fetch --all</c>, whose fetches print no
    /// progress (D109). Every call but the last gets <c>--no-auto-maintenance</c>, so git's
    /// maintenance runs once, as <c>--all</c> runs it; with <paramref name="noAutoMaintenance"/>
    /// (auto-fetch) every call does. Neither <c>--prune</c> nor <c>--tags</c> (D99). The fetch of
    /// all remotes goes on after a remote fails and then throws one <see cref="GitException"/>
    /// whose command and exit code are the first failed call's and whose standard error is every
    /// failed call's, in order, each followed by <c>error: Could not fetch &lt;remote&gt;</c>, as
    /// <c>git fetch --all</c> prints it (2.16). A repository without remotes fetches nothing.
    /// Cancelling stops the running fetch and skips the rest.
    /// </summary>
    public async Task FetchAsync(string? remote, NetworkOptions options, bool noAutoMaintenance = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (remote is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(remote);
            await RunRemoteCommandAsync(RemoteArguments.Fetch(remote, noAutoMaintenance), options, cancellationToken).ConfigureAwait(false);
            return;
        }

        var remotes = await ReadRemotesToFetchAsync(cancellationToken).ConfigureAwait(false);
        var failures = new List<(string Remote, GitCommand Command, GitResult Result)>();
        for (var i = 0; i < remotes.Count; i++)
        {
            var last = i == remotes.Count - 1;
            var command = RemoteCommand(RemoteArguments.Fetch(remotes[i], noAutoMaintenance || !last), options);
            var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                failures.Add((remotes[i], command, result));
            }
        }

        if (failures.Count == 0)
        {
            return;
        }

        var standardError = string.Concat(failures.Select(failure =>
            RemoteArguments.EndWithLineFeed(failure.Result.StandardError) + $"error: Could not fetch {failure.Remote}\n"));
        var standardOutput = string.Concat(failures.Select(failure => RemoteArguments.EndWithLineFeed(failure.Result.StandardOutput)));
        var first = failures[0];
        throw new GitException(first.Command.DisplayText, first.Result.ExitCode, standardError, standardOutput: standardOutput);
    }

    /// <summary>
    /// Pull's second step (2.18, D84): what <c>git pull</c> runs after its fetch, chosen as it
    /// chooses from <c>pull.ff</c>, <c>branch.&lt;b&gt;.rebase</c> and <c>pull.rebase</c>:
    /// <list type="bullet">
    /// <item><c>pull.ff = only</c> wins over a rebase setting: <c>git merge --ff-only &lt;head&gt;</c>.</item>
    /// <item>A rebase (<c>branch.&lt;b&gt;.rebase</c>, or when unset <c>pull.rebase</c>, is true,
    /// <c>interactive</c>, <c>i</c> or <c>merges</c>; booleans are read by git's own rules, under
    /// which Git 2.36 reads <c>m</c> as false): as git pull, a fast-forward when
    /// the branch has nothing of its own (<c>git merge --ff-only &lt;head&gt;</c>), otherwise
    /// <c>git rebase</c> without arguments, which rebases onto the configured upstream in
    /// fork-point mode, with <c>--rebase-merges</c> for <c>merges</c>; <c>interactive</c> runs
    /// as a plain rebase, which is what git's would do without stopping (D102).</item>
    /// <item>Otherwise <c>git merge --no-edit [--ff | --no-ff] &lt;head&gt;</c>, also when nothing is
    /// set (D78), where Git 2.33.1 and later would refuse diverged branches.</item>
    /// </list>
    /// <c>&lt;head&gt;</c> is <c>FETCH_HEAD</c>, which the fetch of the upstream's remote has just
    /// written, so a merge gets git pull's message ("Merge branch 'main' of &lt;url&gt;"). For a
    /// local upstream (no fetch) it is <see cref="GitRef.UpstreamRef"/>, and a merge commit gets
    /// the message <c>git fmt-merge-msg</c> writes for that branch, which is git pull's too (2.8).
    /// A value of <c>pull.ff</c> or of a rebase setting that git would refuse (<c>preserve</c>,
    /// removed in Git 2.34, or a typo) throws <see cref="GitException"/> with the sentence
    /// "&lt;key&gt; is set to &lt;value&gt;, which this app does not understand." as its standard
    /// error, before anything changes. Conflicts are returned, not thrown: git failed and the
    /// index then has conflicted files (it had none before).
    /// </summary>
    public async Task<PullResult> IntegratePullAsync(GitRef branch, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (branch.Kind != RefKind.LocalBranch || branch.UpstreamRef is null)
        {
            throw new ArgumentException($"{branch.Name} is not a local branch with an upstream.", nameof(branch));
        }

        options ??= NetworkOptions.None;
        var ff = await ReadPullFastForwardAsync(cancellationToken).ConfigureAwait(false);
        var rebase = await ReadPullRebaseAsync(branch.Name, cancellationToken).ConfigureAwait(false);
        var head = branch.HasLocalUpstream ? branch.UpstreamRef : "FETCH_HEAD";
        var before = await ReadPullHeadAsync(cancellationToken).ConfigureAwait(false);
        var conflictsBefore = await HasUnmergedEntriesAsync(cancellationToken).ConfigureAwait(false);

        string[] arguments;
        var rebased = false;
        if (ff == PullFastForward.Only)
        {
            arguments = ["merge", "--ff-only", head];
        }
        else if (rebase != PullRebase.None)
        {
            // git pull fast-forwards a branch that has nothing of its own instead of rebasing it.
            if (before is null || await PullCanFastForwardAsync(before, head, cancellationToken).ConfigureAwait(false))
            {
                arguments = ["merge", "--ff-only", head];
            }
            else
            {
                arguments = rebase == PullRebase.Merges ? ["rebase", "--rebase-merges"] : ["rebase"];
                rebased = true;
            }
        }
        else
        {
            var merge = new List<string> { "merge", "--no-edit" };
            if (ff == PullFastForward.Always)
            {
                merge.Add("--ff");
            }
            else if (ff == PullFastForward.Never)
            {
                merge.Add("--no-ff");
            }

            if (branch.HasLocalUpstream && await PullMergeMessageAsync(branch.UpstreamRef, cancellationToken).ConfigureAwait(false) is { } message)
            {
                merge.AddRange(["--no-log", "-m", message]);
            }

            merge.Add(head);
            arguments = [.. merge];
        }

        var command = RemoteCommand(arguments, options);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            if (!conflictsBefore && await HasUnmergedEntriesAsync(cancellationToken).ConfigureAwait(false))
            {
                return new PullResult(PullOutcome.Conflicts, 0);
            }

            result.EnsureSuccess(command);
        }

        var after = await ReadPullHeadAsync(cancellationToken).ConfigureAwait(false);
        if (after is null || after == before)
        {
            return new PullResult(PullOutcome.UpToDate, 0);
        }

        var count = await CountPulledCommitsAsync(before, cancellationToken).ConfigureAwait(false);
        if (rebased)
        {
            return new PullResult(PullOutcome.Rebased, count);
        }

        // A merge commit has the old HEAD as its first parent and a second one; anything else that
        // moved HEAD forward was a fast-forward.
        var parents = (await ReadOutputAsync(cancellationToken, "rev-list", "--parents", "-n", "1", after).ConfigureAwait(false))
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var merged = parents.Length > 2 && parents[1] == before;
        return new PullResult(merged ? PullOutcome.Merged : PullOutcome.FastForward, count);
    }

    /// <summary>
    /// <c>git push --progress [--set-upstream] [--force-with-lease=&lt;remote ref&gt; --force-if-includes]
    /// &lt;remote&gt; refs/heads/&lt;branch&gt;:&lt;remote ref&gt;</c> (2.19, 2.20, D100). The lease names
    /// no expected value, so that git also applies <c>--force-if-includes</c>.
    /// </summary>
    public Task PushAsync(PushRequest request, NetworkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Branch);
        RemoteArguments.RequireRemote(request.Remote);
        RemoteArguments.RequireFullRef(request.RemoteRef, nameof(request));

        var arguments = new List<string> { "push", "--progress" };
        if (request.SetUpstream)
        {
            arguments.Add("--set-upstream");
        }

        if (request.ForceWithLease)
        {
            arguments.Add("--force-with-lease=" + request.RemoteRef);
            arguments.Add("--force-if-includes");
        }

        arguments.Add(request.Remote);
        arguments.Add($"{HeadsPrefix}{request.Branch}:{request.RemoteRef}");
        return RunRemoteCommandAsync([.. arguments], options, cancellationToken);
    }

    /// <summary><c>git branch --set-upstream-to=&lt;ref&gt; &lt;branch&gt;</c>: a remote branch, or a local branch for a local upstream (2.21).</summary>
    public Task SetUpstreamAsync(string branch, string remoteBranchRef, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        RemoteArguments.RequireFullRef(remoteBranchRef, nameof(remoteBranchRef));
        return WriteAsync(cancellationToken, "branch", "--set-upstream-to=" + remoteBranchRef, branch);
    }

    /// <summary><c>git branch --unset-upstream &lt;branch&gt;</c> (2.21). It also removes an upstream that no longer exists.</summary>
    public Task UnsetUpstreamAsync(string branch, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        return WriteAsync(cancellationToken, "branch", "--unset-upstream", branch);
    }

    /// <summary>
    /// <c>git push --progress --force-with-lease=refs/heads/&lt;b&gt;:&lt;sha&gt; &lt;remote&gt; --delete
    /// refs/heads/&lt;b&gt;</c> (2.22, D100): refused (<c>stale info</c>) when the branch on the remote
    /// is no longer at <paramref name="expectedCommit"/>.
    /// </summary>
    public Task DeleteRemoteBranchAsync(string remote, string branch, string expectedCommit, NetworkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        return DeleteOnRemoteAsync(remote, HeadsPrefix + branch, expectedCommit, options, cancellationToken);
    }

    /// <summary>
    /// The configured remotes, read without optional locks, sorted by name (2.23): the names
    /// <c>git remote</c> lists, and for each the first <c>remote.&lt;name&gt;.url</c> and
    /// <c>remote.&lt;name&gt;.pushurl</c> as they are configured (<c>git config --get-regexp</c>),
    /// not as <c>git remote -v</c> prints them after a <c>url.&lt;base&gt;.insteadOf</c> rule has
    /// rewritten them: such a rule, often a global one, can put a token into the URL, which the
    /// Change URL form would then show and write into the repository's own settings. A push URL
    /// equal to the fetch URL is null. URLs keep the credentials configured in them; the call
    /// record hides them (D103), and <see cref="RemoteInfo.ShownUrl"/> is what the app shows.
    /// </summary>
    public async Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync(CancellationToken cancellationToken = default)
    {
        var names = SplitLines(await ReadOutputAsync(cancellationToken, "remote").ConfigureAwait(false)).Distinct(StringComparer.Ordinal).ToList();
        var fetchUrls = new Dictionary<string, string>(StringComparer.Ordinal);
        var pushUrls = new Dictionary<string, string>(StringComparer.Ordinal);

        // Section and key names are printed in lower case; the remote's name (the subsection)
        // keeps its case and may contain dots. --null prints "<key>\n<value>\0" per entry, in the
        // order git reads them. Exit 1: no such setting.
        var command = Read("config", "--null", "--get-regexp", @"^remote\..*\.(url|pushurl)$");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 1)
        {
            const string Prefix = "remote.";
            foreach (var entry in result.EnsureSuccess(command).StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                // A key without a value ("[remote "x"] url") has no line feed; git refuses it as a URL.
                var newline = entry.IndexOf('\n', StringComparison.Ordinal);
                var key = newline < 0 ? entry : entry[..newline];
                var value = newline < 0 ? string.Empty : entry[(newline + 1)..];
                if (!key.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (RemoteArguments.TrimSuffix(key[Prefix.Length..], ".pushurl") is { Length: > 0 } pushName)
                {
                    pushUrls.TryAdd(pushName, value);
                }
                else if (RemoteArguments.TrimSuffix(key[Prefix.Length..], ".url") is { Length: > 0 } fetchName)
                {
                    fetchUrls.TryAdd(fetchName, value);
                }
            }
        }

        names.Sort(StringComparer.Ordinal);
        return [.. names.Select(name =>
        {
            var fetchUrl = fetchUrls.GetValueOrDefault(name, string.Empty);
            var pushUrl = pushUrls.GetValueOrDefault(name);
            return new RemoteInfo(name, fetchUrl, pushUrl is null || pushUrl == fetchUrl ? null : pushUrl);
        })];
    }

    /// <summary><c>git remote add -- &lt;name&gt; &lt;url&gt;</c> (2.23): the <c>--</c> keeps a URL that starts with a dash from being read as an option.</summary>
    public Task AddRemoteAsync(string name, string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return WriteAsync(cancellationToken, "remote", "add", "--", name, url);
    }

    /// <summary><c>git remote rename &lt;old&gt; &lt;new&gt;</c> (2.23): its remote branches and the upstreams that name it move with it.</summary>
    public Task RenameRemoteAsync(string name, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        return WriteAsync(cancellationToken, "remote", "rename", name, newName);
    }

    /// <summary><c>git remote set-url -- &lt;name&gt; &lt;url&gt;</c> (2.23).</summary>
    public Task SetRemoteUrlAsync(string name, string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return WriteAsync(cancellationToken, "remote", "set-url", "--", name, url);
    }

    /// <summary>
    /// <c>git remote remove &lt;name&gt;</c> (2.23): its remote branches go, and the branches that
    /// track it lose their upstream. The caller saves the backups of its remote branches first
    /// (<see cref="SaveBackupRefsAsync"/>). Git 2.36 refuses a <c>--</c> here; a valid remote name
    /// never starts with a dash.
    /// </summary>
    public Task RemoveRemoteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return WriteAsync(cancellationToken, "remote", "remove", name);
    }

    /// <summary><c>git push --progress &lt;remote&gt; refs/tags/&lt;t&gt;:refs/tags/&lt;t&gt;</c> (2.29).</summary>
    public Task PushTagAsync(string remote, string tag, NetworkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(options);
        RemoteArguments.RequireRemote(remote);
        return RunRemoteCommandAsync(["push", "--progress", remote, $"{TagsPrefix}{tag}:{TagsPrefix}{tag}"], options, cancellationToken);
    }

    /// <summary>
    /// <c>git push --progress --force-with-lease=refs/tags/&lt;t&gt;:&lt;sha&gt; &lt;remote&gt; --delete
    /// refs/tags/&lt;t&gt;</c> (2.29, D100): refused (<c>stale info</c>) when the remote's tag is not the
    /// tag object <paramref name="expectedObject"/>, or the remote has no such tag.
    /// </summary>
    public Task DeleteRemoteTagAsync(string remote, string tag, string expectedObject, NetworkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return DeleteOnRemoteAsync(remote, TagsPrefix + tag, expectedObject, options, cancellationToken);
    }

    private Task DeleteOnRemoteAsync(string remote, string fullRef, string expected, NetworkOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);
        ArgumentNullException.ThrowIfNull(options);
        RemoteArguments.RequireRemote(remote);
        return RunRemoteCommandAsync(["push", "--progress", $"--force-with-lease={fullRef}:{expected}", remote, "--delete", fullRef], options, cancellationToken);
    }

    /// <summary>
    /// The remotes a fetch of all remotes fetches: <c>git remote</c>'s list, in its order, without
    /// those whose <c>remote.&lt;name&gt;.skipFetchAll</c> is true (read as git reads a boolean).
    /// </summary>
    private async Task<IReadOnlyList<string>> ReadRemotesToFetchAsync(CancellationToken cancellationToken)
    {
        var remotes = SplitLines(await ReadOutputAsync(cancellationToken, "remote").ConfigureAwait(false));
        if (remotes.Count == 0)
        {
            return remotes;
        }

        // Section and key names are printed in lower case; the remote's name (the subsection)
        // keeps its case and may contain dots. --null prints "<key>\n<value>\0" per entry.
        // Exit 1: no such setting.
        var command = Read("config", "--null", "--type=bool", "--get-regexp", @"^remote\..*\.skipfetchall$");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return remotes;
        }

        const string Prefix = "remote.";
        const string Suffix = ".skipfetchall";
        var skipped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in result.EnsureSuccess(command).StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var newline = entry.IndexOf('\n', StringComparison.Ordinal);
            if (newline < 0)
            {
                continue;
            }

            var key = entry[..newline];
            if (entry[(newline + 1)..] == "true" && key.Length > Prefix.Length + Suffix.Length)
            {
                skipped.Add(key[Prefix.Length..^Suffix.Length]);
            }
        }

        return [.. remotes.Where(remote => !skipped.Contains(remote))];
    }

    /// <summary>What <c>pull.ff</c> asks of a pull's merge.</summary>
    private enum PullFastForward
    {
        Unset,
        Always,
        Never,
        Only,
    }

    /// <summary>What a pull's rebase setting asks for.</summary>
    private enum PullRebase
    {
        None,
        Rebase,
        Merges,
    }

    /// <summary><c>pull.ff</c> as git pull reads it: a boolean (<c>--ff</c> or <c>--no-ff</c>), or <c>only</c>.</summary>
    private async Task<PullFastForward> ReadPullFastForwardAsync(CancellationToken cancellationToken)
    {
        const string Key = "pull.ff";
        if (await ReadPullSettingAsync(Key, cancellationToken).ConfigureAwait(false) is not { } value)
        {
            return PullFastForward.Unset;
        }

        if (await ReadPullBoolAsync(Key, value, cancellationToken).ConfigureAwait(false) is { } flag)
        {
            return flag ? PullFastForward.Always : PullFastForward.Never;
        }

        return value == "only" ? PullFastForward.Only : throw RemoteArguments.NotUnderstood(Key, value);
    }

    /// <summary>
    /// <c>branch.&lt;b&gt;.rebase</c>, or when it is unset <c>pull.rebase</c>, as git pull reads it:
    /// a boolean, <c>merges</c> (<c>m</c>) or <c>interactive</c> (<c>i</c>).
    /// </summary>
    private async Task<PullRebase> ReadPullRebaseAsync(string branch, CancellationToken cancellationToken)
    {
        var key = $"branch.{branch}.rebase";
        var value = await ReadPullSettingAsync(key, cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            key = "pull.rebase";
            value = await ReadPullSettingAsync(key, cancellationToken).ConfigureAwait(false);
        }

        if (value is null)
        {
            return PullRebase.None;
        }

        if (await ReadPullBoolAsync(key, value, cancellationToken).ConfigureAwait(false) is { } flag)
        {
            return flag ? PullRebase.Rebase : PullRebase.None;
        }

        return value switch
        {
            "merges" or "m" => PullRebase.Merges,
            "interactive" or "i" => PullRebase.Rebase,
            _ => throw RemoteArguments.NotUnderstood(key, value),
        };
    }

    /// <summary>A setting's value (<c>git config --get</c>, the last one wins), or null when it is not set.</summary>
    private async Task<string?> ReadPullSettingAsync(string key, CancellationToken cancellationToken)
    {
        var command = Read("config", "--get", key);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 1 ? null : result.EnsureSuccess(command).StandardOutput.TrimEnd('\r', '\n');
    }

    /// <summary>
    /// <paramref name="value"/> of <paramref name="key"/> read as git reads a boolean, or null
    /// when it is not one. The common words are read here; anything else is asked of git
    /// (<c>git config --type=bool</c>), which knows its own rules: a key without a value is true,
    /// a number is a boolean, and so is <c>m</c>, which Git 2.36 reads as the number 0 with the
    /// unit "mega" before it would read it as <c>merges</c> (git pull then merges).
    /// </summary>
    private async Task<bool?> ReadPullBoolAsync(string key, string value, CancellationToken cancellationToken)
    {
        switch (value.ToLowerInvariant())
        {
            case "true" or "yes" or "on":
                return true;
            case "false" or "no" or "off":
                return false;
        }

        var result = await _runner.RunAsync(Read("config", "--type=bool", "--get", key), cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? result.StandardOutput.Trim() == "true" : null;
    }

    /// <summary>
    /// The message <c>git fmt-merge-msg</c> writes for merging the local upstream
    /// <paramref name="upstreamRef"/>, as git pull, which fetches it from <c>.</c>, would write it;
    /// null when it is neither a branch nor a remote branch, and git then writes its own.
    /// </summary>
    private async Task<string?> PullMergeMessageAsync(string upstreamRef, CancellationToken cancellationToken)
    {
        string description;
        if (upstreamRef.StartsWith(HeadsPrefix, StringComparison.Ordinal))
        {
            description = $"branch '{upstreamRef[HeadsPrefix.Length..]}'";
        }
        else if (upstreamRef.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            description = $"remote-tracking branch '{upstreamRef[RemotesPrefix.Length..]}'";
        }
        else
        {
            return null;
        }

        var sha = (await ReadOutputAsync(cancellationToken, "rev-parse", "--verify", "--quiet", upstreamRef + "^{commit}").ConfigureAwait(false)).Trim();
        var command = new GitCommand("fmt-merge-msg")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            StandardInput = $"{sha}\t\t{description} of .\n",
        };
        var message = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput.TrimEnd();
        return message.Length > 0 ? message : null;
    }

    /// <summary>The commit HEAD points at, or null when its branch has no commit yet.</summary>
    private async Task<string?> ReadPullHeadAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(Read("rev-parse", "--verify", "--quiet", "HEAD"), cancellationToken).ConfigureAwait(false);
        var sha = result.StandardOutput.Trim();
        return result.Succeeded && sha.Length > 0 ? sha : null;
    }

    /// <summary>Whether <paramref name="head"/> is <paramref name="before"/> or descends from it (<c>git merge-base --is-ancestor</c>: exit 0 yes, 1 no).</summary>
    private async Task<bool> PullCanFastForwardAsync(string before, string head, CancellationToken cancellationToken)
    {
        var command = Read("merge-base", "--is-ancestor", before, head);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return false;
        }

        result.EnsureSuccess(command);
        return true;
    }

    /// <summary>The commits HEAD has that <paramref name="before"/> lacks (all of HEAD's when it was unborn).</summary>
    private async Task<int> CountPulledCommitsAsync(string? before, CancellationToken cancellationToken)
    {
        var range = before is null ? "HEAD" : $"{before}..HEAD";
        var output = await ReadOutputAsync(cancellationToken, "rev-list", "--count", range).ConfigureAwait(false);
        return int.Parse(output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>Whether the index has conflicted entries (<c>git ls-files --unmerged</c>).</summary>
    private async Task<bool> HasUnmergedEntriesAsync(CancellationToken cancellationToken) =>
        (await ReadOutputAsync(cancellationToken, "ls-files", "--unmerged", "-z").ConfigureAwait(false)).Length > 0;

    /// <summary>A call that may talk to a remote or run a filter that prompts: the operation's variables, and its progress.</summary>
    private GitCommand RemoteCommand(string[] arguments, NetworkOptions options)
    {
        var progress = options.Progress;
        return new GitCommand(arguments)
        {
            WorkingDirectory = WorkingDirectory,
            Environment = options.Environment,
            OnErrorLine = progress is null
                ? null
                : line =>
                {
                    if (GitProgressParser.Parse(line) is { } parsed)
                    {
                        progress.Report(parsed);
                    }
                },
        };
    }

    private async Task RunRemoteCommandAsync(string[] arguments, NetworkOptions options, CancellationToken cancellationToken)
    {
        var command = RemoteCommand(arguments, options);
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    /// <summary>The pure parts of <see cref="GitRepository"/>'s remote calls, kept out of the partial class so that their names cannot meet another package's.</summary>
    private static class RemoteArguments
    {
        /// <summary>The name of a local upstream's remote (<c>branch.&lt;b&gt;.remote = .</c>, D101).</summary>
        private const string LocalRemote = ".";

        public static string[] Fetch(string remote, bool noAutoMaintenance) =>
            noAutoMaintenance ? ["fetch", "--progress", "--no-auto-maintenance", remote] : ["fetch", "--progress", remote];

        public static void RequireRemote(string remote)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(remote);
            if (remote == LocalRemote)
            {
                throw new ArgumentException("A local upstream (remote \".\") is not pushed to (D101).", nameof(remote));
            }
        }

        public static void RequireFullRef(string fullRef, string parameter)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullRef, parameter);
            if (!fullRef.StartsWith("refs/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Not a full ref name: {fullRef}", parameter);
            }
        }

        public static GitException NotUnderstood(string key, string value) =>
            new($"git config --get {key}", 0, $"{key} is set to {value}, which this app does not understand.");

        public static string? TrimSuffix(string text, string suffix) =>
            text.EndsWith(suffix, StringComparison.Ordinal) ? text[..^suffix.Length] : null;

        public static string EndWithLineFeed(string text) =>
            text.Length == 0 || text.EndsWith('\n') ? text : text + "\n";
    }
}
