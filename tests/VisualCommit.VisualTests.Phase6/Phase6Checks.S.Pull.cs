// Check S19 (docs/test-reports/phase-6.md, part S): the pull modes and their precedence (D190, D78,
// D84, design 7.4, 8.7 to 8.9, D218, D219): a global pull.ff = only shown as Fast-forward only and
// refusing the pull of diverged branches with 4.8's text; Rebase saved in "This repository" as both
// keys at --local, which beats the global "only" key by key, noticed in the global scope (SD8) and
// in effect in the repository's (SD18); with those keys the pull rebasing main; Same as global
// unsetting both; a branch.main.rebase written outside the app, noticed and removed; pull.rebase =
// preserve shown as Other and refusing the pull before any fetch. Three methods (SD1, SD15).
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>Check S19, part A (steps 1 to 5): Fast-forward only from the global file, the refused pull, Rebase saved for the repository.</summary>
    [AvaloniaFact]
    public async Task Check_S19_pull_mode_ff_only_and_repository_rebase()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 1" };

        // Step 1: S19's seed (SD3); the global scope shows Fast-forward only, read by Pull's reader in its order.
        using var app = await StartAsync(data, repo, PartS.SeedPullFastForwardOnly);
        var configAtStart = ConfigText(repo);
        var folder = await WorkingTreeAsync(repo);
        AssertGraph(app, PartS.St0, PartS.StLanes, working: null);
        var pull = ToolbarButton(app, "PullButton");
        Assert.True(pull.IsEffectivelyEnabled, "Pull is disabled.");
        Assert.Equal(PartS.PullToolTip, ToolTip.GetTip(pull) as string);
        var start = await QuietMarkAsync(app);
        await app.OpenSettingsAsync();
        app.ChooseSection(SettingsSection.Git);
        ShotAtRest(app, "s19a-global-ff-only");
        AssertGitGlobal(app, layout, data, repo, name: "Test Author", email: "author@example.com", checkedPull: "PullFastForwardOnly", repositoryFolder: folder);
        await AssertScreenReadsAsync(app, start, data, repo, varExit: 0, ident: PartS.TestAuthorIdent, pull: PartS.PullReadsFastForwardOnly);
        await AssertBadBooleanAsync(app, start, PartS.BoolPullFfCommand, PartS.BadBooleanOnly);

        // Step 2: Esc; Pull refused by pull.ff = only after its fetch (4.8, D211).
        layout.Step = "step 2";
        await EscapeScreenAsync(app);
        var mark = app.LogMark();
        var sequence = await QuietMarkAsync(app);
        app.Click(pull);
        await WaitForOutcomeAsync(app, repo, PartS.PullTitle, mark, OperationOutcome.Failed, NetworkTimeout);
        Shot(app, "s19b-pull-refused");
        AssertOperationCalls(
            app,
            sequence,
            [
                (PartS.GetPullFfCommand, 0),
                (PartS.BoolPullFfCommand, 128),
                (PartS.GetBranchRebaseCommand, 1),
                (PartS.GetPullRebaseCommand, 1),
                (PartS.FetchOriginCommand, 0),
                (PartS.AddedFilesCommand, 0),
                (PartS.FastForwardOnlyCommand, 128),
            ],
            PartS.IsAncestorCommand,
            PartS.RebaseCommand,
            PartS.CheckIgnoreCommand);
        Assert.Equal(PartS.AddedFilesOutput, app.CallsSince(sequence).Single(call => call.CommandText == PartS.AddedFilesCommand).StandardOutput);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartS.PullFailed,
            PartS.PullNotFastForward,
            PartS.Settings,
            PartS.FastForwardOnlyCommand,
            [PartS.CopyOutput, PartS.Close],
            [PartS.NotPossibleToFastForward],
            PartS.NotPossibleToFastForward));
        AssertStatus(app, PartS.Failed(PartS.PullTitle), OperationStatusTone.Danger);
        Assert.Equal(PartS.LocalWorkSha, await repo.HeadAsync());
        Assert.Equal(PartS.RemoteWorkSha, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal("+1 -1", await AheadBehindAsync(repo));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain=v1"));
        Assert.Equal(PartS.FetchHeadLine, File.ReadAllText(Path.Combine(repo.Path, ".git", "FETCH_HEAD")).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'));
        Assert.Empty(await repo.BackupRefsAsync());
        AssertUndo(app, PartS.NothingToUndo);
        await CloseDialogAsync(app);

        // Step 3: This repository: Same as global, in effect Fast-forward only from the global file.
        layout.Step = "step 3";
        sequence = await QuietMarkAsync(app);
        var screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseScope(SettingsScope.Repository);
        ShotAtRest(app, "s19c-repository-same-as-global");
        AssertGitRepository(app, layout, folder, string.Empty, PartS.SameAsGlobalValue("Test Author"), string.Empty, PartS.SameAsGlobalValue("author@example.com"), PartS.CommitsMadeAs(PartS.TestAuthorIdent), [], "PullNotSet", PartS.PullInEffect(PartS.Main, PartS.PullFastForwardOnly, "pull.ff", "only", PartS.GlobalFileName));
        AssertPullInEffectTip(app, ShellDriver.GlobalGitConfigPath(data.Path));
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: 0, ident: PartS.TestAuthorIdent, pull: PartS.PullReadsFastForwardOnly);

        // Step 4: Rebase saved for the repository: both keys at --local (8.7), one operation.
        layout.Step = "step 4";
        ClickField(app, "PullRebase");
        AssertChecked(app.SettingsSnapshot(), "PullRebase", true);
        var saved = await SaveRepositorySettingsAsync(app, data, repo, screen, (PartS.WriteLocalPullRebase, "repo"), (PartS.WriteLocalPullFf, "repo"));
        await AssertReadBackAsync(app, data, repo, saved, PartS.PullReadsRebase);
        Assert.Equal(configAtStart + PartS.LocalPullSection, ConfigText(repo));
        Assert.Equal(PartS.SeedPullFastForwardOnly, GlobalText(data));

        // Step 5: the global scope keeps Fast-forward only with SD8's notice; the repository's Rebase in effect.
        layout.Step = "step 5";
        sequence = await QuietMarkAsync(app);
        await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        ShotAtRest(app, "s19d-global-repository-sets-its-own");
        AssertGitGlobal(app, layout, data, repo, name: "Test Author", email: "author@example.com", checkedPull: "PullFastForwardOnly", pullNotices: [PartS.SetsItsOwn(PartS.Settings, PartS.PullRebase)], repositoryFolder: folder);
        app.ChooseScope(SettingsScope.Repository);
        ShotAtRest(app, "s19e-repository-rebase");
        AssertGitRepository(app, layout, folder, string.Empty, PartS.SameAsGlobalValue("Test Author"), string.Empty, PartS.SameAsGlobalValue("author@example.com"), PartS.CommitsMadeAs(PartS.TestAuthorIdent), [], "PullRebase", PartS.PullInEffect(PartS.Main, PartS.PullRebase, "pull.rebase", "true", PartS.LocalFileName));
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: 0, ident: PartS.TestAuthorIdent, pull: PartS.PullReadsRebase);
        await EscapeScreenAsync(app);
        Assert.Equal(configAtStart + PartS.LocalPullSection, ConfigText(repo));
        layout.AssertNone();
    }

    /// <summary>Check S19, part B (step 6): with the repository's pull.rebase and pull.ff true (SD15), Pull rebases main onto origin/main.</summary>
    [AvaloniaFact]
    public async Task Check_S19_pull_rebases_by_the_repository_setting()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 6" };

        // Step 6: the seed and the two keys before the start (the .git/config part A's step 4 writes).
        var configAtCopy = ConfigText(repo);
        await repo.ConfigAsync("pull.rebase", "true");
        await repo.ConfigAsync("pull.ff", "true");
        Assert.Equal(configAtCopy + PartS.LocalPullSection, ConfigText(repo));
        using var app = await StartAsync(data, repo, PartS.SeedPullFastForwardOnly);
        AssertGraph(app, PartS.St0, PartS.StLanes, working: null);
        AssertUndo(app, PartS.NothingToUndo);
        var mark = app.LogMark();
        var sequence = await QuietMarkAsync(app);
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForOutcomeAsync(app, repo, PartS.PullTitle, mark, OperationOutcome.Succeeded, OperationChainTimeout);
        Shot(app, "s19f-pulled-rebased");
        AssertOperationCalls(
            app,
            sequence,
            [
                (PartS.GetPullFfCommand, 0),
                (PartS.GetBranchRebaseCommand, 1),
                (PartS.GetPullRebaseCommand, 0),
                (PartS.FetchOriginCommand, 0),
                (PartS.IsAncestorCommand, 1),
                (PartS.UpdateRefCommand, 0),
                (PartS.RebaseCommand, 0),
            ],
            PartS.BoolPullFfCommand);
        var calls = app.CallsSince(sequence);
        Assert.Equal("true\n", calls.Single(call => call.CommandText == PartS.GetPullFfCommand).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal("true\n", calls.Single(call => call.CommandText == PartS.GetPullRebaseCommand).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(PartS.RebaseBackupInput, calls.Single(call => call.CommandText == PartS.UpdateRefCommand).StandardInput?.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'));
        Assert.DoesNotContain(calls, call => call.CommandText.StartsWith(PartS.MergePrefix, StringComparison.Ordinal));
        var rebase = calls.Single(call => call.CommandText == PartS.RebaseCommand);
        var rebaseLines = rebase.StandardError.Replace("\u001b[K", string.Empty, StringComparison.Ordinal).Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0).ToList();
        Assert.Contains(PartS.RebaseLines[^1], rebaseLines);
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartS.RebaseLines, rebaseLines);
        }

        AssertStatus(app, PartS.PulledAndRebased);
        await AssertNoDialogAsync(app);
        Assert.Equal(PartS.RebasedSha, await repo.HeadAsync());
        Assert.Equal([PartS.RemoteWorkSha], await GitLinesAsync(repo, "log", "-1", "--format=%P", "HEAD"));
        Assert.Equal([PartS.TestAuthorIdent, PartS.TestAuthorIdent], await GitLinesAsync(repo, "log", "-1", "--format=%an <%ae>%n%cn <%ce>", "HEAD"));
        var dates = await GitLinesAsync(repo, "log", "-1", "--format=%aI%n%cI", "HEAD");
        AssertGitDate(ScenarioTime(2), dates[0]);
        AssertGitDate(AppCommitTime, dates[1]);
        Assert.Equal("+1 -0", await AheadBehindAsync(repo));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain=v1"));
        Assert.Equal([new RefEntry(PartS.RebaseBackupRef, PartS.LocalWorkSha)], await repo.BackupRefsAsync());
        Assert.Equal(PartS.RebaseReflog, (await GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD")).Take(3));
        AssertGraph(app, PartS.Str, PartS.StrLanes, working: null);
        AssertUndo(app, PartS.UndoPull);
        layout.AssertNone();
    }

    /// <summary>Check S19, part C (steps 7 to 11): Same as global unsets both keys; branch.main.rebase noticed and removed; pull.rebase = preserve shown as Other and refusing the pull before any fetch.</summary>
    [AvaloniaFact]
    public async Task Check_S19_pull_mode_unset_branch_rebase_and_other()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 7" };

        // Step 7: the seed and the two keys before the start; Same as global saved.
        var configAtCopy = ConfigText(repo);
        await repo.ConfigAsync("pull.rebase", "true");
        await repo.ConfigAsync("pull.ff", "true");
        using var app = await StartAsync(data, repo, PartS.SeedPullFastForwardOnly);
        var folder = await WorkingTreeAsync(repo);
        AssertGraph(app, PartS.St0, PartS.StLanes, working: null);
        var sequence = await QuietMarkAsync(app);
        var screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        AssertGitGlobal(app, layout, data, repo, name: "Test Author", email: "author@example.com", checkedPull: "PullFastForwardOnly", pullNotices: [PartS.SetsItsOwn(PartS.Settings, PartS.PullRebase)], repositoryFolder: folder);
        app.ChooseScope(SettingsScope.Repository);
        AssertGitRepository(app, layout, folder, string.Empty, null, string.Empty, null, PartS.CommitsMadeAs(PartS.TestAuthorIdent), [], "PullRebase", PartS.PullInEffect(PartS.Main, PartS.PullRebase, "pull.rebase", "true", PartS.LocalFileName));
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: 0, ident: PartS.TestAuthorIdent, pull: PartS.PullReadsRebase);
        ClickField(app, "PullNotSet");
        AssertChecked(app.SettingsSnapshot(), "PullNotSet", true);
        var saved = await SaveRepositorySettingsAsync(app, data, repo, screen, (PartS.UnsetLocalPullRebase, "repo"), (PartS.UnsetLocalPullFf, "repo"));
        await AssertReadBackAsync(app, data, repo, saved, PartS.PullReadsFastForwardOnly);
        Assert.Equal(configAtCopy, ConfigText(repo));

        // Step 8: branch.main.rebase = false written outside the app; noticed with its button (8.8).
        layout.Step = "step 8";
        await ChangeConfigOutsideAsync(app, repo, "branch.main.rebase", "false");
        Assert.Equal(configAtCopy.Replace(PartS.BranchMainSection, PartS.BranchMainSection + PartS.BranchRebaseLine, StringComparison.Ordinal), ConfigText(repo));
        sequence = await QuietMarkAsync(app);
        await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseScope(SettingsScope.Repository);
        ShotAtRest(app, "s19g-branch-rebase-notice");
        AssertGitRepository(app, layout, folder, string.Empty, null, string.Empty, null, PartS.CommitsMadeAs(PartS.TestAuthorIdent), [], "PullNotSet", PartS.PullInEffect(PartS.Main, PartS.PullFastForwardOnly, "pull.ff", "only", PartS.GlobalFileName));
        AssertBranchRebase(app, PartS.BranchRebaseNotice(PartS.Main, "false"), PartS.UseRepositorySetting);
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: 0, ident: PartS.TestAuthorIdent, pull: PartS.PullReadsBranchRebase);

        // Step 9: the key marked for removal; Save removes it alone.
        layout.Step = "step 9";
        screen = Screen(app);
        ClickField(app, "BranchRebaseButton");
        ShotAtRest(app, "s19h-branch-rebase-marked");
        AssertBranchRebase(app, PartS.BranchRebaseMarked(PartS.Main), PartS.KeepBranchSetting(PartS.Main));
        Assert.Contains(PartS.BranchRebaseLine, ConfigText(repo), StringComparison.Ordinal);
        saved = await SaveRepositorySettingsAsync(app, data, repo, screen, (PartS.UnsetLocalBranchRebase, "repo"));
        await AssertReadBackAsync(app, data, repo, saved, PartS.PullReadsFastForwardOnly);
        Assert.Equal(configAtCopy, ConfigText(repo));

        // Step 10: pull.rebase = preserve written outside the app: Other, refused.
        layout.Step = "step 10";
        await ChangeConfigOutsideAsync(app, repo, "pull.rebase", "preserve");
        Assert.Equal(configAtCopy + PartS.LocalPreserveSection, ConfigText(repo));
        sequence = await QuietMarkAsync(app);
        await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        AssertGitGlobal(app, layout, data, repo, name: "Test Author", email: "author@example.com", checkedPull: "PullFastForwardOnly", pullNotices: [PartS.SetsItsOwn(PartS.Settings, PartS.PullOther("pull.rebase", "preserve"))], repositoryFolder: folder);
        app.ChooseScope(SettingsScope.Repository);
        ShotAtRest(app, "s19i-other-value");
        AssertGitRepository(app, layout, folder, string.Empty, null, string.Empty, null, PartS.CommitsMadeAs(PartS.TestAuthorIdent), [], "PullOther", PartS.PullFails(PartS.Main, PartS.PreserveNotUnderstood));
        AssertPullOther(app, PartS.PullOther("pull.rebase", "preserve"), PartS.PullsFail);
        Assert.False(app.SettingsSnapshot().Shows("BranchRebaseNotice"), "BranchRebaseNotice shows.");
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: 0, ident: PartS.TestAuthorIdent, pull: PartS.PullReadsPreserve);

        // Nine processes of the screen's reads; any other call is a whole refresh of the watcher's.
        Assert.Equal(9, app.CallsSince(sequence).Count(IsScreenRead));
        await EscapeScreenAsync(app);
        AssertNoWrite(app, sequence);

        // Step 11: Pull refused before any fetch (8.9, D219).
        layout.Step = "step 11";
        var mark = app.LogMark();
        sequence = await QuietMarkAsync(app);
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForOutcomeAsync(app, repo, PartS.PullTitle, mark, OperationOutcome.Failed);
        AssertOperationCalls(
            app,
            sequence,
            [
                (PartS.GetPullFfCommand, 0),
                (PartS.BoolPullFfCommand, 128),
                (PartS.GetBranchRebaseCommand, 1),
                (PartS.GetPullRebaseCommand, 0),
                (PartS.BoolPullRebaseCommand, 128),
            ],
            PartS.FetchOriginCommand,
            PartS.RebaseCommand);
        await AssertBadBooleanAsync(app, sequence, PartS.BoolPullFfCommand, PartS.BadBooleanOnly);
        await AssertBadBooleanAsync(app, sequence, PartS.BoolPullRebaseCommand, PartS.BadBooleanPreserve);
        var calls = app.CallsSince(sequence);
        Assert.Equal("only\n", calls.Single(call => call.CommandText == PartS.GetPullFfCommand).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal("preserve\n", calls.Single(call => call.CommandText == PartS.GetPullRebaseCommand).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal) || call.CommandText.StartsWith(PartS.MergePrefix, StringComparison.Ordinal) || call.CommandText.StartsWith(PartS.RebaseCommand, StringComparison.Ordinal));
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartS.PullFailed,
            PartS.PreserveNotUnderstood,
            PartS.Settings,
            PartS.GetPullRebaseCommand,
            [PartS.CopyOutput, PartS.Close],
            [PartS.PreserveNotUnderstood],
            OutputFromGit: false));
        AssertStatus(app, PartS.Failed(PartS.PullTitle), OperationStatusTone.Danger);
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "FETCH_HEAD")), "Something was fetched.");
        Assert.Equal(PartS.RemoteWorkSha, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(PartS.LocalWorkSha, await repo.HeadAsync());
        await CloseDialogAsync(app);
        layout.AssertNone();
    }

    /// <summary>The <c>--type=bool</c> read <paramref name="command"/> since <paramref name="sequence"/> refused (exit 128), with git's <paramref name="line"/> word for word on the report's git.</summary>
    private static async Task AssertBadBooleanAsync(ShellDriver app, long sequence, string command, string line)
    {
        var call = app.CallsSince(sequence).Last(record => record.CommandText == command);
        Assert.Equal(128, call.ExitCode);
        if (await IsReportGitAsync())
        {
            Assert.Contains(line, call.StandardError.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));
        }
    }

    /// <summary><c>PullInEffect</c>'s tooltip and help text: the full path of the file it names (8.2).</summary>
    private static void AssertPullInEffectTip(ShellDriver app, string file)
    {
        var field = Field(app.SettingsSnapshot(), "PullInEffect");
        Assert.True(field.ToolTip is { } tip && VisualCommit.App.ViewModels.Settings.GitFileNames.SamePath(tip, file), $"PullInEffect's tooltip is \"{field.ToolTip}\", not {file}.");
        Assert.Equal(field.ToolTip, field.HelpText);
    }

    /// <summary><c>BranchRebaseNotice</c> and <c>BranchRebaseButton</c> (8.8) as given.</summary>
    private static void AssertBranchRebase(ShellDriver app, string notice, string button)
    {
        var snapshot = app.SettingsSnapshot();
        AssertText(snapshot, "BranchRebaseNotice", notice);
        AssertText(snapshot, "BranchRebaseButton", button);
        Assert.True(Field(snapshot, "BranchRebaseButton").IsEnabled, "BranchRebaseButton is disabled.");
    }

    /// <summary><c>PullOther</c> shown and checked (8.7): its label and its second line; the six other radios unchecked.</summary>
    private static void AssertPullOther(ShellDriver app, string label, string note)
    {
        var snapshot = app.SettingsSnapshot();
        AssertText(snapshot, "PullOther", label);
        AssertChecked(snapshot, "PullOther", true);
        var other = Named<RadioButton>(app, "PullOther");
        var lines = other.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).ToList();
        Assert.Equal([label, note], lines);
        foreach (var id in new[] { "PullNotSet", "PullMerge", "PullMergeCommit", "PullRebase", "PullRebaseMerges", "PullFastForwardOnly" })
        {
            AssertChecked(snapshot, id, false);
        }
    }

    /// <summary>
    /// Save with "This repository"'s keys (8.3, 7.8 step 5): one operation <c>Save repository
    /// settings</c> with exactly <paramref name="writes"/> in this order, its log line and result in
    /// the status bar, the screen closed after its read back; no global write.
    /// </summary>
    private static async Task<long> SaveRepositorySettingsAsync(ShellDriver app, TempDirectory data, TempRepo repo, SettingsDialogViewModel screen, params (string Command, string Where)[] writes)
    {
        var mark = app.LogMark();
        var sequence = await QuietMarkAsync(app);
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForOutcomeAsync(app, repo, PartS.SaveRepositorySettings, mark, OperationOutcome.Succeeded);
        await WaitForScreenClosedAsync(app, screen);
        Assert.Single(app.LogLinesSince(mark), line => line.Contains($"{PartS.Settings}: {PartS.SaveRepositorySettings}: Succeeded after ", StringComparison.Ordinal) && line.EndsWith(" ms: " + PartS.SavedRepositorySettings, StringComparison.Ordinal));
        await AssertWritesAsync(app, sequence, data, repo, writes);
        Assert.DoesNotContain(app.CallsSince(sequence), call => call.CommandText.Contains(" --global ", StringComparison.Ordinal));
        AssertStatus(app, PartS.SavedRepositorySettings);
        return sequence;
    }

    /// <summary>The read back of a repository save made after <paramref name="sequence"/> (7.8 step 6): the screen's reads after its writes, Pull's reader as given.</summary>
    private static async Task AssertReadBackAsync(ShellDriver app, TempDirectory data, TempRepo repo, long sequence, IReadOnlyList<PartS.PullRead> pull)
    {
        await AssertScreenReadsAsync(app, sequence, data, repo, varExit: 0, ident: PartS.TestAuthorIdent, pull: pull, others: call => IsWrite(call) || call.OperationId is not null, allowWrites: true);
        AssertReadsAfterWrites(app, sequence);
    }

    /// <summary>
    /// A key of <c>.git/config</c> written outside the app (part S's "Outside the app"), then the
    /// watcher's refresh waited for (a status read with nothing staged or unstaged), the queue idle
    /// and the watcher quiet (a second burst of the write's events, which macOS can report apart,
    /// refreshed too).
    /// </summary>
    private static async Task ChangeConfigOutsideAsync(ShellDriver app, TempRepo repo, string key, string value)
    {
        var mark = app.LogMark();
        await repo.ConfigAsync(key, value);
        await WaitForStatusReadAsync(app, 0, 0, mark);
        await app.WaitForIdleAsync();
        await app.WaitForWatchersQuietAsync();
    }
}
