using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using Ignored = VisualCommit.Testing.Scenarios.IgnoredFiles;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP-IA: Keep ignored files, the app half (D193, D196, D221; docs/phase-6-design.md section 9,
/// its 9.17), through <see cref="OperationsHarness"/> against real git on the "ignored" scenario
/// (17.1; the report's scenario facts 4): every checkout path of 9.11 refused by git with
/// <c>--no-overwrite-ignore</c> and told in D179's words with git's count; every other operation of
/// 9.2 refused by the app's own check before git with 9.15's text and its error title, without a
/// git write, a backup ref or an undo entry, after the reads 9.4 to 9.8 count; the two "Check out
/// and …" refused before their checkout; with the setting off, phase 5's commands and none of the
/// reads, while the automatic stash's checkout of an older commit of the current branch keeps
/// D179's option and its words (D221); the setting read when the operation runs; and a read that
/// fails leaving the operation to git. The pull's check is <c>IntegratePullTests</c>' (the git
/// layer) and its app path WP-B's. Keep ignored files is on by default (no <c>keepIgnoredFiles</c>
/// in the harness's settings); a test turns it off through the settings store.
/// </summary>
[Collection(RepositoryOperationsTestSupport.BranchesCollection)]
public class RepositoryOperationsKeepIgnoredTests
{
    // The "ignored" scenario's commits (the report's scenario facts 4.1; Phase6ScenarioTests pins them).
    private const string TrackTheSecret = "1ac759d23fd78a78f7660d289a1373ea85378b69";

    private const string StopTracking = "39bd8031f98ec9a1a6d02519ffabd35ca693bc63";

    private const string VendorSecret = "133a796ef1cfa3b4f2dc3237b5342751f85fcc3f";

    private const string BuildFile = "c8446bb6a7efb5eb9cc91d4a95e2bd37c0be7ced";

    private const string MainWork = "c49cac40cc05ce7ebe709555621751082c868662";

    private const string Stash = "c8795683e3b86ad70873326b9897659e3c5140dd";

    private const string CheckIgnore = "git check-ignore --stdin -z";

    // ---- 9.11: every checkout path, refused by git with the option, in D179's words -------------------

    [Theory]
    [InlineData("refs/heads/vendor", "git switch --no-guess --no-overwrite-ignore vendor", "vendor", "secret.env")]
    [InlineData("refs/remotes/origin/vendor", "git switch --no-guess --no-overwrite-ignore vendor", "origin/vendor", "secret.env")]
    [InlineData("refs/remotes/origin/vendor-remote", "git switch --create vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote", "origin/vendor-remote", "secret.env")]
    [InlineData("refs/heads/tools", "git switch --no-guess --no-overwrite-ignore tools", "tools", "build/out.bin")]
    [InlineData(VendorSecret, "git switch --detach --no-overwrite-ignore " + VendorSecret, "133a796", "secret.env")]
    public async Task A_checkout_git_refuses_for_an_ignored_file_says_so_in_D179s_words(string target, string command, string name, string ignored)
    {
        // G22 steps 2 to 4, 6 and 7: a local branch, a remote branch with a local branch of its name
        // (D93) and without one, an ignored folder where the target has a file (one path: git's
        // count), and a commit main does not reach (D193's detached checkout, no automatic stash).
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.CheckoutAsync(target);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", OperationTexts.IgnoredFilesInTheWay(1, name)), (dialog.Title, dialog.Summary));
        Assert.Equal($"1 ignored file is in the way: {name} tracks a file of the same name. Move it away first.", dialog.Summary);
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, command);
        Assert.Equal(($"Check out {name} failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));

        // The check-ignore of the paths git named (a folder by its files), after git's refusal, in
        // the same operation; no stash: a branch, and a commit the branch does not reach, are no
        // automatic stash's target (D179).
        var calls = OperationCalls(app, mark);
        var refused = calls.FindIndex(call => call.CommandText == command);
        Assert.True(refused >= 0, $"{command} ran.");
        var check = Assert.Single(calls, call => call.CommandText == CheckIgnore);
        Assert.True(calls.IndexOf(check) > refused, "check-ignore follows git's refusal.");
        Assert.Equal((0, ignored + "\0"), (check.ExitCode, check.StandardOutput));
        Assert.DoesNotContain(calls, call => call.CommandText.StartsWith("git stash push", StringComparison.Ordinal));

        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Null(await repo.RefTargetAsync("refs/heads/vendor-remote"));
        Assert.Empty(app.Repository.UndoHistory.Entries);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Create_branch_with_its_checkout_refused_names_the_start_as_the_dialog_named_it_and_makes_no_branch()
    {
        // G22 step 5 (the branch menu's start, GD4), and a commit's start named by its short SHA.
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        app.Dialogs.EnterName("vendor-copy", option: true).CloseError().EnterName("vendor-commit", option: true).CloseError();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.CreateBranchAsync("refs/heads/vendor");
        await app.Operations.CreateBranchAsync(VendorSecret);

        Assert.Equal(
            [
                ("Checkout blocked", "1 ignored file is in the way: vendor tracks a file of the same name. Move it away first."),
                ("Checkout blocked", "1 ignored file is in the way: 133a796 tracks a file of the same name. Move it away first."),
            ],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.All(app.Dialogs.Errors, dialog => Assert.False(dialog.HasExtraAction));
        AssertOutputOfFailedCall(app, app.Dialogs.Errors[0], "git switch --create vendor-copy --no-overwrite-ignore refs/heads/vendor");
        AssertOutputOfFailedCall(app, app.Dialogs.Errors[1], "git switch --create vendor-commit --no-overwrite-ignore " + VendorSecret);
        Assert.Equal("Create branch vendor-commit failed", app.StatusText);
        var calls = OperationCalls(app, mark);
        Assert.Equal(2, calls.Count(call => call.CommandText == CheckIgnore));
        Assert.DoesNotContain(calls, call => call.CommandText.StartsWith("git branch ", StringComparison.Ordinal));
        Assert.Null(await repo.RefTargetAsync("refs/heads/vendor-copy"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/vendor-commit"));
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Stash_changes_and_check_out_reads_the_ignored_files_before_it_pops_the_changes_back()
    {
        // D91's path (9.11): the tracked change stashed, git's refusal for the ignored file, the
        // check-ignore before the pop back, the changes back, and D179's words in place of 2.31's.
        using var repo = await Scenarios.IgnoredAsync();
        repo.WriteFile("app.txt", "app\nlocal\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.StashAndCheckoutAsync("refs/heads/vendor");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "1 ignored file is in the way: vendor tracks a file of the same name. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, "git switch --no-guess --no-overwrite-ignore vendor");
        var made = OperationCalls(app, mark)
            .Select(call => call.CommandText)
            .Where(call => call.StartsWith("git stash push", StringComparison.Ordinal) || call.StartsWith("git stash pop", StringComparison.Ordinal)
                || call.StartsWith("git switch", StringComparison.Ordinal) || call == CheckIgnore)
            .ToList();
        Assert.Equal(4, made.Count);
        Assert.StartsWith("git stash push", made[0], StringComparison.Ordinal);
        Assert.DoesNotContain("--include-untracked", made[0], StringComparison.Ordinal);
        Assert.Equal(["git switch --no-guess --no-overwrite-ignore vendor", CheckIgnore], made[1..3]);
        Assert.StartsWith("git stash pop", made[3], StringComparison.Ordinal);

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(("app\nlocal\n", Ignored.SecretLocal), (Read(repo, "app.txt"), Read(repo, "secret.env")));
        Assert.Equal([" M app.txt"], await PorcelainAsync(repo));
        Assert.Equal([Stash], (await repo.StashListAsync()).Select(stash => stash.Sha));
    }

    [Fact]
    public async Task Check_out_and_merge_and_check_out_and_rebase_are_refused_before_their_checkout()
    {
        // 9.9: into (or the tip) is the branch they would check out; "plain" lacks secret.env, so
        // its checkout would succeed and the merge or the rebase would then write over LOCAL.
        using var repo = await Scenarios.IgnoredAsync();
        await repo.GitAsync("branch", "plain", StopTracking);
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.CheckoutAndMergeAsync("refs/heads/plain", "refs/heads/vendor");
        var merged = OperationCalls(app, mark);
        mark = app.Calls.Snapshot().Count;
        await app.Operations.CheckoutAndRebaseAsync("refs/heads/plain", new RebaseTarget(VendorSecret, "vendor"));
        var rebased = OperationCalls(app, mark);

        Assert.Equal(
            [
                ("Merge failed", "1 ignored file is in the way of the merge: secret.env. Move it away first."),
                ("Rebase failed", "1 ignored file is in the way of the rebase: secret.env. Move it away first."),
            ],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.All(app.Dialogs.Errors, dialog => Assert.False(dialog.HasOutput || dialog.HasCommand || dialog.HasExtraAction));
        Assert.Equal("Check out plain and rebase it onto vendor failed", app.StatusText);
        Assert.Equal(
            [
                "git diff-tree -r -z --no-renames --name-only --diff-filter=A refs/heads/plain refs/heads/vendor --",
                CheckIgnore,
                "git merge-base --all refs/heads/plain refs/heads/vendor",
                $"git diff-tree -r -z --no-renames --name-only --diff-filter=AMT {StopTracking} refs/heads/vendor --",
            ],
            IgnoredReads(merged));
        Assert.Contains($"git merge-base --is-ancestor {VendorSecret} refs/heads/plain", IgnoredReads(rebased));
        Assert.Equal(CheckIgnore, IgnoredReads(rebased)[^1]);
        AssertNoWrite(merged);
        AssertNoWrite(rebased);
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
    }

    [Fact]
    public async Task The_checkouts_of_check_out_and_merge_rebase_and_the_interactive_list_say_so_in_D179s_words()
    {
        // 9.11: their own checks find nothing (main adds nothing vendor lacks; vendor is only ahead
        // of "Stop tracking the secret", and the list's one pick is the one git skips), so git's
        // refusal of the checkout of vendor names it.
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var list = await app.Repository.Repository.ReadRebaseListAsync(StopTracking, "refs/heads/vendor", fromCommit: false, options: null, TestContext.Current.CancellationToken);
        var request = new InteractiveRebaseRequest(new RebaseTarget(StopTracking, "39bd803"), "refs/heads/vendor", FromHere: false, CheckoutFirst: true);
        var plan = new RebasePlan(list.Onto, list.BranchTip, "refs/heads/vendor", [new RebaseStep(VendorSecret, RebaseAction.Pick, null)]);

        await app.Operations.CheckoutAndMergeAsync("refs/heads/vendor", "refs/heads/main");
        await app.Operations.CheckoutAndRebaseAsync("refs/heads/vendor", new RebaseTarget(StopTracking, "39bd803"));
        Assert.False(await app.Operations.RunInteractiveRebaseAsync(plan, request));

        const string Blocked = "1 ignored file is in the way: vendor tracks a file of the same name. Move it away first.";
        Assert.Equal([("Checkout blocked", Blocked), ("Checkout blocked", Blocked), ("Checkout blocked", Blocked)], app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.All(app.Dialogs.Errors, dialog => AssertOutputOfFailedCall(app, dialog, "git switch --no-guess --no-overwrite-ignore vendor"));
        Assert.All(app.Dialogs.Errors, dialog => Assert.False(dialog.HasExtraAction));
        Assert.Equal("Interactive rebase of vendor failed", app.StatusText);
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git merge ", StringComparison.Ordinal) || call.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
    }

    [Fact]
    public async Task Stash_changes_and_check_out_with_nothing_left_to_stash_says_so_in_D179s_words()
    {
        // 9.11 (14.4's every checkout path): on a clean tree "Stash changes and check out" stashes
        // nothing and runs a plain checkout, whose refusal for the ignored secret.env is told as
        // every checkout's.
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.StashAndCheckoutAsync("refs/heads/vendor");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "1 ignored file is in the way: vendor tracks a file of the same name. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, "git switch --no-guess --no-overwrite-ignore vendor");
        Assert.Equal("Check out vendor with your changes failed", app.StatusText);
        var calls = OperationCalls(app, mark);
        Assert.Contains(calls, call => call.CommandText.StartsWith("git stash push", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, call => call.CommandText.StartsWith("git stash pop", StringComparison.Ordinal));
        Assert.Equal("secret.env\0", Assert.Single(calls, call => call.CommandText == CheckIgnore).StandardOutput);
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
    }

    [Fact]
    public async Task Aborts_way_back_after_check_out_and_rebase_says_so_in_D179s_words()
    {
        // 9.11 (GD2): "Check out plain and rebase it onto main" from vendor stops with a conflict;
        // an ignored secret.env made during the stop is in the way of vendor, which Abort checks out
        // again after git's abort: only the way back failed.
        using var repo = await Scenarios.IgnoredAsync();
        await repo.CreateBranchAsync("plain", StopTracking);
        await repo.CheckoutAsync("plain");
        await repo.CommitFileAsync("app.txt", "app\nplain\n", "Plain work");
        await repo.CheckoutAsync("vendor");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm();

        await app.Operations.CheckoutAndRebaseAsync("refs/heads/plain", new RebaseTarget(MainWork, "main"));
        Assert.Equal(("Rebase stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal("refs/heads/vendor", app.Operations.ReadReturnBranch());
        Assert.False(File.Exists(Path.Combine(repo.Path, "secret.env")), "The checkout of plain removed vendor's secret.env.");
        repo.WriteFile("secret.env", Ignored.SecretLocal);
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.AbortAsync();

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "1 ignored file is in the way: vendor tracks a file of the same name. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, "git switch --no-guess --no-overwrite-ignore vendor");
        Assert.Equal("Abort the rebase failed", app.StatusText);
        var calls = OperationCalls(app, mark);
        Assert.Contains(calls, call => call.CommandText == "git rebase --abort");
        Assert.Equal("secret.env\0", Assert.Single(calls, call => call.CommandText == CheckIgnore).StandardOutput);
        Assert.Equal("plain", await repo.CurrentBranchAsync());
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-merge")), "The rebase was not aborted.");
        Assert.Equal(Ignored.SecretLocal, Read(repo, "secret.env"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_kept_commits_and_a_tags_checkout_say_so_in_D179s_words()
    {
        // 9.2's first row (GD2, GD10): the backups list's and the reflog page's "Check out this
        // commit" (CheckoutKeptCommitAsync), and a tag, each both ways: a commit main does not reach
        // (D193's detached checkout) and one it reaches (the automatic stash's, D179).
        using var repo = await Scenarios.IgnoredAsync();
        await repo.GitAsync("tag", "v1", VendorSecret);
        await repo.GitAsync("tag", "v0", TrackTheSecret);
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var marks = new List<int>();

        foreach (var checkout in new Func<Task>[]
        {
            () => app.Operations.CheckoutKeptCommitAsync(VendorSecret),
            () => app.Operations.CheckoutKeptCommitAsync(TrackTheSecret),
            () => app.Operations.CheckoutAsync("refs/tags/v1"),
            () => app.Operations.CheckoutAsync("refs/tags/v0"),
        })
        {
            marks.Add(app.Calls.Snapshot().Count);
            await checkout();
        }

        string[] names = ["133a796", "1ac759d", "v1", "v0"];
        Assert.Equal(
            names.Select(name => ("Checkout blocked", $"1 ignored file is in the way: {name} tracks a file of the same name. Move it away first.")),
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        var all = app.Calls.Snapshot();
        for (var i = 0; i < names.Length; i++)
        {
            var end = i + 1 < marks.Count ? marks[i + 1] : all.Count;
            var calls = all.Skip(marks[i]).Take(end - marks[i]).Where(call => call.OperationId is not null).ToList();
            var refused = Assert.Single(calls, call => call.CommandText.StartsWith("git switch --detach --no-overwrite-ignore ", StringComparison.Ordinal));
            AssertOutputOfFailedCall(app, app.Dialogs.Errors[i], refused.CommandText);
            Assert.Equal("secret.env\0", Assert.Single(calls, call => call.CommandText == CheckIgnore && call.OperationId == refused.OperationId).StandardOutput);
            Assert.False(app.Dialogs.Errors[i].HasExtraAction);
        }

        Assert.Equal("Check out v0 failed", app.StatusText);
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
    }

    [Fact]
    public async Task The_interactive_Start_that_checks_out_first_is_refused_by_its_own_check_before_the_checkout()
    {
        // 9.7, 9.9: "Check out plainwork and rebase it onto vendor interactively…": git would check
        // out vendor's tip first, writing secret.env over LOCAL; refused before the checkout.
        using var repo = await Scenarios.IgnoredAsync();
        await repo.CreateBranchAsync("plainwork", StopTracking);
        await repo.CheckoutAsync("plainwork");
        var plainWork = await repo.CommitFileAsync("app.txt", "app\nplain\n", "Plain work");
        await repo.CheckoutAsync("main");
        repo.WriteFile("secret.env", Ignored.SecretLocal);
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var list = await app.Repository.Repository.ReadRebaseListAsync(VendorSecret, "refs/heads/plainwork", fromCommit: false, options: null, TestContext.Current.CancellationToken);
        Assert.Equal((VendorSecret, plainWork), (list.Onto, list.BranchTip));
        var request = new InteractiveRebaseRequest(new RebaseTarget(VendorSecret, "vendor"), "refs/heads/plainwork", FromHere: false, CheckoutFirst: true);
        var plan = new RebasePlan(list.Onto, list.BranchTip, "refs/heads/plainwork", [new RebaseStep(plainWork, RebaseAction.Pick, null)]);
        var mark = app.Calls.Snapshot().Count;

        Assert.False(await app.Operations.RunInteractiveRebaseAsync(plan, request));

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Interactive rebase failed", "1 ignored file is in the way of the rebase: secret.env. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasOutput || dialog.HasCommand || dialog.HasExtraAction);
        Assert.Equal("Interactive rebase of plainwork failed", app.StatusText);
        var calls = OperationCalls(app, mark);
        Assert.Equal(CheckIgnore, IgnoredReads(calls)[^1]);
        AssertNoWrite(calls);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_merge_commits_revert_is_checked_against_its_first_parent()
    {
        // 9.5: a merge reverted with -m 1 is read as "<sha> <first parent>" (its own line prints
        // nothing). "Merge main into vendor" has vendor's tip as its first parent and main's tree:
        // against that parent it deletes secret.env, so its revert writes secret.env over LOCAL.
        using var repo = await Scenarios.IgnoredAsync();
        var merge = (await repo.GitAsync("commit-tree", MainWork + "^{tree}", "-p", VendorSecret, "-p", MainWork, "-m", "Merge main into vendor")).StandardOutput.Trim();
        await repo.CreateBranchAsync("merged", merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.RevertAsync(merge);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Revert failed", "1 ignored file is in the way of the revert: secret.env. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.Equal($"Revert {merge[..7]} failed", app.StatusText);
        var calls = OperationCalls(app, mark);
        var read = Assert.Single(calls, call => call.CommandText == "git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT -R");
        Assert.Contains("secret.env\0", read.StandardOutput, StringComparison.Ordinal);
        AssertNoWrite(calls);
        Assert.Equal(before, await repo.ExactStateAsync());
    }

    // ---- 9.4 to 9.8: the app's own check before git -----------------------------------------------------

    public static TheoryData<string> RefusedOperations => ["merge", "fast-forward", "rebase", "cherry-pick", "revert", "apply", "pop"];

    [Theory]
    [MemberData(nameof(RefusedOperations))]
    public async Task An_operation_git_has_no_exact_option_for_is_refused_before_git_with_its_own_title(string operation)
    {
        // G23 part A (the report's TR1 to TR5) and 9.4's fast-forward: the reads 9.17 counts, then
        // the refusal with 9.15's text and the operation's error title, no git output, and nothing
        // written: no git write, no backup ref, no undo entry, the exact state unchanged.
        using var repo = await Scenarios.IgnoredAsync();
        string? ahead = null;
        if (operation == "fast-forward")
        {
            // A branch one commit ahead of main whose commit tracks secret.env (vendor's tree).
            ahead = (await repo.GitAsync("commit-tree", VendorSecret + "^{tree}", "-p", MainWork, "-m", "Ahead")).StandardOutput.Trim();
            await repo.GitAsync("branch", "ahead", ahead);
        }

        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var mark = app.Calls.Snapshot().Count;

        var (title, errorTitle, summary, reads) = operation switch
        {
            "merge" => (
                "Merge vendor",
                "Merge failed",
                "1 ignored file is in the way of the merge: secret.env. Move it away first.",
                (string[])
                [
                    "git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/heads/vendor --",
                    CheckIgnore,
                    "git merge-base --all HEAD refs/heads/vendor",
                    $"git diff-tree -r -z --no-renames --name-only --diff-filter=AMT {StopTracking} refs/heads/vendor --",
                ]),
            "fast-forward" => (
                "Fast-forward main",
                "Fast-forward failed",
                "1 ignored file is in the way of the merge: secret.env. Move it away first.",
                [
                    "git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/heads/ahead --",
                    CheckIgnore,
                    "git merge-base --all HEAD refs/heads/ahead",
                    $"git diff-tree -r -z --no-renames --name-only --diff-filter=AMT {MainWork} refs/heads/ahead --",
                ]),
            "rebase" => (
                "Rebase main onto vendor",
                "Rebase failed",
                "1 ignored file is in the way of the rebase: secret.env. Move it away first.",
                [
                    // The four side by side, in the order they started; then check-ignore.
                    $"git merge-base --is-ancestor {VendorSecret} HEAD",
                    $"git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD {VendorSecret} --",
                    $"git log --no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT {VendorSecret}..HEAD --",
                    $"git rev-list --min-parents=2 --max-count=1 {VendorSecret}..HEAD --",
                    CheckIgnore,
                ]),
            "cherry-pick" => (
                "Cherry-pick 133a796",
                "Cherry-pick failed",
                "1 ignored file is in the way of the cherry-pick: secret.env. Move it away first.",
                ["git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT", CheckIgnore]),
            "revert" => (
                "Revert 39bd803",
                "Revert failed",
                "1 ignored file is in the way of the revert: secret.env. Move it away first.",
                ["git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT -R", CheckIgnore]),
            _ => (
                operation == "apply" ? "Apply stash@{0}" : "Pop stash@{0}",
                "Applying the stash failed",
                "1 ignored file is in the way of applying the stash: secret.env. Move it away first.",

                // 9.8: no process before check-ignore (the stash's paths are D179's count's).
                [CheckIgnore]),
        };

        await (operation switch
        {
            "merge" => app.Operations.MergeAsync("refs/heads/vendor", MergeMode.Default),
            "fast-forward" => app.Operations.MergeAsync("refs/heads/ahead", MergeMode.FastForwardOnly),
            "rebase" => app.Operations.RebaseAsync(new RebaseTarget(VendorSecret, "vendor")),
            "cherry-pick" => app.Operations.CherryPickAsync(VendorSecret),
            "revert" => app.Operations.RevertAsync(StopTracking),
            "apply" => app.Operations.ApplyStashAsync(Stash),
            _ => app.Operations.PopStashAsync(Stash),
        });

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal((errorTitle, summary), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasOutput || dialog.HasCommand || dialog.HasExtraAction);
        Assert.Equal(($"{title} failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));

        var calls = OperationCalls(app, mark);
        var found = IgnoredReads(calls);
        if (operation == "rebase")
        {
            // 9.6's four reads run side by side: their order in the log is not fixed (GD18).
            Assert.Equal(reads[..4].Order(StringComparer.Ordinal), found.Take(4).Order(StringComparer.Ordinal));
            Assert.Equal(reads[4..], found.Skip(4));
        }
        else
        {
            Assert.Equal(reads, found);
        }

        Assert.Equal("secret.env\0", calls.Last(call => call.CommandText == CheckIgnore).StandardOutput);
        AssertNoWrite(calls);
        Assert.DoesNotContain(calls, call => call.CommandText.StartsWith("git fmt-merge-msg", StringComparison.Ordinal));
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Equal(Ignored.SecretLocal, Read(repo, "secret.env"));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal([Stash], (await repo.StashListAsync()).Select(stash => stash.Sha));
        Assert.Empty(app.Repository.UndoHistory.Entries);
        if (ahead is not null)
        {
            Assert.Equal(ahead, await repo.RefTargetAsync("refs/heads/ahead"));
        }
    }

    [Fact]
    public async Task A_list_of_picks_or_reverts_is_checked_as_git_gets_it_and_names_every_file_in_the_way()
    {
        // 9.5: one diff-tree --stdin for the whole list, then check-ignore: two reads. The picks of
        // "Add the vendor secret" and "Add the build file" write secret.env and a file build over
        // the ignored build/out.bin; the reverts, newest first, re-add secret.env (Stop tracking's
        // deletion reversed) and delete it (Track the secret's addition reversed).
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.CherryPickSeveralAsync([BuildFile, VendorSecret]);
        var picked = OperationCalls(app, mark);
        mark = app.Calls.Snapshot().Count;
        await app.Operations.RevertSeveralAsync([TrackTheSecret, StopTracking]);
        var reverted = OperationCalls(app, mark);

        Assert.Equal(
            [
                ("Cherry-pick failed", "2 ignored files are in the way of the cherry-pick: build/out.bin, secret.env. Move them away first."),
                ("Revert failed", "1 ignored file is in the way of the revert: secret.env. Move it away first."),
            ],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.Equal("Revert 2 commits failed", app.StatusText);
        Assert.Equal(["git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT", CheckIgnore], IgnoredReads(picked));
        Assert.Equal(["git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT -R", CheckIgnore], IgnoredReads(reverted));
        Assert.Equal("build/out.bin\0secret.env\0", picked.Last(call => call.CommandText == CheckIgnore).StandardOutput);
        AssertNoWrite(picked);
        AssertNoWrite(reverted);
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
    }

    [Fact]
    public async Task The_interactive_rebase_and_the_squash_are_refused_before_their_backup()
    {
        // 9.7: "Main work" moved before "Stop tracking the secret" (0.4's T7): no leading pick git
        // skips, so it would check out "Track the secret", writing secret.env over LOCAL (rev-list,
        // the two diff-trees, check-ignore: 4 reads). The squash of the two (its first group changes
        // the message, so nothing is skipped) starts at "Track the secret" too (3 reads).
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var list = await app.Repository.Repository.ReadRebaseListAsync(TrackTheSecret, "refs/heads/main", fromCommit: true, options: null, TestContext.Current.CancellationToken);
        Assert.Equal((TrackTheSecret, MainWork), (list.Onto, list.BranchTip));
        var request = new InteractiveRebaseRequest(new RebaseTarget(TrackTheSecret, "1ac759d"), "refs/heads/main", FromHere: true, CheckoutFirst: false);
        var plan = new RebasePlan(list.Onto, list.BranchTip, "refs/heads/main", [new RebaseStep(MainWork, RebaseAction.Pick, null), new RebaseStep(StopTracking, RebaseAction.Pick, null)]);
        app.Dialogs.CloseError().Answer<MessageDialogViewModel>(dialog => dialog.ConfirmCommand.Execute(null)).CloseError();
        var mark = app.Calls.Snapshot().Count;

        Assert.False(await app.Operations.RunInteractiveRebaseAsync(plan, request));
        var interactive = OperationCalls(app, mark);
        mark = app.Calls.Snapshot().Count;
        await app.Operations.SquashAsync([MainWork, StopTracking]);
        var squashed = OperationCalls(app, mark);

        Assert.Equal(
            [
                ("Interactive rebase failed", "1 ignored file is in the way of the rebase: secret.env. Move it away first."),
                ("Squash failed", "1 ignored file is in the way of the squash: secret.env. Move it away first."),
            ],
            app.Dialogs.Errors.Select(dialog => (dialog.Title, dialog.Summary)));
        Assert.Equal("Squash 2 commits failed", app.StatusText);
        // The rev-list first, the two diff-trees side by side (sorted here), then check-ignore.
        Assert.Equal(
            [
                "git rev-list --stdin --no-walk=unsorted --parents",
                "git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT",
                $"git diff-tree -r -z --no-renames --name-only --diff-filter=A {MainWork} {TrackTheSecret} --",
                CheckIgnore,
            ],
            IgnoredReadsInPhases(interactive, first: 1));
        Assert.Equal(
            [
                "git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT",
                $"git diff-tree -r -z --no-renames --name-only --diff-filter=A {MainWork} {TrackTheSecret} --",
                CheckIgnore,
            ],
            IgnoredReadsInPhases(squashed, first: 0));
        AssertNoWrite(interactive);
        AssertNoWrite(squashed);
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
        app.AssertScriptFollowed();
    }

    // ---- 9.1: the setting, read when the operation runs; off restores phase 5 (D221) -------------------

    [Fact]
    public async Task Off_a_merge_and_a_checkout_run_as_phase_5_ran_them_with_none_of_the_reads()
    {
        // G24 (D221): no --no-overwrite-ignore, none of 9.4's reads, no check-ignore in either
        // operation; git overwrites secret.env, and the checkout deletes the ignored folder build/.
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        TurnOff(app);
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.MergeAsync("refs/heads/vendor", MergeMode.Default);
        var merged = OperationCalls(app, mark);
        mark = app.Calls.Snapshot().Count;
        await app.Operations.CheckoutAsync("refs/heads/tools");
        var checkedOut = OperationCalls(app, mark);

        Assert.Empty(app.Dialogs.Shown);
        Assert.Contains("git merge --no-edit --no-log -m \"Merge branch 'vendor'\" refs/heads/vendor", merged.Select(call => call.CommandText));
        Assert.Contains("git switch --no-guess tools", checkedOut.Select(call => call.CommandText));
        Assert.Empty(IgnoredReads(merged));
        Assert.Empty(IgnoredReads(checkedOut));
        Assert.DoesNotContain(merged.Concat(checkedOut), call => call.CommandText.Contains("--no-overwrite-ignore", StringComparison.Ordinal));
        Assert.Equal("Checked out tools", app.StatusText);
        Assert.Equal("tools", await repo.CurrentBranchAsync());
        Assert.False(File.Exists(Path.Combine(repo.Path, "secret.env")), "The merge overwrote secret.env and the checkout of tools removed it.");
        // git's default deletes the ignored folder build/ for tools' file build.
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "build")), "The ignored folder build/ is still there.");
        Assert.Equal("build\n", Read(repo, "build"));
    }

    public static TheoryData<string> OperationsWithoutTheCheckWhenOff => ["rebase", "cherry-pick", "revert", "apply", "pop", "squash"];

    [Theory]
    [MemberData(nameof(OperationsWithoutTheCheckWhenOff))]
    public async Task Off_every_other_covered_operation_goes_to_git_without_the_check(string operation)
    {
        // D221: phase 5 exactly: git writes over the ignored secret.env (the report's scenario
        // facts 4.4) or deletes it (the squash's pick of "Stop tracking the secret").
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        TurnOff(app);
        if (operation == "squash")
        {
            app.Dialogs.Answer<MessageDialogViewModel>(dialog => dialog.ConfirmCommand.Execute(null));
        }

        var mark = app.Calls.Snapshot().Count;

        var (task, write) = operation switch
        {
            "rebase" => (app.Operations.RebaseAsync(new RebaseTarget(VendorSecret, "vendor")), $"git rebase {VendorSecret}"),
            "cherry-pick" => (app.Operations.CherryPickAsync(VendorSecret), $"git cherry-pick {VendorSecret}"),
            "revert" => (app.Operations.RevertAsync(StopTracking), "git revert"),
            "apply" => (app.Operations.ApplyStashAsync(Stash), "git stash apply"),
            "pop" => (app.Operations.PopStashAsync(Stash), "git stash pop"),
            _ => (app.Operations.SquashAsync([TrackTheSecret, StopTracking]), "git rebase"),
        };
        await task;

        var calls = OperationCalls(app, mark);
        Assert.Contains(calls, call => call.CommandText.StartsWith(write, StringComparison.Ordinal));
        Assert.Empty(IgnoredReads(calls));
        Assert.Empty(app.Dialogs.Errors);
        if (operation == "squash")
        {
            Assert.False(File.Exists(Path.Combine(repo.Path, "secret.env")), "git deleted the ignored secret.env.");
        }
        else
        {
            Assert.NotEqual(Ignored.SecretLocal, Read(repo, "secret.env"));
        }
    }

    public static TheoryData<string> MoreOperationsWithoutTheCheckWhenOff => ["fast-forward", "pick-list", "revert-list", "interactive", "check-out-and-merge", "check-out-and-rebase"];

    [Theory]
    [MemberData(nameof(MoreOperationsWithoutTheCheckWhenOff))]
    public async Task Off_the_lists_the_fast_forward_the_interactive_Start_and_the_check_out_and_operations_run_as_phase_5_ran_them(string operation)
    {
        // D221 (GD10): no --no-overwrite-ignore, none of the reads; git overwrites the ignored
        // secret.env (with vendor's, or the old tracked one) or deletes it after writing it.
        using var repo = await Scenarios.IgnoredAsync();
        var ahead = (await repo.GitAsync("commit-tree", VendorSecret + "^{tree}", "-p", MainWork, "-m", "Ahead")).StandardOutput.Trim();
        await repo.CreateBranchAsync("ahead", ahead);
        await repo.CreateBranchAsync("plain", StopTracking);
        using var app = await OperationsHarness.OpenAsync(repo);
        TurnOff(app);
        var mark = app.Calls.Snapshot().Count;

        string write;
        switch (operation)
        {
            case "fast-forward":
                await app.Operations.MergeAsync("refs/heads/ahead", MergeMode.FastForwardOnly);
                write = "git merge --ff-only refs/heads/ahead";
                break;
            case "pick-list":
                await app.Operations.CherryPickSeveralAsync([BuildFile, VendorSecret]);
                write = $"git cherry-pick {VendorSecret} {BuildFile}";
                break;
            case "revert-list":
                await app.Operations.RevertSeveralAsync([TrackTheSecret, StopTracking]);
                write = "git revert";
                break;
            case "interactive":
                var list = await app.Repository.Repository.ReadRebaseListAsync(TrackTheSecret, "refs/heads/main", fromCommit: true, options: null, TestContext.Current.CancellationToken);
                var request = new InteractiveRebaseRequest(new RebaseTarget(TrackTheSecret, "1ac759d"), "refs/heads/main", FromHere: true, CheckoutFirst: false);
                var plan = new RebasePlan(list.Onto, list.BranchTip, "refs/heads/main", [new RebaseStep(MainWork, RebaseAction.Pick, null), new RebaseStep(StopTracking, RebaseAction.Pick, null)]);
                Assert.True(await app.Operations.RunInteractiveRebaseAsync(plan, request));
                write = "git rebase";
                break;
            case "check-out-and-merge":
                await app.Operations.CheckoutAndMergeAsync("refs/heads/plain", "refs/heads/vendor");
                // git's message for a merge into plain (fmt-merge-msg's words, not asserted here).
                write = "git merge --no-edit --no-log -m \"Merge branch 'vendor'";
                Assert.Contains(OperationCalls(app, mark), call => call.CommandText.StartsWith(write, StringComparison.Ordinal) && call.CommandText.EndsWith("\" refs/heads/vendor", StringComparison.Ordinal));
                Assert.Contains("git switch --no-guess plain", OperationCalls(app, mark).Select(call => call.CommandText));
                break;
            default:
                await app.Operations.CheckoutAndRebaseAsync("refs/heads/plain", new RebaseTarget(VendorSecret, "vendor"));
                write = $"git rebase {VendorSecret}";
                Assert.Contains("git switch --no-guess plain", OperationCalls(app, mark).Select(call => call.CommandText));
                break;
        }

        var calls = OperationCalls(app, mark);
        Assert.Contains(calls, call => call.CommandText.StartsWith(write, StringComparison.Ordinal));
        Assert.Empty(IgnoredReads(calls));
        Assert.DoesNotContain(calls, call => call.CommandText.Contains("--no-overwrite-ignore", StringComparison.Ordinal));
        Assert.Empty(app.Dialogs.Errors);
        var secret = Path.Combine(repo.Path, "secret.env");
        Assert.True(!File.Exists(secret) || File.ReadAllText(secret) != Ignored.SecretLocal, "git kept the ignored secret.env.");
    }

    [Fact]
    public async Task Off_undo_keeps_D169s_guard_and_refuses_an_ignored_file_in_its_way()
    {
        // D221 (G24's "undo's guards kept"): with the setting off, vendor -> main runs as git runs
        // it; an ignored secret.env made afterwards is in the way of undo's switch back to vendor,
        // whose own --no-overwrite-ignore and check refuse it.
        using var repo = await Scenarios.IgnoredAsync();
        await repo.CheckoutAsync("vendor");
        using var app = await OperationsHarness.OpenAsync(repo);
        TurnOff(app);

        await app.Operations.CheckoutAsync("refs/heads/main");
        Assert.Equal("Checked out main", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git switch --no-guess main");
        Assert.Equal("checkout of main", Assert.Single(app.Repository.UndoHistory.Entries).Text);
        Assert.False(File.Exists(Path.Combine(repo.Path, "secret.env")), "The checkout of main removed vendor's secret.env.");
        repo.WriteFile("secret.env", Ignored.SecretLocal);
        await app.Repository.RefreshAsync();

        await app.Operations.UndoAsync();

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Undo failed", "Can't undo checkout of main: 1 ignored file is in the way: secret.env. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.Equal("Undo checkout of main failed", app.StatusText);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(Ignored.SecretLocal, Read(repo, "secret.env"));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText == "git switch --no-guess --no-overwrite-ignore vendor" && call.Succeeded);
    }

    [Fact]
    public async Task Off_the_automatic_stashs_checkout_of_an_older_commit_keeps_D179s_option_and_words()
    {
        // S21 step 3 (9.1, D221): "Track the secret" is reached from main, so the automatic stash's
        // path, with nothing to stash (an ignored file is no change); then S21 step 4: vendor
        // checked out as git checks it out, secret.env overwritten without a word.
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        TurnOff(app);
        var before = await repo.ExactStateAsync();
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.CheckoutAsync(TrackTheSecret);

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "1 ignored file is in the way: 1ac759d tracks a file of the same name. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.False(dialog.HasExtraAction);
        AssertOutputOfFailedCall(app, dialog, "git switch --detach --no-overwrite-ignore " + TrackTheSecret);
        var calls = OperationCalls(app, mark);
        Assert.DoesNotContain(calls, call => call.CommandText.StartsWith("git stash push", StringComparison.Ordinal));
        Assert.Equal("secret.env\0", Assert.Single(calls, call => call.CommandText == CheckIgnore).StandardOutput);
        Assert.Equal("Check out 1ac759d failed", app.StatusText);
        Assert.Equal(before, await repo.ExactStateAsync());

        mark = app.Calls.Snapshot().Count;
        await app.Operations.CheckoutAsync("refs/heads/vendor");

        calls = OperationCalls(app, mark);
        Assert.Contains("git switch --no-guess vendor", calls.Select(call => call.CommandText));
        Assert.DoesNotContain(calls, call => call.CommandText == CheckIgnore);
        Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Checked out vendor", app.StatusText);
        Assert.Equal(Ignored.SecretVendor, Read(repo, "secret.env"));
        Assert.Equal("checkout of vendor", Assert.Single(app.Repository.UndoHistory.Entries).Text);
    }

    [Fact]
    public async Task Off_a_checkout_refused_for_an_untracked_file_asks_nothing_about_ignored_files()
    {
        // D221: no check-ignore after a refused checkout while the setting is off (phase 5's
        // display for an untracked file in the way). secret.env is not ignored in this copy (the
        // changed .gitignore is carried along: vendor's is main's).
        using var repo = await Scenarios.IgnoredAsync();
        repo.WriteFile(".gitignore", "build/\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        TurnOff(app);
        var mark = app.Calls.Snapshot().Count;

        await app.Operations.CheckoutAsync("refs/heads/vendor");

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Checkout blocked", "1 conflict prevents checkout. Commit, stash, or discard your changes first."), (dialog.Title, dialog.Summary));
        AssertOutputOfFailedCall(app, dialog, "git switch --no-guess vendor");
        Assert.DoesNotContain(OperationCalls(app, mark), call => call.CommandText == CheckIgnore);
    }

    [Fact]
    public async Task The_setting_is_read_when_the_operation_runs_not_when_it_was_chosen()
    {
        // 9.1 (D124): off when the merge was chosen, on by the time it runs: refused with the check.
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        TurnOff(app);
        var (hold, ahead) = HoldQueue(app, () =>
        {
            app.Settings.Update(settings => settings with { KeepIgnoredFiles = true });
            return Task.CompletedTask;
        });
        var merge = app.Operations.MergeAsync("refs/heads/vendor", MergeMode.Default);
        hold.SetResult();
        await ahead;
        await merge;

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Merge failed", "1 ignored file is in the way of the merge: secret.env. Move it away first."), (dialog.Title, dialog.Summary));
        Assert.Equal(Ignored.SecretLocal, Read(repo, "secret.env"));
        Assert.Equal(MainWork, await repo.HeadAsync());
    }

    [Fact]
    public async Task A_read_that_fails_is_logged_and_the_operation_goes_on_to_git()
    {
        // 9.3: as D179's count, a failed read leaves the operation to git (here git merges).
        using var repo = await Scenarios.IgnoredAsync();
        using var app = await OperationsHarness.OpenWrappedAsync(repo, FailingIgnoredReads.Wrap);

        await app.Operations.MergeAsync("refs/heads/vendor", MergeMode.Default);

        Assert.Empty(app.Dialogs.Errors);
        Assert.Equal("Merged vendor into main", app.StatusText);
        Assert.Contains(app.Log.Messages, message => message.Contains("the ignored files in its way could not be read", StringComparison.Ordinal));
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git merge --no-edit --no-log -m \"Merge branch 'vendor'\" --no-overwrite-ignore refs/heads/vendor");
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static void TurnOff(OperationsHarness app) => app.Settings.Update(settings => settings with { KeepIgnoredFiles = false });

    /// <summary>The calls operations made since <paramref name="mark"/> (the watcher's own check-ignore runs outside every operation).</summary>
    private static List<GitCallRecord> OperationCalls(OperationsHarness app, int mark) =>
        [.. app.Calls.Snapshot().Skip(mark).Where(call => call.OperationId is not null)];

    /// <summary>9.3 to 9.8's reads among <paramref name="calls"/>, in the order they started.</summary>
    private static List<string> IgnoredReads(IEnumerable<GitCallRecord> calls) =>
        [.. calls.Select(call => call.CommandText).Where(IsIgnoredRead)];

    /// <summary>9.7's reads as they run: the first <paramref name="first"/> (the rev-list), the reads side by side between them and check-ignore (sorted), then check-ignore.</summary>
    private static List<string> IgnoredReadsInPhases(IEnumerable<GitCallRecord> calls, int first)
    {
        var reads = IgnoredReads(calls);
        return [.. reads[..first], .. reads[first..^1].Order(StringComparer.Ordinal), reads[^1]];
    }

    private static bool IsIgnoredRead(string command) =>
        command == CheckIgnore
        || command.StartsWith("git diff-tree --stdin", StringComparison.Ordinal)
        || command.StartsWith("git diff-tree -r -z --no-renames --name-only --diff-filter=", StringComparison.Ordinal)
        || command.StartsWith("git merge-base --all", StringComparison.Ordinal)
        || command.StartsWith("git merge-base --is-ancestor", StringComparison.Ordinal)
        || command.StartsWith("git log --no-renames --name-only -z --root --no-merges", StringComparison.Ordinal)
        || command.StartsWith("git rev-list --min-parents=2", StringComparison.Ordinal)
        || command.StartsWith("git rev-list --topo-order --boundary", StringComparison.Ordinal)
        || command.StartsWith("git rev-list --stdin --no-walk=unsorted", StringComparison.Ordinal)
        || command.StartsWith("git ls-tree -r -z --name-only", StringComparison.Ordinal);

    /// <summary>The report's "no write": no git call that writes the working tree, the index or a ref.</summary>
    private static void AssertNoWrite(IEnumerable<GitCallRecord> calls)
    {
        string[] writes = ["git merge ", "git rebase", "git cherry-pick", "git revert", "git stash apply", "git stash pop", "git stash push", "git switch", "git reset", "git commit", "git branch ", "git update-ref", "git push"];
        Assert.DoesNotContain(calls, call => writes.Any(write => call.CommandText.StartsWith(write, StringComparison.Ordinal)));
    }

    /// <summary>A repository whose five ignored-files reads fail as git fails on a bad revision.</summary>
    public class FailingIgnoredReads : DispatchProxy
    {
        private IGitRepository _inner = null!;

        public static IGitRepository Wrap(IGitRepository inner)
        {
            var proxy = Create<IGitRepository, FailingIgnoredReads>();
            ((FailingIgnoredReads)(object)proxy)._inner = inner;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name.StartsWith("FindIgnoredInTheWayOf", StringComparison.Ordinal))
            {
                return Task.FromException<IReadOnlyList<string>>(new GitException("git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/heads/vendor --", 128, "fatal: bad revision\n"));
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
}
