using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using Protect = VisualCommit.Testing.Scenarios.ProtectFiles;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP4: C12, protected changes (docs/phase-5-design.md section 9, D153, D179; the App tests of
/// 9.4 and the report's part P) through <see cref="OperationsHarness"/> against real git on the
/// "protect" scenario: the dialog of each refusal kind on every checkout path, with git's count;
/// the automatic stash's git calls, running texts, result, stash and the rules for when it is
/// not taken; HEAD moved while the checkout waited; the ignored-files text; a stash git could not
/// finish; the refusals of merge, cherry-pick, revert and pull in C12's words; and a stash's apply
/// or pop checked before git. What the checks picture is part P's (WP8).
/// </summary>
[Collection(RepositoryOperationsTestSupport.BranchesCollection)]
public class RepositoryOperationsProtectTests
{
    private const string OneConflict = "1 conflict prevents checkout. Commit, stash, or discard your changes first.";

    private const string DescribeTheApp = "f39ea2d9addec45f926151cd2267cb57ea0b16aa";

    private const string OtherEdits = "4f5e8215c9321acae3a2ea386bda05107915f2da";

    private const string AddTheGuide = "73e0babca7d61aaa9088f167ddbf0951f5f13744";

    private const string MainWork = "41f8398f95fa73a5614e32760cc511673aa6a80d";

    // ---- 9.3: the refusal message on every checkout path ---------------------------------------------

    [Fact]
    public async Task A_branch_checkout_blocked_by_tracked_changes_counts_them_and_offers_the_stash()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError().CloseError();
        var calls = app.Calls.Snapshot().Count;

        await app.Operations.CheckoutAsync("refs/heads/other");

        // P24 step 2 (TP1).
        var dialog = app.Dialogs.Errors[0];
        Assert.Equal(("Checkout blocked", OneConflict, "Stash changes and check out"), (dialog.Title, dialog.Summary, dialog.ExtraAction));
        AssertOutputOfFailedCall(app, dialog, "git switch --no-guess --no-overwrite-ignore other");
        Assert.Equal(("Check out other failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.DoesNotContain(CallsSince(app, calls), IsStashCall);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.Empty(await repo.StashListAsync());

        // P24 step 5 (TP3): three files, the count, the button.
        repo.WriteFile("src/lib.txt", "lib\nlocal\n").WriteFile("docs/guide.txt", "guide\nlocal\n");
        await app.Repository.RefreshAsync();
        await app.Operations.CheckoutAsync("refs/heads/other");

        dialog = app.Dialogs.Errors[1];
        Assert.Equal(("Checkout blocked", "3 conflicts prevent checkout. Commit, stash, or discard your changes first.", "Stash changes and check out"), (dialog.Title, dialog.Summary, dialog.ExtraAction));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    [Theory]
    [InlineData("notes", "my own notes\n")]
    [InlineData("notes/today.txt", "today\n")]
    public async Task An_untracked_file_or_a_folder_in_the_way_counts_as_one_and_offers_no_stash(string path, string content)
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile(path, content);
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.CheckoutAsync("refs/heads/other");

        // P24 steps 6 and 8 (TP4, TP5): one path under git's header, no button (D91).
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", OneConflict), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, "git switch --no-guess --no-overwrite-ignore other");

        // Keep ignored files (docs/phase-6-design.md 9.11, 16.1): git's untracked block is asked
        // about after the refusal, in the checkout's operation (the watcher's own check-ignore runs
        // outside every operation); nothing in it is ignored (exit 1), so phase 5's text stands.
        var refused = app.Calls.Snapshot().Last(call => call.CommandText == "git switch --no-guess --no-overwrite-ignore other");
        Assert.NotNull(refused.OperationId);
        Assert.Equal(1, app.Calls.Snapshot().Single(call => call.CommandText == "git check-ignore --stdin -z" && call.OperationId == refused.OperationId).ExitCode);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(content, Read(repo, path));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Create_branch_with_its_checkout_refused_shows_the_checkout_message_without_the_button_and_makes_no_branch()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var otherFiles = await app.ShaAsync("refs/heads/other");
        app.Dialogs.EnterName("topic", option: true).CloseError();

        await app.Operations.CreateBranchAsync(otherFiles);

        // P24 step 3 (TP2, the report's PD4).
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", OneConflict), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, "git switch --create topic --no-overwrite-ignore " + otherFiles);
        Assert.Equal(("Create branch topic failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Null(await repo.RefTargetAsync("refs/heads/topic"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Checkouts_during_an_operation_or_with_conflicted_files_are_refused_before_git_on_every_path()
    {
        using var repo = await Scenarios.ProtectAsync();
        await repo.CommitFileAsync("src/app.txt", "app\nmain\n", "Change the app");
        await repo.GitAllowingFailureAsync("merge", "--no-edit", "refs/heads/other");
        using var app = await OperationsHarness.OpenAsync(repo);
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
        app.Dialogs.CloseError().CloseError().EnterName("topic", option: true).CloseError().CloseError();
        var calls = app.Calls.Snapshot().Count;

        // A branch, a commit the branch reaches (the automatic stash's path), "Create branch", and
        // with conflicts left after the merge was given up, a commit again.
        await app.Operations.CheckoutAsync("refs/heads/other");
        await app.Operations.CheckoutAsync(DescribeTheApp);
        await app.Operations.CreateBranchAsync(DescribeTheApp);
        await repo.GitAsync("merge", "--quit");
        await app.Repository.RefreshAsync();
        await app.Operations.CheckoutAsync(DescribeTheApp);

        Assert.Equal(
            [
                ("Checkout blocked", "Finish or abort the merge first"),
                ("Checkout blocked", "Finish or abort the merge first"),
                ("Checkout blocked", "Finish or abort the merge first"),
                ("Checkout blocked", "Resolve the conflicts first"),
            ],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.All(app.Dialogs.Errors, dialog => Assert.False(dialog.HasOutput || dialog.HasCommand || dialog.HasExtraAction));
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git switch", StringComparison.Ordinal) || IsStashCall(call));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Contains("UU src/app.txt", await PorcelainAsync(repo));
        Assert.Null(await repo.RefTargetAsync("refs/heads/topic"));
        app.AssertScriptFollowed();
    }

    // ---- 9.1, 9.2: the automatic stash ------------------------------------------------------------------

    [Fact]
    public async Task A_commit_the_branch_reaches_is_checked_out_after_the_automatic_stash_which_nothing_pops()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeChangesAsync(repo);
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);
        var calls = app.Calls.Snapshot().Count;

        await app.Operations.CheckoutAsync(DescribeTheApp);

        // P25 step 2: no question, one operation, its texts, the stash and the detached HEAD.
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(1, Ran(app, "Check out f39ea2d"));
        const string Result = "Your changes are in the stash \"Auto stash before checking out f39ea2d\".";
        Assert.Equal(["Stashing your changes…", "Checking out f39ea2d…", Result], texts.Distinct());
        Assert.Equal((Result, OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        var made = CallsSince(app, calls).Where(call => IsStashCall(call) || call.StartsWith("git switch", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            ["git stash push --include-untracked --message \"Auto stash before checking out f39ea2d\"", "git switch --detach --no-overwrite-ignore " + DescribeTheApp],
            made);
        Assert.Equal(DescribeTheApp, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD")).Succeeded, "HEAD is not detached.");
        var stash = Assert.Single(await repo.StashListAsync());
        Assert.Equal(("256077fbd990adad7fe00df31789590d7370aff0", "On main: Auto stash before checking out f39ea2d"), (stash.Sha, stash.Message));
        Assert.Equal(["!! settings.local.json"], await StatusWithIgnoredAsync(repo));
        Assert.Equal(Protect.SettingsLocal, Read(repo, "settings.local.json"));
        Assert.Empty(await repo.BackupRefsAsync());

        // The stash row, on main's tip (D54), drawn as every stash.
        var row = app.Repository.Graph.CommitAt(0);
        Assert.Equal((stash.Sha, CommitKind.Stash, MainWork), (row.Sha, row.Kind, row.Parents.Single()));
        Assert.Equal("stash@{0}", Assert.Single(app.Operations.Refs!.Stashes).Name);

        // P25 step 3: main again; nothing pops the stash (D153 (3)).
        await app.Operations.CheckoutAsync("refs/heads/main");
        Assert.Equal("Checked out main", app.StatusText);
        Assert.Single(await repo.StashListAsync());
        Assert.Equal(["!! settings.local.json"], await StatusWithIgnoredAsync(repo));

        // P25 step 4: the toolbar's Pop is phase 3's plain pop, which the app's check lets through
        // (PD12): every file back, the staged part unstaged.
        await app.Operations.PopNewestStashAsync();
        Assert.Equal("Popped stash@{0}", app.StatusText);
        Assert.Contains("git stash pop stash@{0}", CallsSince(app, calls));
        Assert.Equal([" M src/app.txt", " M src/lib.txt", "?? scratch.txt", "!! settings.local.json"], await StatusWithIgnoredAsync(repo));
        Assert.Equal(("app\nunstaged\n", "lib\nstaged\nand more\n", "scratch\n"), (Read(repo, "src/app.txt"), Read(repo, "src/lib.txt"), Read(repo, "scratch.txt")));
        Assert.Empty(await repo.StashListAsync());
    }

    [Fact]
    public async Task A_tag_the_branch_reaches_takes_the_stash_named_by_the_tag_and_a_clean_tree_takes_none()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeChangesAsync(repo);
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.CheckoutAsync("refs/tags/v1.1");

        // The report's 5.3 variant: the stash ffe0797 "Auto stash before checking out v1.1".
        Assert.Equal("Your changes are in the stash \"Auto stash before checking out v1.1\".", app.StatusText);
        var stash = Assert.Single(await repo.StashListAsync());
        Assert.Equal(("ffe07971f3b3f345f0907e53270d64e776589dae", "On main: Auto stash before checking out v1.1"), (stash.Sha, stash.Message));
        Assert.Equal(DescribeTheApp, await repo.HeadAsync());

        // Back on main with nothing to stash: an older commit is checked out alone (PD8: no stash call).
        await app.Operations.CheckoutAsync("refs/heads/main");
        var calls = app.Calls.Snapshot().Count;
        await app.Operations.CheckoutAsync(await app.ShaAsync("main~2"));

        Assert.Equal("HEAD is now detached at 64166f7", app.StatusText);
        var stopTracking = await app.ShaAsync("main~2");
        Assert.Equal(["git switch --detach --no-overwrite-ignore " + stopTracking], CallsSince(app, calls).Where(call => IsStashCall(call) || call.StartsWith("git switch", StringComparison.Ordinal)));
        Assert.Single(await repo.StashListAsync());
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task HEADs_own_commit_and_a_commit_off_the_branch_take_no_stash()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var calls = app.Calls.Snapshot().Count;

        // P26 step 2 (TP1d): Other edits is not on main: phase 3's checkout, refused, with the button.
        await app.Operations.CheckoutAsync(OtherEdits);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", OneConflict, "Stash changes and check out"), (dialog.Title, dialog.Summary, dialog.ExtraAction));
        AssertOutputOfFailedCall(app, dialog, "git switch --detach --no-overwrite-ignore " + OtherEdits);
        Assert.Equal("Check out 4f5e821 failed", app.StatusText);

        // P26 step 3: HEAD's own commit (D179, PD7): no automatic stash, the change carried; with
        // --no-overwrite-ignore while Keep ignored files is on (D193, docs/phase-6-design.md 9.11),
        // which changes nothing here (HEAD's own commit writes no file).
        await app.Operations.CheckoutAsync(MainWork);

        Assert.Equal("HEAD is now detached at 41f8398", app.StatusText);
        Assert.Contains("git switch --detach --no-overwrite-ignore " + MainWork, CallsSince(app, calls));
        Assert.DoesNotContain(CallsSince(app, calls), IsStashCall);
        Assert.Equal(MainWork, await repo.HeadAsync());
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.Empty(await repo.StashListAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_commit_that_tracks_an_ignored_file_is_refused_with_the_ignored_files_text_on_a_clean_tree_and_after_the_stash_puts_everything_back()
    {
        using var repo = await Scenarios.ProtectAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError().CloseError();
        var calls = app.Calls.Snapshot().Count;
        const string Ignored = "1 ignored file is in the way: 73e0bab tracks a file of the same name. Move it away first.";

        // P26 step 4: a clean tree, no stash call; check-ignore runs without GIT_LITERAL_PATHSPECS
        // (it would exit 128 with it, design 15.8).
        await app.Operations.CheckoutAsync(AddTheGuide);

        var dialog = app.Dialogs.Errors[0];
        Assert.Equal(("Checkout blocked", Ignored), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, "git switch --detach --no-overwrite-ignore " + AddTheGuide);
        var checkIgnore = app.Calls.Snapshot().Skip(calls).Single(call => call.CommandText == "git check-ignore --stdin -z");
        Assert.Equal((0, "settings.local.json\0"), (checkIgnore.ExitCode, checkIgnore.StandardOutput));
        Assert.DoesNotContain(CallsSince(app, calls), IsStashCall);
        Assert.Equal("Check out 73e0bab failed", app.StatusText);
        Assert.Equal(Protect.SettingsLocal, Read(repo, "settings.local.json"));
        Assert.Equal("main", await repo.CurrentBranchAsync());

        // P26 steps 5 and 6: with changes, the stash, the refusal, the stash popped back.
        await MakeChangesAsync(repo);
        await app.Repository.RefreshAsync();
        var before = await repo.ExactStateAsync();
        calls = app.Calls.Snapshot().Count;
        await app.Operations.CheckoutAsync(AddTheGuide);

        dialog = app.Dialogs.Errors[1];
        Assert.Equal(("Checkout blocked", Ignored), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        Assert.Equal(
            [
                "git stash push --include-untracked --message \"Auto stash before checking out 73e0bab\"",
                "git switch --detach --no-overwrite-ignore " + AddTheGuide,
                "git stash pop --index stash@{0}",
            ],
            CallsSince(app, calls).Where(call => IsStashCall(call) || call.StartsWith("git switch", StringComparison.Ordinal)));
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(await repo.StashListAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task HEAD_moved_while_the_checkout_waited_decides_again()
    {
        using var repo = await Scenarios.ProtectAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        // On another branch that does not reach the target: refused, nothing written (9.2 step 1).
        var (hold, ahead) = HoldQueue(app, () => repo.GitAsync("checkout", "--quiet", "other"));
        var checkout = app.Operations.CheckoutAsync(DescribeTheApp);
        await WaitUntilAsync(() => app.Repository.Queue.Waiting == 1, "the checkout to wait");
        hold.SetResult();
        await Task.WhenAll(ahead, checkout).WaitAsync(OperationChainTimeout, TestContext.Current.CancellationToken);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Checkout failed", "HEAD moved while this waited: it was on main and is on other now. Nothing was checked out."),
            (dialog.Title, dialog.Summary));
        Assert.Equal("other", await repo.CurrentBranchAsync());

        // On the same branch, moved on, still reaching it: the checkout goes on.
        await repo.GitAsync("checkout", "--quiet", "main");
        await app.Repository.RefreshAsync();
        (hold, ahead) = HoldQueue(app, () => repo.CommitFileAsync("notes.txt", "later\n", "Later work"));
        checkout = app.Operations.CheckoutAsync(DescribeTheApp);
        await WaitUntilAsync(() => app.Repository.Queue.Waiting == 1, "the checkout to wait");
        hold.SetResult();
        await Task.WhenAll(ahead, checkout).WaitAsync(OperationChainTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(DescribeTheApp, await repo.HeadAsync());
        Assert.Equal("HEAD is now detached at f39ea2d", app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_checkout_the_click_gave_no_stash_is_refused_when_HEAD_moved_onto_a_branch_that_reaches_it()
    {
        using var repo = await Scenarios.ProtectAsync();
        await repo.GitAsync("checkout", "--quiet", "--detach", "main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError().CloseError();
        var calls = app.Calls.Snapshot().Count;

        // HEAD detached at the click (no automatic stash), on main when the checkout runs: phase 3's
        // checkout of Add the guide would overwrite the ignored settings.local.json (0.7), so it is
        // refused before any write (9.1, D124).
        var (hold, ahead) = HoldQueue(app, () => repo.GitAsync("checkout", "--quiet", "main"));
        var checkout = app.Operations.CheckoutAsync(AddTheGuide);
        await WaitUntilAsync(() => app.Repository.Queue.Waiting == 1, "the checkout to wait");
        hold.SetResult();
        await Task.WhenAll(ahead, checkout).WaitAsync(OperationChainTimeout, TestContext.Current.CancellationToken);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Checkout failed", "HEAD moved while this waited: it was detached at 41f8398 and is on main now. Nothing was checked out."),
            (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasOutput || dialog.HasCommand || dialog.HasExtraAction);
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git switch", StringComparison.Ordinal) || IsStashCall(call));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(["!! settings.local.json"], await StatusWithIgnoredAsync(repo));
        Assert.Equal(Protect.SettingsLocal, Read(repo, "settings.local.json"));
        Assert.Empty(await repo.BackupRefsAsync());

        // HEAD's own commit takes no stash at the click; main moved on meanwhile and now reaches it.
        await app.Repository.RefreshAsync();
        (hold, ahead) = HoldQueue(app, () => repo.CommitFileAsync("notes.txt", "later\n", "Later work"));
        checkout = app.Operations.CheckoutAsync(MainWork);
        await WaitUntilAsync(() => app.Repository.Queue.Waiting == 1, "the checkout to wait");
        hold.SetResult();
        await Task.WhenAll(ahead, checkout).WaitAsync(OperationChainTimeout, TestContext.Current.CancellationToken);

        var later = await repo.HeadAsync();
        Assert.Equal(
            ("Checkout failed", $"main moved while this waited: it was at 41f8398 and is at {later[..7]} now. Nothing was checked out."),
            (app.Dialogs.Errors[1].Title, app.Dialogs.Errors[1].Summary));
        Assert.Equal("main", await repo.CurrentBranchAsync());

        // HEAD moved where the target still takes no stash (Other edits is not on main): phase 3's checkout.
        await app.Repository.RefreshAsync();
        (hold, ahead) = HoldQueue(app, () => repo.CommitFileAsync("notes.txt", "later again\n", "More work"));
        checkout = app.Operations.CheckoutAsync(OtherEdits);
        await WaitUntilAsync(() => app.Repository.Queue.Waiting == 1, "the checkout to wait");
        hold.SetResult();
        await Task.WhenAll(ahead, checkout).WaitAsync(OperationChainTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(OtherEdits, await repo.HeadAsync());
        Assert.Equal("HEAD is now detached at 4f5e821", app.StatusText);
        Assert.Equal(["git switch --detach --no-overwrite-ignore " + OtherEdits], CallsSince(app, calls).Where(call => call.StartsWith("git switch", StringComparison.Ordinal) || IsStashCall(call)));
        Assert.Equal(2, app.Dialogs.Errors.Count);
        app.AssertScriptFollowed();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_stash_git_could_not_finish_puts_back_the_files_it_deleted_and_checks_nothing_out(bool putBack)
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n").WriteFile("held.txt", "held\n").WriteFile("other.txt", "other\n");
        using var app = await OperationsHarness.OpenWrappedAsync(repo, inner => HalfMadeStash.Wrap(inner, repo, failRestore: !putBack));
        app.Dialogs.CloseError();

        await app.Operations.CheckoutAsync(DescribeTheApp);

        // 9.2 step 3: "Checkout failed" with the stash push's output; the stash kept, nothing checked out.
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Checkout failed", dialog.Title);
        Assert.Equal(
            putBack
                ? "Git could not clear your changes from the working tree, so nothing was checked out. They are also saved in the stash \"Auto stash before checking out f39ea2d\"."
                : "Your changes are saved in the stash \"Auto stash before checking out f39ea2d\". Git could not finish clearing the working tree, so some of them are still there too. Nothing was checked out. Keep the stash until you have checked your files.",
            dialog.Summary);
        Assert.Contains("failed to remove held.txt", dialog.Output, StringComparison.Ordinal);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("On main: Auto stash before checking out f39ea2d", Assert.Single(await repo.StashListAsync()).Message);
        Assert.Equal(putBack, File.Exists(Path.Combine(repo.Path, "other.txt")));
        Assert.Equal(("app\nlocal\n", "held\n"), (Read(repo, "src/app.txt"), Read(repo, "held.txt")));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_checkout_refused_after_the_stash_whose_pop_back_fails_names_the_stash()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        using var app = await OperationsHarness.OpenWrappedAsync(repo, inner => Intercepting.Wrap(inner, new()
        {
            [nameof(IGitRepository.ApplyStashAsync)] = _ => Task.FromException<ApplyResult>(new GitException("git stash pop --index stash@{0}", 1, "error: could not restore the stash\n")),
            [nameof(IGitRepository.CheckoutDetachedAsync)] = args => args is [_, true, ..]
                ? Task.FromException(new GitException("git switch --detach --no-overwrite-ignore " + DescribeTheApp, 128, "fatal: Unable to create '.git/index.lock': File exists.\n"))
                : null,
        }));
        app.Dialogs.CloseError();

        await app.Operations.CheckoutAsync(DescribeTheApp);

        // 9.2 step 5: phase 3's sentence for changes kept, naming this stash.
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Checkout failed", "Git could not check out f39ea2d, and your changes could not be re-applied. They are kept in the stash \"Auto stash before checking out f39ea2d\"."),
            (dialog.Title, dialog.Summary));
        Assert.Equal("On main: Auto stash before checking out f39ea2d", Assert.Single(await repo.StashListAsync()).Message);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        app.AssertScriptFollowed();
    }

    // ---- 9.3: merge, cherry-pick, revert and pull refused for changes --------------------------------

    [Fact]
    public async Task Merge_pick_and_revert_refused_for_a_changed_file_count_it_and_for_a_staged_one_say_so()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError().CloseError().CloseError().CloseError().CloseError().CloseError().CloseError();

        // P27 steps 2 to 4 (TP7, TP8, TP12): git's "by merge" blocks.
        await app.Operations.MergeAsync("refs/heads/other", MergeMode.Default);
        await app.Operations.CherryPickAsync(OtherEdits);
        repo.WriteFile("README.md", "# Protect\n\nLocal.\n");
        await app.Repository.RefreshAsync();
        await app.Operations.RevertAsync(DescribeTheApp);
        AssertOutputOfFailedCall(app, app.Dialogs.Errors[2], "git revert --no-edit " + DescribeTheApp);

        // P27 steps 5 to 8 (TP9, TP10, TP11): something staged, no names.
        await repo.GitAsync("checkout", "--", "src/app.txt");
        await repo.WriteFile("README.md", "# Protect\n\nStaged.\n").GitAsync("add", "--", "README.md");
        await app.Repository.RefreshAsync();
        await app.Operations.MergeAsync("refs/heads/other", MergeMode.Default);
        await app.Operations.CherryPickAsync(OtherEdits);
        await app.Operations.RevertAsync(DescribeTheApp);

        // A list of picks refused at its start (phase 4's one git call) says the same.
        await app.Operations.CherryPickSeveralAsync([OtherEdits, await app.ShaAsync("refs/heads/other")]);

        Assert.Equal(
            [
                ("Merge failed", "1 conflict prevents the merge. Commit, stash, or discard your changes first."),
                ("Cherry-pick failed", "1 conflict prevents the cherry-pick. Commit, stash, or discard your changes first."),
                ("Revert failed", "1 conflict prevents the revert. Commit, stash, or discard your changes first."),
                ("Merge failed", "Your staged changes prevent the merge. Commit, stash, or unstage them first."),
                ("Cherry-pick failed", "Your staged changes prevent the cherry-pick. Commit, stash, or unstage them first."),
                ("Revert failed", "Your staged changes prevent the revert. Commit, stash, or unstage them first."),
                ("Cherry-pick failed", "Your staged changes prevent the cherry-pick. Commit, stash, or unstage them first."),
            ],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.All(app.Dialogs.Errors, dialog => Assert.True(dialog.HasOutput));
        Assert.Equal(MainWork, await repo.HeadAsync());
        Assert.Equal(["M  README.md"], await PorcelainAsync(repo));
        foreach (var name in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer" })
        {
            Assert.False(Path.Exists(Path.Combine(repo.Path, ".git", name)), $"{name} was left.");
        }

        app.AssertScriptFollowed();
    }

    [Theory]
    [InlineData("src/remote.txt", "src/remote.txt")]
    [InlineData("src/remote", "src/remote/mine.txt")]
    public async Task A_pull_whose_fast_forward_git_refuses_for_an_untracked_file_or_folder_counts_it(string pushed, string mine)
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", pushed, "remote\n", "Remote change");
        repo.WriteFile(mine, "mine\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        // Git's untracked-files block, or its folder block, which reads the same as a checkout's
        // (9.3's second table: pull's merge and fast-forward say what a merge says).
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "1 conflict prevents the merge. Commit, stash, or discard your changes first."), (dialog.Title, dialog.Summary));
        Assert.StartsWith("git merge ", dialog.Command, StringComparison.Ordinal);
        Assert.Contains(pushed, dialog.Output, StringComparison.Ordinal);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal("mine\n", Read(repo, mine));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_pull_whose_rebase_git_refuses_for_an_untracked_folder_is_not_told_as_a_merge()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote", "remote\n", "Remote change");
        await repo.CommitFileAsync("local.txt", "local\n", "Local work");
        repo.WriteFile("src/remote/mine.txt", "mine\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();
        app.Dialogs.CloseError();

        await app.Operations.PullRebaseAsync();

        // The rebase's own checkout refused: the pull's default summary, not 9.3's merge sentence.
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "Git could not pull origin/main into main."), (dialog.Title, dialog.Summary));
        Assert.StartsWith("git rebase", dialog.Command, StringComparison.Ordinal);
        Assert.Contains("Updating the following directories would lose untracked files in them:", dialog.Output, StringComparison.Ordinal);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal("mine\n", Read(repo, "src/remote/mine.txt"));
        app.AssertScriptFollowed();
    }

    // ---- 9.3: a stash's apply or pop checked before git ------------------------------------------------

    [Fact]
    public async Task A_stash_is_applied_only_when_nothing_is_in_its_way_counted_by_its_paths_both_sides_of_a_rename_and_its_untracked_files()
    {
        using var repo = await Scenarios.ProtectAsync();

        // P27 part B's stash, with a rename to a name with é: src/app.txt changed, docs/old.txt
        // renamed, todo.txt untracked.
        repo.WriteFile("src/app.txt", "app\nstashed\n").WriteFile("todo.txt", "todo\n");
        await repo.GitAsync("mv", "docs/old.txt", "docs/très old.txt");
        await repo.GitWithEnvironmentAsync(new Dictionary<string, string?> { ["GIT_LITERAL_PATHSPECS"] = null }, "stash", "push", "--include-untracked", "--message", "App experiment");
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");
        app.Dialogs.CloseError().CloseError().CloseError();
        var calls = app.Calls.Snapshot().Count;

        // P27 step 9: one local change in its way; git not run, nothing written.
        await app.Operations.ApplyStashAsync(stash);

        // Step 11 with more in the way: todo.txt exists, and the rename's new name is taken.
        repo.WriteFile("todo.txt", "mine\n").WriteFile("docs/très old.txt", "mine too\n");
        await app.Repository.RefreshAsync();
        await app.Operations.PopNewestStashAsync();

        // Its old name changed locally counts too.
        await repo.GitAsync("checkout", "--", "src/app.txt");
        File.Delete(Path.Combine(repo.Path, "todo.txt"));
        File.Delete(Path.Combine(repo.Path, "docs", "très old.txt"));
        repo.WriteFile("docs/old.txt", "old notes, edited\n");
        await app.Repository.RefreshAsync();
        await app.Operations.ApplyStashAsync(stash);

        Assert.Equal(
            [
                ("Applying the stash failed", "1 conflict prevents applying the stash. Commit, stash, or discard your changes first."),
                ("Applying the stash failed", "3 conflicts prevent applying the stash. Commit, stash, or discard your changes first."),
                ("Applying the stash failed", "1 conflict prevents applying the stash. Commit, stash, or discard your changes first."),
            ],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.All(app.Dialogs.Errors, dialog => Assert.False(dialog.HasOutput || dialog.HasCommand));
        Assert.Equal(("Apply stash@{0} failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.DoesNotContain(CallsSince(app, calls), IsStashCall);
        Assert.Equal(stash, Assert.Single(await repo.StashListAsync()).Sha);
        Assert.Empty(await repo.BackupRefsAsync());

        // Nothing in the way: git applies it.
        await repo.GitAsync("checkout", "--", "docs/old.txt");
        await app.Repository.RefreshAsync();
        await app.Operations.ApplyStashAsync(stash);
        Assert.Equal("Applied stash@{0}", app.StatusText);
        Assert.Equal(("app\nstashed\n", "todo\n"), (Read(repo, "src/app.txt"), Read(repo, "todo.txt")));
        app.AssertScriptFollowed();
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    /// <summary>The report's P25 changes: src/lib.txt staged then changed again, src/app.txt changed, scratch.txt untracked.</summary>
    private static async Task MakeChangesAsync(TempRepo repo)
    {
        await repo.WriteFile("src/lib.txt", "lib\nstaged\n").GitAsync("add", "--", "src/lib.txt");
        repo.WriteFile("src/lib.txt", "lib\nstaged\nand more\n").WriteFile("src/app.txt", "app\nunstaged\n").WriteFile("scratch.txt", "scratch\n");
    }

    /// <summary>The report's "no git stash call": a call whose arguments start with stash push, pop or apply.</summary>
    private static bool IsStashCall(string command) =>
        command.StartsWith("git stash push", StringComparison.Ordinal)
        || command.StartsWith("git stash pop", StringComparison.Ordinal)
        || command.StartsWith("git stash apply", StringComparison.Ordinal);

    /// <summary>The command lines of the calls made since <paramref name="mark"/> calls had been recorded.</summary>
    private static List<string> CallsSince(OperationsHarness app, int mark) => [.. app.Calls.Snapshot().Skip(mark).Select(call => call.CommandText)];

    private static async Task<IReadOnlyList<string>> StatusWithIgnoredAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("status", "--porcelain=v1", "--untracked-files=all", "--ignored")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// The real repository, except for the methods <c>overrides</c> names: each answers with its
    /// task, or passes the call on when it gives null.
    /// </summary>
    public class Intercepting : DispatchProxy
    {
        private IGitRepository _inner = null!;
        private Dictionary<string, Func<object?[], object?>> _overrides = null!;

        public static IGitRepository Wrap(IGitRepository inner, Dictionary<string, Func<object?[], object?>> overrides)
        {
            var proxy = Create<IGitRepository, Intercepting>();
            var intercepting = (Intercepting)(object)proxy;
            intercepting._inner = inner;
            intercepting._overrides = overrides;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (_overrides.TryGetValue(targetMethod.Name, out var answer) && answer(args ?? []) is { } task)
            {
                return task;
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(ex.InnerException);
                throw;
            }
        }
    }

    [Fact]
    public async Task A_stash_whose_reset_git_could_not_finish_puts_back_the_tracked_files_it_rewrote_before_it_says_they_are_in_both_places()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n").WriteFile("src/lib.txt", "lib\nlocal\n").WriteFile("other.txt", "other\n");
        using var app = await OperationsHarness.OpenWrappedAsync(repo, inner => HalfMadeStash.Wrap(inner, repo, failRestore: false, trackedHeld: true));
        app.Dialogs.CloseError();

        await app.Operations.CheckoutAsync(DescribeTheApp);

        // Design 15.9: git's reset rewrote src/lib.txt before it stopped at the held src/app.txt;
        // both are back with the untracked file before the dialog says the changes are in both places.
        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            "Git could not clear your changes from the working tree, so nothing was checked out. They are also saved in the stash \"Auto stash before checking out f39ea2d\".",
            dialog.Summary);
        Assert.Contains("unable to unlink old 'src/app.txt'", dialog.Output, StringComparison.Ordinal);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Single(await repo.StashListAsync());
        Assert.Equal(("app\nlocal\n", "lib\nlocal\n", "other\n"), (Read(repo, "src/app.txt"), Read(repo, "src/lib.txt"), Read(repo, "other.txt")));
        app.AssertScriptFollowed();
    }

    /// <summary>
    /// The real repository, except that <see cref="IGitRepository.StashAsync"/> stops as git does
    /// when another program holds a file open on Windows (design 0.7, 15.9): the stash is saved,
    /// the other untracked file deleted, and git's error thrown. An untracked file held
    /// (<c>held.txt</c>) stops git's clean, leaving it and the tracked changes; a tracked one held
    /// (<c>trackedHeld</c>: <c>src/app.txt</c>) stops git's reset after it rewrote the other tracked
    /// file (<c>src/lib.txt</c>). With <c>failRestore</c>, putting the files back fails too.
    /// </summary>
    private static class HalfMadeStash
    {
        public static IGitRepository Wrap(IGitRepository inner, TempRepo repo, bool failRestore, bool trackedHeld = false) => Intercepting.Wrap(inner, new()
        {
            [nameof(IGitRepository.StashAsync)] = args => StopHalfWayAsync(inner, repo, (string?)args[0], trackedHeld),
            [nameof(IGitRepository.PutBackUnclearedStashAsync)] = args => failRestore
                ? Task.FromException<bool>(new GitException("git restore", 128, "error: unable to write other.txt\n"))
                : null,
        });

        private static async Task<string?> StopHalfWayAsync(IGitRepository inner, TempRepo repo, string? message, bool trackedHeld)
        {
            var stash = await inner.StashAsync(message, includeUntracked: true);
            await repo.GitAsync("stash", "apply", "--index", stash!);
            File.Delete(Path.Combine(repo.Path, "other.txt"));
            if (trackedHeld)
            {
                // The reset rewrote src/lib.txt and wrote no index.
                File.WriteAllText(Path.Combine(repo.Path, "src", "lib.txt"), Scenarios.ProtectFiles.Lib);
            }

            throw new GitException(
                $"git stash push --include-untracked --message \"{message}\"",
                trackedHeld ? 128 : 1,
                trackedHeld
                    ? "error: unable to unlink old 'src/app.txt': Invalid argument\nfatal: Could not reset index file to revision 'HEAD'.\n"
                    : "warning: failed to remove held.txt: Permission denied\n",
                standardOutput: $"Saved working directory and index state On main: {message}\n");
        }
    }
}
