using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>
/// Part S's expectations (Phase6Expectations.S.cs) held against plain git on fresh copies of the
/// scenarios "settings", "conflicts" and "ignored", and against each other: the SHAs, files, the
/// session's global file and <c>.git/config</c> through S18 to S20, the screen's reads (SD29) with
/// their exit codes, the identity reads, the commits part S makes, the pulls of S19 and the
/// checkouts of S21. No app: a scenario that drifted, or an expectation copied wrong, fails here,
/// before a window opens. Every git call runs as the app's session runs it (part S's conventions):
/// the session's own global file in a data folder of the test's (<c>GIT_CONFIG_GLOBAL</c>,
/// <c>XDG_CONFIG_HOME</c>, never the owner's), no identity variables, D72's dates. Git's own texts
/// are asserted word for word on the report's git, 2.36 only; on every git their key lines, exit
/// codes and states.
/// </summary>
public sealed class Phase6ExpectationTestsS
{
    // ---- "settings" ----

    /// <summary>ST0: the commits' SHAs and subjects as the graph's rows name them, the refs, the files, the status, the tracking and the copy's <c>.git/config</c>.</summary>
    [Fact]
    public async Task Settings_is_ST0_as_the_report_gives_it()
    {
        using var repo = await Scenarios.GitSettingsAsync();

        Assert.Equal(
            [
                "refs/heads/main " + PartS.LocalWorkSha,
                "refs/remotes/origin/main " + PartS.RemoteWorkSha,
            ],
            await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));
        var shas = new[] { PartS.LocalWorkSha, PartS.RemoteWorkSha, PartS.InitialCommitSha };
        Assert.Equal(shas.Select(sha => sha[..7]), PartS.St0.Select(row => row.ShortSha));
        foreach (var (row, sha) in PartS.St0.Zip(shas))
        {
            Assert.Equal(row.Message, (await LinesAsync(repo, "log", "-1", "--format=%s", sha)).Single());
        }

        Assert.Equal(PartS.InitialCommitSha, (await LinesAsync(repo, "log", "-1", "--format=%P", PartS.LocalWorkSha)).Single());
        Assert.Equal(PartS.InitialCommitSha, (await LinesAsync(repo, "log", "-1", "--format=%P", PartS.RemoteWorkSha)).Single());
        Assert.Equal(PartS.WorkingTreeFiles, await LinesAsync(repo, "ls-files"));
        Assert.Equal(PartS.ReadmeText, WorkingFile(repo, "README.md"));
        Assert.Equal(PartS.NotesText, WorkingFile(repo, PartS.NotesFile));
        Assert.Equal(PartS.RunPyText, WorkingFile(repo, PartS.RunPyFile));
        Assert.Empty(await LinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Equal(
            ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +1 -1"],
            (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# branch.h", StringComparison.Ordinal) || line.StartsWith("# branch.u", StringComparison.Ordinal) || line.StartsWith("# branch.ab", StringComparison.Ordinal)));
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "FETCH_HEAD")), "A fresh copy has a FETCH_HEAD.");

        var config = ConfigText(repo);
        Assert.EndsWith(PartS.SettingsConfigRemoteAndBranch, config, StringComparison.Ordinal);
        Assert.DoesNotContain("[user]", config, StringComparison.Ordinal);
        Assert.DoesNotContain("[pull]", config, StringComparison.Ordinal);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(PartS.SettingsConfigOnWindows, config);
        }

        Assert.Equal(PartS.ReadmeText, (await repo.GitAsync("show", PartS.InitialCommitSha + ":README.md")).StandardOutput);
        Assert.Equal(PartS.NotesText, (await repo.GitAsync("show", PartS.InitialCommitSha + ":notes.txt")).StandardOutput);
        Assert.Equal(PartS.RunPyText, (await repo.GitAsync("show", PartS.InitialCommitSha + ":run.py")).StandardOutput);
    }

    /// <summary>
    /// The identity reads (part S's "The identity reads", 8.6) as the session's runner makes them:
    /// with the seed <c>user.useConfigOnly = true</c> alone both <c>git var</c> refuse (128) with the
    /// report's block; with S18 part B's seed Ann Example; with the repository's own Bo Local; with
    /// S19's seed Test Author; each at D72's date.
    /// </summary>
    [Fact]
    public async Task The_identity_reads_give_the_reports_answers_with_each_seed()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var session = new Session(PartS.SeedConfigOnly);

        var author = await session.RunAsync(repo.Path, PartS.VarAuthorCommand);
        var committer = await session.RunAsync(repo.Path, PartS.VarCommitterCommand);
        Assert.Equal((128, 128), (author.ExitCode, committer.ExitCode));
        Assert.Equal(PartS.NoEmailGiven, Lines(author.StandardError)[^1]);
        Assert.Equal(PartS.NoEmailGiven, Lines(committer.StandardError)[^1]);
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartS.IdentityUnknown("Author"), author.StandardError.TrimEnd('\n').Split('\n'));
            Assert.Equal(PartS.IdentityUnknown("Committer"), committer.StandardError.TrimEnd('\n').Split('\n'));
        }

        session.Seed(PartS.SeedAnnExample);
        await AssertIdentAsync(session, repo, PartS.AnnIdent);

        await session.RunAsync(repo.Path, PartS.WriteLocalBoName, expectedExit: 0);
        await session.RunAsync(repo.Path, PartS.WriteLocalBoEmail, expectedExit: 0);
        await AssertIdentAsync(session, repo, PartS.BoIdent);

        using var other = await Scenarios.GitSettingsAsync();
        session.Seed(PartS.SeedPullFastForwardOnly);
        await AssertIdentAsync(session, other, PartS.TestAuthorIdent);
    }

    /// <summary>
    /// The global file through S18 and S20 (part S's "The session's global file"): the outside read
    /// lists only its entries as <c>global</c>; the writes made as the app makes them, in the data
    /// folder, give the report's bytes, and the emptied <c>[merge]</c> goes.
    /// </summary>
    [Fact]
    public async Task The_global_writes_give_the_reports_files()
    {
        using var session = new Session(PartS.SeedConfigOnly);

        var read = await session.RunOutsideAsync(PartS.ConfigListCommand);
        Assert.Equal(0, read.ExitCode);
        var entries = read.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["global", "file:" + session.GlobalFile.Replace('\\', '/'), "user.useconfigonly\ntrue"], entries.Select(entry => entry.Replace('\\', '/')));

        await session.RunOutsideAsync(PartS.WriteGlobalAnnName, expectedExit: 0);
        await session.RunOutsideAsync(PartS.WriteGlobalAnnEmail, expectedExit: 0);
        Assert.Equal(PartS.SeedAnnExample, session.GlobalText());

        session.Seed(PartS.SeedSlowTool);
        await session.RunOutsideAsync(PartS.WriteGlobalSlow, expectedExit: 0);
        Assert.Equal(PartS.SlowToolChosen, session.GlobalText());
        await session.RunOutsideAsync(PartS.UnsetGlobalMergeTool, expectedExit: 0);
        Assert.Equal(PartS.SeedSlowTool, session.GlobalText());
    }

    /// <summary>
    /// <c>.git/config</c> through S18 and S19 (part S's table), each write as the app makes it in the
    /// working tree, and Pull's reader at each state (SD29, 8.9: the report's table, the
    /// <c>--type=bool</c> read after every <c>only</c>), with S19's seed.
    /// </summary>
    [Fact]
    public async Task The_repository_writes_and_pulls_reads_follow_the_reports_table()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var session = new Session(PartS.SeedPullFastForwardOnly);
        var start = ConfigText(repo);

        await AssertPullReadsAsync(session, repo, PartS.PullReadsFastForwardOnly);
        var refused = await session.RunAsync(repo.Path, PartS.BoolPullFfCommand);
        Assert.Contains(PartS.BadBooleanOnly, Lines(refused.StandardError));

        // S18 step 7: the repository's identity.
        await session.RunAsync(repo.Path, PartS.WriteLocalBoName, expectedExit: 0);
        await session.RunAsync(repo.Path, PartS.WriteLocalBoEmail, expectedExit: 0);
        Assert.Equal(start + PartS.LocalIdentitySection, ConfigText(repo));

        // S19 step 4 (on another copy): both pull keys at --local.
        using var pull = await Scenarios.GitSettingsAsync();
        start = ConfigText(pull);
        await session.RunAsync(pull.Path, PartS.WriteLocalPullRebase, expectedExit: 0);
        await session.RunAsync(pull.Path, PartS.WriteLocalPullFf, expectedExit: 0);
        Assert.Equal(start + PartS.LocalPullSection, ConfigText(pull));
        await AssertPullReadsAsync(session, pull, PartS.PullReadsRebase);
        var mine = await session.RunAsync(pull.Path, PartS.ConfigListCommand, expectedExit: 0);
        var local = mine.StandardOutput.Split('\0').ToList();
        Assert.Contains("pull.rebase\ntrue", local);
        Assert.Contains("pull.ff\ntrue", local);

        // S19 parts B and C's set-up, as a terminal makes it: the same bytes.
        using var terminal = await Scenarios.GitSettingsAsync();
        start = ConfigText(terminal);
        await terminal.ConfigAsync("pull.rebase", "true");
        await terminal.ConfigAsync("pull.ff", "true");
        Assert.Equal(start + PartS.LocalPullSection, ConfigText(terminal));

        // S19 step 7: Same as global unsets both; git removes the emptied [pull].
        await session.RunAsync(terminal.Path, PartS.UnsetLocalPullRebase, expectedExit: 0);
        await session.RunAsync(terminal.Path, PartS.UnsetLocalPullFf, expectedExit: 0);
        Assert.Equal(start, ConfigText(terminal));
        await AssertPullReadsAsync(session, terminal, PartS.PullReadsFastForwardOnly);

        // S19 step 8: branch.main.rebase = false, outside the app; the branch's key answers.
        await terminal.ConfigAsync("branch.main.rebase", "false");
        Assert.Equal(start.Replace(PartS.BranchMainSection, PartS.BranchMainSection + PartS.BranchRebaseLine, StringComparison.Ordinal), ConfigText(terminal));
        await AssertPullReadsAsync(session, terminal, PartS.PullReadsBranchRebase);

        // S19 step 9: its --unset-all.
        await session.RunAsync(terminal.Path, PartS.UnsetLocalBranchRebase, expectedExit: 0);
        Assert.Equal(start, ConfigText(terminal));

        // S19 step 10: pull.rebase = preserve, outside the app; refused by --type=bool (128).
        await terminal.ConfigAsync("pull.rebase", "preserve");
        Assert.Equal(start + PartS.LocalPreserveSection, ConfigText(terminal));
        await AssertPullReadsAsync(session, terminal, PartS.PullReadsPreserve);
        var preserve = await session.RunAsync(terminal.Path, PartS.BoolPullRebaseCommand);
        Assert.Contains(PartS.BadBooleanPreserve, Lines(preserve.StandardError));
    }

    /// <summary>
    /// S18's commits (SD5): <c>notes.txt</c> with three lines, staged, committed with
    /// <c>Add a third note</c> as the app commits, as Ann Example (the global seed) and on a fresh
    /// copy as Bo Local (the repository's own): the report's SHAs, one tree, the parent Local work.
    /// </summary>
    [Fact]
    public async Task The_third_note_commits_have_the_reports_shas()
    {
        using var session = new Session(PartS.SeedAnnExample);
        using var global = await Scenarios.GitSettingsAsync();
        Assert.Equal(PartS.GlobalCommitSha, await CommitThirdNoteAsync(session, global));
        Assert.Equal($"{PartS.AnnIdent} {PartS.AnnIdent}", (await LinesAsync(global, "log", "-1", "--format=%an <%ae> %cn <%ce>")).Single());

        using var local = await Scenarios.GitSettingsAsync();
        await session.RunAsync(local.Path, PartS.WriteLocalBoName, expectedExit: 0);
        await session.RunAsync(local.Path, PartS.WriteLocalBoEmail, expectedExit: 0);
        Assert.Equal(PartS.RepositoryCommitSha, await CommitThirdNoteAsync(session, local));
        Assert.Equal($"{PartS.BoIdent} {PartS.BoIdent}", (await LinesAsync(local, "log", "-1", "--format=%an <%ae> %cn <%ce>")).Single());

        foreach (var (repo, sha) in new[] { (global, PartS.GlobalCommitSha), (local, PartS.RepositoryCommitSha) })
        {
            Assert.StartsWith(PartS.ThirdNoteTree, (await LinesAsync(repo, "log", "-1", "--format=%T", sha)).Single(), StringComparison.Ordinal);
            Assert.Equal(PartS.LocalWorkSha, (await LinesAsync(repo, "log", "-1", "--format=%P", sha)).Single());
            Assert.Equal(PartS.ThirdNote, (await LinesAsync(repo, "log", "-1", "--format=%B", sha)).Single());
            Assert.All(await LinesAsync(repo, "log", "-1", "--format=%aI%n%cI", sha), text => Assert.Equal(GitIsolation.AppCommitDate, GitIsoDates.Parse(text)));
            Assert.Equal(["# branch.ab +2 -1"], (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# branch.ab", StringComparison.Ordinal)));
        }

        Assert.Equal(PartS.St1[0].ShortSha, PartS.GlobalCommitSha[..7]);
        Assert.Equal(PartS.St1r[0].ShortSha, PartS.RepositoryCommitSha[..7]);
    }

    /// <summary>
    /// S19's pulls on plain git with the seed: the fetch (nothing printed on 2.36), the added-files
    /// read, the fast-forward refused under <c>pull.ff = only</c> (128, the key line); with the
    /// repository's keys, the ancestry read (1: diverged) and <c>git rebase</c> giving the report's
    /// <c>b4253bb</c>, its reflog and its two lines.
    /// </summary>
    [Fact]
    public async Task The_pulls_of_S19_refuse_the_fast_forward_and_rebase_to_b4253bb()
    {
        using var session = new Session(PartS.SeedPullFastForwardOnly);
        using var repo = await Scenarios.GitSettingsAsync();
        var exact = await repo.ExactStateAsync();

        var fetch = await session.RunAsync(repo.Path, PartS.FetchOriginCommand, expectedExit: 0);
        if (await IsReportGitAsync())
        {
            Assert.Equal((string.Empty, string.Empty), (fetch.StandardOutput, fetch.StandardError));
        }

        Assert.Equal(PartS.FetchHeadLine, File.ReadAllText(Path.Combine(repo.Path, ".git", "FETCH_HEAD")).TrimEnd('\n'));
        var added = await session.RunAsync(repo.Path, PartS.AddedFilesCommand, expectedExit: 0);
        Assert.Equal(PartS.AddedFilesOutput, added.StandardOutput);
        Assert.False(File.Exists(Path.Combine(repo.Path, "remote.txt")));
        var refused = await session.RunAsync(repo.Path, PartS.FastForwardOnlyCommand, expectedExit: 128);
        Assert.Contains(PartS.NotPossibleToFastForward, Lines(refused.StandardError));
        if (await IsReportGitAsync())
        {
            Assert.Equal([PartS.NotPossibleToFastForward], GitOutputCleaner.Clean(refused.StandardError, refused.StandardOutput).Split('\n'));
        }

        Assert.Equal(exact, await repo.ExactStateAsync());

        using var rebase = await Scenarios.GitSettingsAsync();
        await rebase.ConfigAsync("pull.rebase", "true");
        await rebase.ConfigAsync("pull.ff", "true");
        await session.RunAsync(rebase.Path, PartS.FetchOriginCommand, expectedExit: 0);
        await session.RunAsync(rebase.Path, PartS.IsAncestorCommand, expectedExit: 1);
        var rebased = await session.RunAsync(rebase.Path, PartS.RebaseCommand, expectedExit: 0);
        var lines = Lines(rebased.StandardError.Replace("\u001b[K", string.Empty, StringComparison.Ordinal));
        Assert.Equal(PartS.RebaseLines, lines);
        Assert.Equal(PartS.RebasedSha, await rebase.HeadAsync());
        Assert.Equal(PartS.RemoteWorkSha, (await LinesAsync(rebase, "log", "-1", "--format=%P", "HEAD")).Single());
        Assert.Equal([PartS.TestAuthorIdent, PartS.TestAuthorIdent], await LinesAsync(rebase, "log", "-1", "--format=%an <%ae>%n%cn <%ce>", "HEAD"));
        var dates = await LinesAsync(rebase, "log", "-1", "--format=%aI%n%cI", "HEAD");
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 2, 0, TimeSpan.Zero), GitIsoDates.Parse(dates[0]));
        Assert.Equal(GitIsolation.AppCommitDate, GitIsoDates.Parse(dates[1]));
        Assert.Equal(PartS.RebaseReflog, (await LinesAsync(rebase, "log", "-g", "--format=%gs", "HEAD")).Take(3));
        Assert.Equal(["# branch.ab +1 -0"], (await LinesAsync(rebase, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# branch.ab", StringComparison.Ordinal)));
        Assert.Equal(PartS.Str.Select(row => row.ShortSha), (await LinesAsync(rebase, "log", "--format=%h", "--abbrev=7", "HEAD")));
    }

    // ---- "conflicts" and "ignored" ----

    /// <summary>S20's "conflicts" stopped in its merge (phase 4's GC-M): <c>UU notes.txt</c>, its <c>MERGE_HEAD</c>, and <c>git config --get merge.tool</c> with S20's seed (exit 1), after step 3 (<c>slow</c>) and after step 4 (exit 1).</summary>
    [Fact]
    public async Task Conflicts_merge_tool_reads_follow_S20s_global_file()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var session = new Session(PartS.SeedSlowTool);

        Assert.Equal(["UU notes.txt"], await LinesAsync(repo, "status", "--porcelain=v1"));
        Assert.Equal(PartS.TheirsEditsSha, File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_HEAD")).Trim());
        await session.RunAsync(repo.Path, PartS.GetMergeToolCommand, expectedExit: 1);
        session.Seed(PartS.SlowToolChosen);
        var slow = await session.RunAsync(repo.Path, PartS.GetMergeToolCommand, expectedExit: 0);
        Assert.Equal(PartS.SlowTool + "\n", slow.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        session.Seed(PartS.SeedSlowTool);
        await session.RunAsync(repo.Path, PartS.GetMergeToolCommand, expectedExit: 1);
        var list = await session.RunAsync(repo.Path, PartS.ConfigListCommand, expectedExit: 0);
        Assert.Contains("mergetool.slow.cmd\ntrue", list.StandardOutput.Split('\0'));
    }

    /// <summary>
    /// S21's checkouts on IG0: with <c>--no-overwrite-ignore</c> <c>vendor</c> and the detached
    /// Track the secret are refused (1, the report's four lines), nothing changed, and
    /// <c>check-ignore</c> names <c>secret.env</c>; without the option <c>vendor</c> is checked out
    /// over the ignored file, as git does.
    /// </summary>
    [Fact]
    public async Task Ignored_refuses_S21s_checkouts_with_the_option_and_takes_vendor_without()
    {
        using var repo = await Scenarios.IgnoredAsync();
        using var session = new Session(null);
        var exact = await repo.ExactStateAsync();
        Assert.Equal(PartS.Ig0Ignored, await LinesAsync(repo, "status", "--porcelain=v1", "--ignored"));
        Assert.Equal(PartS.MainWorkSha, await repo.HeadAsync());
        Assert.Equal("Track the secret", (await LinesAsync(repo, "log", "-1", "--format=%s", PartS.TrackSecretSha)).Single());
        Assert.Equal(0, (await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", PartS.TrackSecretSha, "main")).ExitCode);

        foreach (var command in new[] { PartS.SwitchVendorKeptCommand, PartS.DetachTrackSecretCommand })
        {
            var refused = await session.RunAsync(repo.Path, command, expectedExit: 1);
            Assert.Contains(PartS.UntrackedOverwritten[0], Lines(refused.StandardError));
            Assert.Equal(PartS.UntrackedOverwritten, GitOutputCleaner.Clean(refused.StandardError, refused.StandardOutput).Split('\n'));
            Assert.Equal(exact, await repo.ExactStateAsync());
        }

        var ignored = await session.RunAsync(repo.Path, PartS.CheckIgnoreCommand, expectedExit: 0, input: PartS.SecretIgnored, literalPathspecs: false);
        Assert.Equal(PartS.SecretIgnored, ignored.StandardOutput);

        var taken = await session.RunAsync(repo.Path, PartS.SwitchVendorCommand, expectedExit: 0);
        Assert.Equal(PartS.UpToDateWithVendor, taken.StandardOutput.Trim());
        Assert.Equal(PartS.SwitchedToVendor, taken.StandardError.Trim());
        Assert.Equal(PartS.VendorSecretSha, await repo.HeadAsync());
        Assert.Equal("vendor", await repo.CurrentBranchAsync());
        Assert.Equal(PartS.SecretVendor, WorkingFile(repo, "secret.env"));
        Assert.Equal(PartS.BuildOutput, WorkingFile(repo, "build/out.bin"));
        Assert.Equal(PartS.Ig0vIgnored, await LinesAsync(repo, "status", "--porcelain=v1", "--ignored"));
    }

    // ---- The expectations against each other ----

    /// <summary>The Keyboard shortcuts list: eleven groups, 50 rows, one row on two lines, each key text of the Windows form (no <c>Cmd</c>).</summary>
    [Fact]
    public void The_shortcut_list_has_eleven_groups_and_fifty_rows()
    {
        Assert.Equal(11, PartS.ShortcutGroups.Count);
        Assert.Equal(50, PartS.ShortcutGroups.Sum(group => group.Rows.Count));
        Assert.Equal(PartS.LastGroup, PartS.ShortcutGroups[^1].Group);
        Assert.Contains(PartS.ShortcutGroups, group => group.Group == PartS.NotedGroup);
        Assert.Single(PartS.ShortcutGroups.SelectMany(group => group.Rows), row => row.Action == PartS.ExtendSelection);
        Assert.DoesNotContain(PartS.ShortcutGroups.SelectMany(group => group.Rows), row => row.Keys.Contains("Cmd", StringComparison.Ordinal));
        Assert.Equal(50, PartS.ShortcutGroups.SelectMany(group => group.Rows).Select(row => row.Action).Distinct().Count());
    }

    /// <summary>The numbers the checks rest on: the menus' heights (171 and 197), the click points, the card at both sizes, the pull reads' counts at an opening (seven, eight, nine processes).</summary>
    [Fact]
    public void The_numbers_agree_with_one_another()
    {
        Assert.Equal(171, MenuHeight(5, 1));
        Assert.Equal(197, MenuHeight(6, 1));
        Assert.Equal(new Avalonia.Point(468, 207), PartS.StRowPoint(3));
        Assert.Equal(new Avalonia.Point(468, 129), PartS.StRowPoint(0));
        Assert.Equal(PartS.StRowPointX, WindowLayout.MessageColumnX(PartS.StLanes) + WindowLayout.MessageClickInset);
        Assert.Equal(PartS.IgRowPointX, WindowLayout.MessageColumnX(3) + WindowLayout.MessageClickInset);
        Assert.Equal(PartS.TrackSecretPoint.Y, WindowLayout.RowCentreY(PartS.TrackSecretRow));
        Assert.Equal(1100 - (2 * PartS.CardAt1100.X), PartS.CardAt1100.Width);
        Assert.Equal(1920 - (2 * PartS.CardAt1920.X), PartS.CardAt1920.Width);
        Assert.True(PartS.RestAt1920.Y > PartS.CardAt1920.Bottom && PartS.RestAt1920.Y < 1080 - WindowLayout.StatusBarHeight, "The rest point at 1920×1080 lies on the backdrop below the card (SD2).");
        Assert.True(PartS.CardAt1920.Contains(PartS.RestAt1100), "The neutral point lies inside the card at 1920×1080 (SD2's reason).");
        Assert.Equal(7, 4 + PartS.PullReadsNothingSet.Count);
        Assert.Equal(8, 4 + PartS.PullReadsFastForwardOnly.Count);
        Assert.Equal(9, 4 + PartS.PullReadsPreserve.Count);
        Assert.Equal(PartS.CheckOutTrackSecret, "Check out " + PartS.TrackSecretSha[..7]);
        Assert.Equal(PartS.RebaseBackupInput, $"create {PartS.RebaseBackupRef} {PartS.LocalWorkSha}");
        Assert.Equal("rebase-20260102T090001000Z", BackupRef.Parse(PartS.RebaseBackupRef).Stem);
        Assert.Equal(PartS.IsAncestorCommand, $"git merge-base --is-ancestor {PartS.LocalWorkSha} FETCH_HEAD");
        Assert.Equal(8, PartS.MergeToolOptions.Count);
        Assert.DoesNotContain(PartS.MergeToolOptions, option => option.Text == "vimdiff");
        Assert.Equal(5, PartS.EditorOptions.Count);
        Assert.Equal(VisualCommit.Core.ToolCatalogues.FixedEditors.Select(editor => (editor.Name, editor.Program)), PartS.EditorOptions.Skip(1).Take(3));
        Assert.Equal(9, PartS.SettingsDefaults.Count);
    }

    // ---- Helpers ----

    /// <summary>
    /// A data folder with the session's own git environment (part S's conventions, 17.2): the global
    /// file <c>&lt;data&gt;/harness/global.gitconfig</c> seeded as given, <c>XDG_CONFIG_HOME</c> beside
    /// it, no identity variables, D72's dates; calls run as the app's runner runs them.
    /// </summary>
    private sealed class Session : IDisposable
    {
        private readonly TempDirectory _data = new("data");
        private readonly GitRunner _runner = new(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("No git."));

        public Session(string? seed)
        {
            Environment = new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(_data.Combine("home")));
            foreach (var (name, value) in GitIsolation.SessionGitEnvironment(_data.Path))
            {
                Environment[name] = value;
            }

            foreach (var variable in GitIsolation.IdentityVariables)
            {
                Environment[variable] = null;
            }

            foreach (var (name, value) in GitIsolation.AppCommitDates)
            {
                Environment[name] = value;
            }

            if (seed is not null)
            {
                Seed(seed);
            }
        }

        public Dictionary<string, string?> Environment { get; }

        public string GlobalFile => GitIsolation.SessionGlobalConfigFile(_data.Path);

        public void Seed(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GlobalFile)!);
            File.WriteAllText(GlobalFile, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public string GlobalText() => File.ReadAllText(GlobalFile);

        /// <summary>Runs <paramref name="commandText"/> (as the call log renders it) in <paramref name="folder"/>; with <paramref name="expectedExit"/>, asserts its exit code.</summary>
        public async Task<GitResult> RunAsync(string folder, string commandText, int? expectedExit = null, string? input = null, bool literalPathspecs = true, IReadOnlyDictionary<string, string?>? extra = null)
        {
            Assert.StartsWith("git ", commandText, StringComparison.Ordinal);
            var environment = new Dictionary<string, string?>(Environment)
            {
                ["GIT_LITERAL_PATHSPECS"] = literalPathspecs ? "1" : null,
            };
            foreach (var (name, value) in extra ?? new Dictionary<string, string?>())
            {
                environment[name] = value;
            }

            var command = new GitCommand(Arguments(commandText["git ".Length..])) { WorkingDirectory = folder, Environment = environment, StandardInput = input };
            var result = await _runner.RunAsync(command);
            if (expectedExit is { } exit)
            {
                Assert.True(result.ExitCode == exit, $"\"{commandText}\" exited {result.ExitCode}, not {exit}.\n{result.StandardError}");
            }

            return result;
        }

        /// <summary>A call of the outside read's kind: in the data folder, git's ceiling its parent (8.1).</summary>
        public Task<GitResult> RunOutsideAsync(string commandText, int? expectedExit = null) =>
            RunAsync(_data.Path, commandText, expectedExit, extra: new Dictionary<string, string?> { [GitIsolation.CeilingVariable] = Path.GetDirectoryName(_data.Path) });

        public void Dispose() => _data.Dispose();
    }

    /// <summary>A command line as the call log renders it, split back into its arguments: at spaces, a part in double quotes one argument.</summary>
    private static string[] Arguments(string text)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ' ' && !quoted)
            {
                if (current.Length > 0)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            arguments.Add(current.ToString());
        }

        return [.. arguments];
    }

    /// <summary>Both <c>git var</c> of the session print <paramref name="ident"/> at D72's date.</summary>
    private static async Task AssertIdentAsync(Session session, TempRepo repo, string ident)
    {
        foreach (var command in new[] { PartS.VarAuthorCommand, PartS.VarCommitterCommand })
        {
            var result = await session.RunAsync(repo.Path, command, expectedExit: 0);
            Assert.Equal($"{ident} {PartS.IdentDate}", result.StandardOutput.Trim());
        }
    }

    /// <summary>Pull's reader's calls in its order with the report's exit codes and outputs; the reads after the deciding one are not made (8.9).</summary>
    private static async Task AssertPullReadsAsync(Session session, TempRepo repo, IReadOnlyList<PartS.PullRead> reads)
    {
        foreach (var read in reads)
        {
            var result = await session.RunAsync(repo.Path, read.Command, expectedExit: read.ExitCode, extra: new Dictionary<string, string?> { ["GIT_OPTIONAL_LOCKS"] = "0" });
            Assert.Equal(read.Output, result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        }
    }

    /// <summary>S18's commit made as the app makes it: <c>notes.txt</c> with three lines, staged, <c>git commit --file=- --cleanup=whitespace</c> with the summary on standard input; returns the new HEAD.</summary>
    private static async Task<string> CommitThirdNoteAsync(Session session, TempRepo repo)
    {
        File.WriteAllText(Path.Combine(repo.Path, PartS.NotesFile), PartS.NotesThreeLines);
        await session.RunAsync(repo.Path, "git add -- notes.txt", expectedExit: 0);
        await session.RunAsync(repo.Path, PartS.CommitCommand, expectedExit: 0, input: PartS.ThirdNote);
        return await repo.HeadAsync();
    }

    /// <summary>The repository's <c>.git/config</c>, its line ends as line feeds.</summary>
    private static string ConfigText(TempRepo repo) => File.ReadAllText(Path.Combine(repo.Path, ".git", "config")).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string WorkingFile(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Plain git's output as lines, line ends as line feeds (none for an empty output).</summary>
    private static async Task<string[]> LinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>The lines of a stream, split at every carriage return and line feed, without trailing spaces and empty lines.</summary>
    private static List<string> Lines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];
}
