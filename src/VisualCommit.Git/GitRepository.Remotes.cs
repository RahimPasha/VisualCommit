using System.Globalization;
using System.Text.RegularExpressions;
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
    /// Cancelling stops the running fetch and skips the rest; so does a prompt the user cancelled
    /// (<see cref="NetworkOptions.PromptCancelled"/>), after which the fetch that asked fails and
    /// this throws <see cref="OperationCanceledException"/> instead of asking for the next remote.
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
                if (options.PromptCancelled?.Invoke() == true)
                {
                    // The user cancelled a prompt: the fetch is cancelled, and the next remote would ask again (2.16).
                    throw new OperationCanceledException($"A prompt of the fetch from {remotes[i]} was cancelled.");
                }
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
    /// Pull's second step (2.18, D84) as git is configured: the same as
    /// <see cref="IntegratePullAsync(GitRef, PullIntegration, Func{string, Task}?, NetworkOptions?, PullIntegrationOptions?, CancellationToken)"/>
    /// with <see cref="PullIntegration.AsConfigured"/> and no callback.
    /// </summary>
    public Task<PullResult> IntegratePullAsync(GitRef branch, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        IntegratePullAsync(branch, PullIntegration.AsConfigured, null, options, cancellationToken: cancellationToken);

    /// <summary>
    /// Pull's second step (2.18, D84; docs/phase-4-design.md section 6, D147): what <c>git pull</c>
    /// runs after its fetch. With <see cref="PullIntegration.AsConfigured"/> it is chosen as git
    /// pull chooses from <c>pull.ff</c>, <c>branch.&lt;b&gt;.rebase</c> and <c>pull.rebase</c>:
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
    /// <see cref="PullIntegration.Rebase"/> ("Pull from {up} (rebase)") follows none of these
    /// settings, as <c>git pull --rebase</c> ignores them (a rebase setting is not even read, an
    /// invalid one included; <c>pull.ff = only</c> gives way): the fast-forward when the branch has
    /// nothing of its own, otherwise <c>git rebase</c> without arguments. It reads <c>pull.ff</c>
    /// all the same and refuses a value git would refuse, as <c>git pull --rebase</c> does before it
    /// fetches.
    /// <para>
    /// Before either pull rebases, <paramref name="beforeRebase"/> is called with the branch's old
    /// tip (the app saves the rebase's backup there, 1.2); not before a fast-forward or a merge.
    /// A rebase is classified by 1.4's rules (<see cref="ClassifyRebaseAsync"/>): every stop,
    /// with or without conflicted files, is <see cref="PullOutcome.Conflicts"/>, and a refusal
    /// throws. The commits that git's fork point leaves out are counted before the rebase
    /// (<see cref="PullResult.LeftOutByForkPoint"/>, <see cref="CountLeftOutByForkPointAsync"/>).
    /// </para>
    /// <c>&lt;head&gt;</c> is <c>FETCH_HEAD</c>, which the fetch of the upstream's remote has just
    /// written, so a merge gets git pull's message ("Merge branch 'main' of &lt;url&gt;"). For a
    /// local upstream (no fetch) it is <see cref="GitRef.UpstreamRef"/>, and a merge commit gets
    /// the message <c>git fmt-merge-msg</c> writes for that branch, which is git pull's too (2.8).
    /// A value of <c>pull.ff</c> or of a rebase setting that git would refuse (<c>preserve</c>,
    /// removed in Git 2.34, or a typo) throws <see cref="GitException"/> with the sentence
    /// "&lt;key&gt; is set to &lt;value&gt;, which this app does not understand." as its standard
    /// error, before anything changes; for <see cref="PullIntegration.Rebase"/> only a value of
    /// <c>pull.ff</c> does, as <c>git pull --rebase</c> reads <c>pull.ff</c> and no rebase setting. A
    /// merge's conflicts are returned, not thrown: git failed
    /// and the index then has conflicted files (it had none before). When the fetch marked nothing for
    /// merge, because the upstream was deleted on its remote (<c>FETCH_HEAD</c> has only
    /// <c>not-for-merge</c> lines, while the remote branch stays without a prune, D99), nothing runs
    /// and <see cref="PullOutcome.UpstreamGone"/> is returned: git pull fails there with "no such
    /// ref was fetched", and a merge of <c>FETCH_HEAD</c> would only say "Already up to date".
    /// <para>
    /// <paramref name="pullOptions"/> (docs/phase-6-design.md 8.9, 9.10): with
    /// <see cref="PullIntegration.AsConfigured"/>, its <see cref="PullIntegrationOptions.Configuration"/>,
    /// read before the fetch, decides instead of a read here (<see cref="ReadPullConfigurationAsync"/>,
    /// whose <see cref="PullConfiguration.Problem"/> throws <see cref="PullConfiguration.NotUnderstood"/>
    /// either way), so a terminal's change during the fetch is not seen, as with git pull; with
    /// <see cref="PullIntegration.Rebase"/> only its problem with <c>pull.ff</c> counts (thrown), and
    /// nothing is read here. Keep ignored files' check runs once
    /// the integration is chosen, before anything is written (<see cref="KeepIgnoredInPullAsync"/>).
    /// </para>
    /// </summary>
    public async Task<PullResult> IntegratePullAsync(
        GitRef branch,
        PullIntegration integration,
        Func<string, Task>? beforeRebase,
        NetworkOptions? options = null,
        PullIntegrationOptions? pullOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        if (branch.Kind != RefKind.LocalBranch || branch.UpstreamRef is null)
        {
            throw new ArgumentException($"{branch.Name} is not a local branch with an upstream.", nameof(branch));
        }

        options ??= NetworkOptions.None;
        PullFastForwardSetting ff;
        PullRebaseSetting rebase;
        switch (integration)
        {
            case PullIntegration.AsConfigured:
                // The settings Pull read before its fetch (8.9), or, given none, read here as phase 4's pull did.
                var configuration = pullOptions?.Configuration
                    ?? await ReadPullConfigurationAsync(branch.Name, cancellationToken).ConfigureAwait(false);
                if (configuration.Problem is not null)
                {
                    throw configuration.NotUnderstood();
                }

                ff = configuration.FastForward;
                rebase = configuration.Rebase;
                break;
            case PullIntegration.Rebase:
                // git pull --rebase reads no rebase setting (an invalid one included), but pull.ff,
                // which it reads whenever no --ff option is given, and refuses an invalid value before
                // it fetches (fatal: invalid value for 'pull.ff', 2.36 and 2.55); pull.ff = only and
                // false give way to the rebase. So the configuration given counts only for a pull.ff
                // problem, and without one only pull.ff is read.
                if (pullOptions?.Configuration is { } given)
                {
                    if (given.ProblemKey == PullConfigKeys.FastForward)
                    {
                        throw given.NotUnderstood();
                    }
                }
                else if ((await ReadPullFastForwardAsync(cancellationToken).ConfigureAwait(false)).Refused is { } refused)
                {
                    throw refused.NotUnderstood();
                }

                ff = PullFastForwardSetting.Unset;
                rebase = PullRebaseSetting.Rebase;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(integration), integration, "Unknown pull integration.");
        }

        if (!branch.HasLocalUpstream && FetchMarkedNothingForMerge())
        {
            // The upstream was deleted on its remote: git merge FETCH_HEAD would say "Already up
            // to date", and FETCH_HEAD names another branch, which a rebase would be checked against.
            return new PullResult(PullOutcome.UpstreamGone, 0);
        }

        var head = branch.HasLocalUpstream ? branch.UpstreamRef : "FETCH_HEAD";
        var before = await ReadPullHeadAsync(cancellationToken).ConfigureAwait(false);

        string[] arguments;
        var rebased = false;
        if (ff == PullFastForwardSetting.Only)
        {
            arguments = ["merge", "--ff-only", head];
        }
        else if (rebase != PullRebaseSetting.None)
        {
            // git pull fast-forwards a branch that has nothing of its own instead of rebasing it.
            if (before is null || await PullCanFastForwardAsync(before, head, cancellationToken).ConfigureAwait(false))
            {
                arguments = ["merge", "--ff-only", head];
            }
            else
            {
                arguments = rebase == PullRebaseSetting.Merges ? ["rebase", "--rebase-merges"] : ["rebase"];
                rebased = true;
            }
        }
        else
        {
            var merge = new List<string> { "merge", "--no-edit" };
            if (ff == PullFastForwardSetting.Always)
            {
                merge.Add("--ff");
            }
            else if (ff == PullFastForwardSetting.Never)
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

        // Keep ignored files (9.10): nothing is integrated while an ignored file is in the way.
        var keep = await KeepIgnoredInPullAsync(branch, head, before, arguments, rebased, pullOptions, cancellationToken).ConfigureAwait(false);
        if (keep.Ignored.Count > 0)
        {
            return new PullResult(PullOutcome.IgnoredFilesInTheWay, 0) { IgnoredFiles = keep.Ignored, WouldRebase = rebased };
        }

        arguments = keep.Arguments;
        if (rebased)
        {
            return await PullRebaseAsync(branch.UpstreamRef, before!, arguments, beforeRebase, options, cancellationToken).ConfigureAwait(false);
        }

        var conflictsBefore = await HasUnmergedEntriesAsync(cancellationToken).ConfigureAwait(false);
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

        // A merge commit has the old HEAD as its first parent and a second one; anything else that
        // moved HEAD forward was a fast-forward.
        var parents = (await ReadOutputAsync(cancellationToken, "rev-list", "--parents", "-n", "1", after).ConfigureAwait(false))
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var merged = parents.Length > 2 && parents[1] == before;
        return new PullResult(merged ? PullOutcome.Merged : PullOutcome.FastForward, count);
    }

    /// <summary>
    /// A pull's rebase (section 6): counts what the fork point leaves out, calls
    /// <paramref name="beforeRebase"/> with the old tip <paramref name="before"/>, runs
    /// <paramref name="arguments"/> (<c>git rebase</c>, with <c>--rebase-merges</c> for
    /// <c>pull.rebase=merges</c>) and classifies it (1.4): any stop is
    /// <see cref="PullOutcome.Conflicts"/>, a refusal throws, HEAD where it was is
    /// <see cref="PullOutcome.UpToDate"/>. Changes <c>rebase.autoStash</c> put back with
    /// conflicts are told by <see cref="PullResult.AutostashConflicted"/>.
    /// </summary>
    private async Task<PullResult> PullRebaseAsync(
        string upstreamRef,
        string before,
        string[] arguments,
        Func<string, Task>? beforeRebase,
        NetworkOptions options,
        CancellationToken cancellationToken)
    {
        var leftOut = await CountLeftOutByForkPointAsync(upstreamRef, cancellationToken).ConfigureAwait(false);
        if (beforeRebase is not null)
        {
            await beforeRebase(before).ConfigureAwait(false);
        }

        var folderBefore = RebaseFolderExists();
        var command = RemoteCommand(arguments, options);
        var result = RebaseText.WithoutEraseLine(await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false));
        var rebase = await ClassifyRebaseAsync(command, result, folderBefore, cancellationToken).ConfigureAwait(false);
        if (rebase.Outcome == RebaseOutcome.Stopped)
        {
            return new PullResult(PullOutcome.Conflicts, 0) { LeftOutByForkPoint = leftOut };
        }

        var after = await ReadPullHeadAsync(cancellationToken).ConfigureAwait(false);
        if (after is null || after == before)
        {
            return new PullResult(PullOutcome.UpToDate, 0) { LeftOutByForkPoint = leftOut, AutostashConflicted = rebase.AutostashConflicted };
        }

        var count = await CountPulledCommitsAsync(before, cancellationToken).ConfigureAwait(false);
        return new PullResult(PullOutcome.Rebased, count) { LeftOutByForkPoint = leftOut, AutostashConflicted = rebase.AutostashConflicted };
    }

    /// <summary>
    /// The branch's own commits that <c>git rebase</c> without arguments leaves out because
    /// <paramref name="upstreamRef"/> once held them (0.1, section 6): git replays
    /// <c>&lt;upstream&gt;...HEAD</c> (the right side, no merges, patches already upstream left
    /// out) restricted to the commits not reachable from the fork point, <c>git merge-base
    /// --fork-point &lt;upstream&gt; HEAD</c> (found in the upstream's reflog). The difference of the
    /// two counts, read before the rebase, so that a commit the rebase drops because it became
    /// empty is not taken for one the fork point left out; 0 when git finds no fork point.
    /// </summary>
    private async Task<int> CountLeftOutByForkPointAsync(string upstreamRef, CancellationToken cancellationToken)
    {
        var forkPoint = await _runner.RunAsync(Read("merge-base", "--fork-point", upstreamRef, "HEAD"), cancellationToken).ConfigureAwait(false);
        var fork = forkPoint.StandardOutput.Trim();
        if (!forkPoint.Succeeded || fork.Length == 0)
        {
            return 0;
        }

        var range = $"{upstreamRef}...HEAD";
        var all = ParseCount(await ReadOutputAsync(cancellationToken, "rev-list", "--count", "--no-merges", "--right-only", "--cherry-pick", range, "--").ConfigureAwait(false));
        var replayed = ParseCount(await ReadOutputAsync(cancellationToken, "rev-list", "--count", "--no-merges", "--right-only", "--cherry-pick", range, "^" + fork, "--").ConfigureAwait(false));
        return Math.Max(0, all - replayed);
    }

    /// <summary>
    /// <c>git push --progress [--set-upstream] [--force-with-lease=&lt;remote ref&gt; --force-if-includes]
    /// &lt;remote&gt; refs/heads/&lt;branch&gt;:&lt;remote ref&gt;</c> (2.19, 2.20, D100). The lease names
    /// no expected value, so that git also applies <c>--force-if-includes</c>. What it changed on
    /// the remote is read from git's summary line (<see cref="ReadPushOutcomeAsync"/>).
    /// </summary>
    public async Task<PushOutcome> PushAsync(PushRequest request, NetworkOptions options, CancellationToken cancellationToken = default)
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
        var command = RemoteCommand([.. arguments], options);
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return await ReadPushOutcomeAsync(result.StandardError, HeadsPrefix + request.Branch, request.RemoteRef, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// <c>git push --progress &lt;remote&gt; refs/tags/&lt;t&gt;:refs/tags/&lt;t&gt;</c> (2.29). What it
    /// changed on the remote is read from git's summary line, as <see cref="PushAsync"/>'s: a new
    /// tag, or nothing when the remote has it already.
    /// </summary>
    public async Task<PushOutcome> PushTagAsync(string remote, string tag, NetworkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(options);
        RemoteArguments.RequireRemote(remote);
        var command = RemoteCommand(["push", "--progress", remote, $"{TagsPrefix}{tag}:{TagsPrefix}{tag}"], options);
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return await ReadPushOutcomeAsync(result.StandardError, TagsPrefix + tag, TagsPrefix + tag, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Whether the fetch just run wrote a <c>FETCH_HEAD</c> whose every line is
    /// <c>not-for-merge</c>: the upstream it would have marked for merge no longer exists on the
    /// remote, where <c>git pull</c> fails with "no such ref was fetched". A line for merge has
    /// an empty second field (<c>&lt;sha&gt;\t\tbranch 'main' of &lt;url&gt;</c>). False when there
    /// is no <c>FETCH_HEAD</c>, and git's merge then says what is wrong.
    /// </summary>
    private bool FetchMarkedNothingForMerge()
    {
        if (ReadGitFile("FETCH_HEAD") is not { } fetchHead)
        {
            return false;
        }

        return !fetchHead.Split('\n').Any(line => line.Split('\t') is [{ Length: > 0 }, { Length: 0 }, ..]);
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
        if (options.Configuration.Count > 0)
        {
            arguments = [.. options.Configuration.SelectMany(setting => new[] { "-c", setting }), .. arguments];
        }

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

    // ---- What a push changed on the remote (docs/phase-5-design.md 0.5, 7.7, D170) --------------

    /// <summary>
    /// What a push of the local ref <paramref name="source"/> to the remote ref
    /// <paramref name="destination"/> changed, read from git's summary line for it
    /// (<see cref="ReadPushSummary"/>), never from the remote-tracking ref, which can be stale
    /// (0.5): <c>[new branch]</c> or <c>[new tag]</c> created it; <c>&lt;old&gt;..&lt;new&gt;</c> and
    /// a forced update's <c>&lt;old&gt;...&lt;new&gt;</c> moved it. The line abbreviates the old
    /// value as this repository's unique abbreviation, which <c>rev-parse --disambiguate</c> gives
    /// back whole (the old value of a fast-forward is an ancestor of the new one, and a lease's is
    /// the remote-tracking ref's, so it is here); the new value is the source's. No line for it
    /// (<c>Everything up-to-date</c>) means nothing changed. An old value not found here leaves the
    /// ref out: its undo would have no lease.
    /// </summary>
    private async Task<PushOutcome> ReadPushOutcomeAsync(string standardError, string source, string destination, CancellationToken cancellationToken)
    {
        var shown = PushDisplayName(destination);
        var refs = new List<PushedRef>();
        foreach (var line in ReadPushSummary(standardError))
        {
            if (!string.Equals(line.Destination, shown, StringComparison.Ordinal) || line.Flag is not ('*' or ' ' or '+'))
            {
                continue;
            }

            var pushed = await ResolveAsync(source, cancellationToken).ConfigureAwait(false);
            if (line.NewAbbreviation is { } abbreviation && (pushed is null || !pushed.StartsWith(abbreviation, StringComparison.OrdinalIgnoreCase)))
            {
                pushed = await DisambiguateAsync(abbreviation, cancellationToken).ConfigureAwait(false);
            }

            string? old = null;
            if (line.OldAbbreviation is { } oldAbbreviation && (old = await DisambiguateAsync(oldAbbreviation, cancellationToken).ConfigureAwait(false)) is null)
            {
                _log.Warning($"The push of {source} to {destination} moved it from {oldAbbreviation}, which this repository does not have: it is not recorded.");
                continue;
            }

            if (pushed is not null)
            {
                refs.Add(new PushedRef(destination, old, pushed));
            }
        }

        return refs.Count == 0 ? PushOutcome.None : new PushOutcome(refs);
    }

    /// <summary>The one object whose id starts with <paramref name="abbreviation"/>, or null when none or several do.</summary>
    private async Task<string?> DisambiguateAsync(string abbreviation, CancellationToken cancellationToken)
    {
        var command = Read("rev-parse", "--disambiguate=" + abbreviation);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var ids = result.Succeeded ? SplitLines(result.StandardOutput) : [];
        return ids.Count == 1 ? ids[0] : null;
    }

    /// <summary>A ref's name as git's push summary shows it (<c>prettify_refname</c>): without <c>refs/heads/</c>, <c>refs/tags/</c> or <c>refs/remotes/</c>.</summary>
    private static string PushDisplayName(string fullName)
    {
        foreach (var prefix in new[] { HeadsPrefix, TagsPrefix, RemotesPrefix })
        {
            if (fullName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return fullName[prefix.Length..];
            }
        }

        return fullName;
    }

    /// <summary>
    /// The ref lines of a push's summary on standard error (0.5), as git 2.30 to 2.55 print them
    /// without <c>--porcelain</c>: a space, the flag, a space, the summary padded with spaces, then
    /// <c>&lt;from&gt; -&gt; &lt;to&gt;</c> (only <c>&lt;to&gt;</c> for a deletion) and a reason in
    /// parentheses: <c>   a1b2c3d..e4f5a6b  main -&gt; main</c>, <c> + a1b2c3d...e4f5a6b main -&gt; main
    /// (forced update)</c>, <c> * [new branch]      main -&gt; main</c>, <c> - [deleted]         x</c>,
    /// <c> ! [rejected]        main -&gt; main (stale info)</c>. Progress, <c>To &lt;url&gt;</c>,
    /// <c>remote:</c> and hint lines are left out.
    /// </summary>
    internal static IReadOnlyList<PushSummaryLine> ReadPushSummary(string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        var lines = new List<PushSummaryLine>();
        foreach (var raw in standardError.Split('\n'))
        {
            var match = PushSummaryPattern().Match(raw.TrimEnd('\r'));
            if (!match.Success)
            {
                continue;
            }

            var refs = match.Groups["refs"].Value;
            var arrow = refs.IndexOf(" -> ", StringComparison.Ordinal);
            var range = match.Groups["range"];
            lines.Add(new PushSummaryLine(
                match.Groups["flag"].Value[0],
                match.Groups["summary"].Value,
                arrow < 0 ? null : refs[..arrow],
                arrow < 0 ? refs : refs[(arrow + " -> ".Length)..],
                match.Groups["reason"].Success ? match.Groups["reason"].Value : null,
                range.Success ? match.Groups["old"].Value : null,
                range.Success ? match.Groups["new"].Value : null));
        }

        return lines;
    }

    [GeneratedRegex(@"^ (?<flag>[ +*=!-]) (?<summary>\[[^\]]+\]|(?<range>(?<old>[0-9a-f]{4,})\.\.\.?(?<new>[0-9a-f]{4,})))\s+(?<refs>\S+(?: -> \S+)?)(?: \((?<reason>[^)]*)\))?$")]
    private static partial Regex PushSummaryPattern();

    /// <summary>One ref line of a push's summary (<see cref="ReadPushSummary"/>).</summary>
    /// <param name="Flag">git's flag: <c>' '</c> a fast-forward, <c>'+'</c> a forced update, <c>'*'</c> a new ref, <c>'-'</c> a deletion, <c>'!'</c> rejected, <c>'='</c> up to date.</param>
    /// <param name="Summary">The summary: <c>a1b2c3d..e4f5a6b</c>, <c>[new branch]</c>, <c>[rejected]</c>.</param>
    /// <param name="Source">What was pushed as git shows it (a short name or a full SHA), or null for a deletion.</param>
    /// <param name="Destination">The remote ref as git shows it: <c>main</c> for <c>refs/heads/main</c>.</param>
    /// <param name="Reason">The reason in parentheses: <c>forced update</c>, <c>stale info</c>; null without one.</param>
    /// <param name="OldAbbreviation">The remote's old value, abbreviated, for an update; null otherwise.</param>
    /// <param name="NewAbbreviation">Its new value, abbreviated, for an update; null otherwise.</param>
    internal sealed record PushSummaryLine(char Flag, string Summary, string? Source, string Destination, string? Reason, string? OldAbbreviation, string? NewAbbreviation);

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

        /// <summary>
        /// Pull's sentence for a value of <paramref name="key"/> it does not understand, which
        /// <see cref="PullConfiguration.Problem"/> holds; <see cref="PullConfiguration.NotUnderstood"/>
        /// turns it into the failure (<c>git config --get &lt;key&gt;</c>, exit 0, the sentence).
        /// </summary>
        public static string NotUnderstood(string key, string value) =>
            $"{key} is set to {value}, which this app does not understand.";

        public static string? TrimSuffix(string text, string suffix) =>
            text.EndsWith(suffix, StringComparison.Ordinal) ? text[..^suffix.Length] : null;

        public static string EndWithLineFeed(string text) =>
            text.Length == 0 || text.EndsWith('\n') ? text : text + "\n";
    }
}
