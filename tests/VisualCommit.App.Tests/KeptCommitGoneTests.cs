using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.UndoAppTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// Design 5.2's check that a reflog row's or a backup's commit still exists (<c>git cat-file -e
/// &lt;sha&gt;^{commit}</c>), since a <c>gc</c> may have pruned it after the page read it: the
/// reflog page's and the backups list's "Create branch here…", "Check out this commit", "Reset
/// {cur} to this commit…" and "Cherry-pick onto {cur}" refuse with "The commit {short} no longer
/// exists." inside their operation (and before a question about it), and the backups' own restores
/// with 7.6's "the commits no longer exist"; each changes nothing. Against the "reflog" scenario,
/// with commits pruned by <c>git reflog expire --expire-unreachable=now --all</c> and <c>git gc
/// --prune=now</c>, as an outside gc would.
/// </summary>
[Collection(UndoCollection)]
public class KeptCommitGoneTests
{
    // "Draft the notes": kept by HEAD@{11} and main@{3} alone, so the expiry and the gc prune it.
    private const string DraftTheNotes = "c7bf50881e2d9ebc82c5adf905a4bb441b0664bb";

    // "Try a shortcut": lost too, but kept by the backup reset-20260101T122401000Z.
    private const string TryAShortcut = "d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f";

    // main's commit, "Write the notes".
    private const string WriteTheNotes = "9dd61fde06301292b9b14d0911560b6d987f645d";

    private const string Gone = "The commit c7bf508 no longer exists.";

    [Fact]
    public async Task A_reflog_rows_commit_pruned_by_a_gc_is_refused_by_each_of_its_operations_and_nothing_changes()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);
        await PruneAsync(app, repo);
        Assert.False(await app.Repository.Repository.CommitExistsAsync(DraftTheNotes, TestContext.Current.CancellationToken));
        var picture = await PictureAsync(repo);
        var backups = await repo.BackupRefsAsync();
        var calls = app.Calls.Snapshot().Count;

        // Create branch here…: the name dialog names the commit by its short SHA (its subject cannot
        // be read), then the operation refuses before git creates anything.
        app.Dialogs.EnterName("drafts");
        await app.Operations.CreateBranchAtKeptCommitAsync(DraftTheNotes);
        Assert.Equal("From c7bf508", Assert.Single(app.Dialogs.Shown.OfType<NameDialogViewModel>()).Context);
        AssertRefused(app, "Creating the branch failed", "Create branch drafts failed");

        // Check out this commit and Reset main to this commit…: refused before the dialog or the
        // automatic stash's choice reads the commit.
        await app.Operations.CheckoutKeptCommitAsync(DraftTheNotes);
        AssertRefused(app, "Checkout failed", "Check out c7bf508 failed");
        await app.Operations.ResetToKeptCommitAsync(DraftTheNotes);
        Assert.Empty(app.Dialogs.Shown.OfType<ResetDialogViewModel>());
        AssertRefused(app, "Reset failed", "Reset main failed");

        // Cherry-pick onto main: refused inside its operation.
        await app.Operations.CherryPickKeptCommitAsync(DraftTheNotes);
        AssertRefused(app, "Cherry-pick failed", "Cherry-pick c7bf508 failed");

        // Each asked git whether the commit exists, and none wrote anything.
        var made = app.Calls.Snapshot().Skip(calls).Select(call => call.CommandText).ToList();
        Assert.Equal(4, made.Count(command => command == $"git cat-file -e {DraftTheNotes}^{{commit}}"));
        Assert.DoesNotContain(made, command => command.StartsWith("git branch", StringComparison.Ordinal)
            || command.StartsWith("git switch", StringComparison.Ordinal)
            || command.StartsWith("git reset", StringComparison.Ordinal)
            || command.StartsWith("git cherry-pick", StringComparison.Ordinal)
            || command.StartsWith("git stash push", StringComparison.Ordinal)
            || command.StartsWith("git update-ref", StringComparison.Ordinal));
        Assert.Equal(picture, await PictureAsync(repo));
        Assert.Equal(backups, await repo.BackupRefsAsync());
        Assert.Empty(app.Repository.UndoHistory.Entries);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_check_inside_the_operation_refuses_a_commit_pruned_after_the_click()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);
        var head = await repo.HeadAsync();
        var backups = await repo.BackupRefsAsync();

        // The reset dialog asked about a commit that existed; a gc pruned it before Reset.
        app.Dialogs.Keep();
        var reset = app.Operations.ResetToKeptCommitAsync(DraftTheNotes);
        var dialog = await app.Dialogs.WaitForAsync<ResetDialogViewModel>();
        await PruneAsync(app, repo);
        dialog.Mode = ResetMode.Hard;
        dialog.ConfirmCommand.Execute(null);
        await reset;
        AssertRefused(app, "Reset failed", "Reset main failed");
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(backups, await repo.BackupRefsAsync());
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git reset", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_checkout_checks_inside_its_operation_and_inside_the_automatic_stashs()
    {
        // HEAD detached at main's commit: no automatic stash, no question about detached commits.
        using (var repo = await Scenarios.ReflogAsync())
        {
            await repo.GitAsync("switch", "--detach", "main");
            ScriptedExists? exists = null;
            using var app = await OpenWithBackupClockAsync(repo, git => (exists = ScriptedExists.Wrap(git, true)).Repository);
            await PruneAsync(app, repo);

            // The check before the operation saw the commit; the one inside it finds it gone.
            await app.Operations.CheckoutKeptCommitAsync(DraftTheNotes);
            AssertRefused(app, "Checkout failed", "Check out c7bf508 failed");
            Assert.Equal(0, exists!.Left);
            Assert.Equal(WriteTheNotes, await repo.HeadAsync());
            Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git switch", StringComparison.Ordinal));
            app.AssertScriptFollowed();
        }

        // HEAD on main, a commit main reaches, a local change: the automatic stash's operation
        // checks before it stashes (the answer scripted: a reachable commit cannot be pruned).
        using (var repo = await Scenarios.ReflogAsync())
        {
            repo.WriteFile("README.md", "# Reflog\n\nA local change.\n");
            ScriptedExists? exists = null;
            using var app = await OpenWithBackupClockAsync(repo, git => (exists = ScriptedExists.Wrap(git, true, false)).Repository);
            var picture = await PictureAsync(repo);
            const string ClashNotes = "316166adc8e550ebb513a7f8c0ea99ecbdd88983";
            await app.Operations.CheckoutKeptCommitAsync(ClashNotes);
            Assert.Equal(("Checkout failed", "The commit 316166a no longer exists."), (app.Dialogs.Errors[^1].Title, app.Dialogs.Errors[^1].Summary));
            Assert.Equal("Check out 316166a failed", app.StatusText);
            Assert.Equal(0, exists!.Left);
            Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git stash push", StringComparison.Ordinal) || call.CommandText.StartsWith("git switch", StringComparison.Ordinal));
            Assert.Equal(picture, await PictureAsync(repo));
            app.AssertScriptFollowed();
        }
    }

    [Fact]
    public async Task A_commit_that_still_exists_passes_the_check()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);
        var calls = app.Calls.Snapshot().Count;

        // Report B8 step 7: the branch made from HEAD@{9} without checking it out.
        app.Dialogs.EnterName("shortcut", option: false);
        await app.Operations.CreateBranchAtKeptCommitAsync(TryAShortcut);
        Assert.Equal("Created shortcut", app.StatusText);
        Assert.Equal(TryAShortcut, await repo.RefTargetAsync("refs/heads/shortcut"));
        var made = app.Calls.Snapshot().Skip(calls).Select(call => call.CommandText).ToList();
        var check = made.IndexOf($"git cat-file -e {TryAShortcut}^{{commit}}");
        Assert.True(check >= 0 && check < made.IndexOf($"git branch shortcut {TryAShortcut}"), string.Join(" || ", made));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_backup_whose_commit_was_pruned_is_refused_by_its_restore_with_the_commits_no_longer_exist()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = await OpenWithBackupClockAsync(repo);
        var branch = await BackupAsync(app, "branch");
        var discard = await BackupAsync(app, "discard");
        var mixed = await BackupAsync(app, "reset", "VisualCommit: index before a mixed reset");

        // The backups go (the clean-up, or outside the app) and a gc prunes what only they kept.
        foreach (var entry in new[] { branch, discard, mixed }.SelectMany(backup => backup.Refs))
        {
            await repo.GitAsync("update-ref", "-d", entry.FullName);
        }

        await PruneAsync(app, repo);
        foreach (var commit in new[] { branch.Main!.Commit, discard.Main!.Target, mixed.Refs.Single(entry => entry.IsChanges).Target })
        {
            Assert.False(await app.Repository.Repository.CommitExistsAsync(commit, TestContext.Current.CancellationToken), commit);
        }

        var picture = await PictureAsync(repo);
        var calls = app.Calls.Snapshot().Count;

        // The branch's restore: the step's own check of the objects it writes.
        await app.Operations.RestoreBackupAsync(branch);
        Assert.Equal(("Restoring the branch failed", "Can't restore branch doomed: the commits no longer exist"), (app.Dialogs.Errors[^1].Title, app.Dialogs.Errors[^1].Summary));

        // The files' and the staged changes' restores: before their snapshot is read, with no question.
        await app.Operations.RestoreBackupAsync(discard);
        Assert.Equal("Can't restore the discarded files: the commits no longer exist", app.Dialogs.Errors[^1].Summary);
        await app.Operations.RestoreBackupAsync(mixed);
        Assert.Equal("Can't restore the staged changes: the commits no longer exist", app.Dialogs.Errors[^1].Summary);

        Assert.Equal(3, app.Dialogs.Errors.Count);
        Assert.All(app.Dialogs.Errors, error => Assert.Null(error.Output));
        Assert.Empty(app.Dialogs.Confirmations);
        Assert.DoesNotContain(app.Calls.Snapshot().Skip(calls), call => call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal)
            || call.CommandText.StartsWith("git restore", StringComparison.Ordinal)
            || call.CommandText.StartsWith("git read-tree", StringComparison.Ordinal));
        Assert.Equal(picture, await PictureAsync(repo));
        Assert.Empty(app.Repository.UndoHistory.Entries);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_push_back_asks_nothing_about_a_commit_that_is_gone()
    {
        // A remote branch's backup keeps a commit main reaches, which no gc prunes: the check's answer is scripted.
        using var repo = await Scenarios.ReflogAsync();
        ScriptedExists? exists = null;
        using var app = await OpenWithBackupClockAsync(repo, git => (exists = ScriptedExists.Wrap(git, false)).Repository);
        await app.Operations.RestoreBackupAsync(await BackupAsync(app, "remote-branch"));
        Assert.Equal(("Push failed", "Can't push feature/gone back to origin: the commits no longer exist"), (app.Dialogs.Errors[^1].Title, app.Dialogs.Errors[^1].Summary));
        Assert.Empty(app.Dialogs.Confirmations);
        Assert.Equal(0, exists!.Left);
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    /// <summary>The last error dialog says the commit is gone, without git's output; the status bar says the operation failed.</summary>
    private static void AssertRefused(OperationsHarness app, string title, string status)
    {
        var error = app.Dialogs.Errors[^1];
        Assert.Equal((title, Gone, null, null), (error.Title, error.Summary, error.Output, error.ExtraAction));
        Assert.Equal((status, OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
    }

    /// <summary>
    /// What an outside gc does to what only reflogs keep: every reflog entry of a commit its ref no
    /// longer reaches expires, and the unreachable objects are pruned; then the app's refresh.
    /// </summary>
    private static async Task PruneAsync(OperationsHarness app, TempRepo repo)
    {
        await repo.GitAsync("reflog", "expire", "--expire-unreachable=now", "--all");
        await repo.GitAsync("gc", "--prune=now", "--quiet");
        await app.Repository.RefreshAsync();
        await app.WaitForIdleAsync();
    }

    /// <summary>
    /// The real repository, except that its first answers of <see cref="IGitRepository.CommitExistsAsync"/>
    /// are the scripted ones (a commit seen before a gc pruned it, or gone where no gc can prune
    /// it); every later one and every other call is the real repository's.
    /// </summary>
    internal class ScriptedExists : DispatchProxy
    {
        private readonly Queue<bool> _answers = new();
        private IGitRepository _inner = null!;

        /// <summary>The scripted answers not given yet.</summary>
        public int Left
        {
            get
            {
                lock (_answers)
                {
                    return _answers.Count;
                }
            }
        }

        public IGitRepository Repository => (IGitRepository)(object)this;

        public static ScriptedExists Wrap(IGitRepository inner, params bool[] answers)
        {
            var proxy = Create<IGitRepository, ScriptedExists>();
            var scripted = (ScriptedExists)(object)proxy;
            scripted._inner = inner;
            foreach (var answer in answers)
            {
                scripted._answers.Enqueue(answer);
            }

            return scripted;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IGitRepository.CommitExistsAsync))
            {
                lock (_answers)
                {
                    if (_answers.TryDequeue(out var answer))
                    {
                        return Task.FromResult(answer);
                    }
                }
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
