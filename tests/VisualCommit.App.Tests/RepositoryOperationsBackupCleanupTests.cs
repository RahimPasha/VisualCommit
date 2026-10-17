using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP4: the backups' clean-up after an operation (docs/phase-4-design.md section 9, D139) through
/// <see cref="RepositoryOperations"/> against real git: it runs at the end of an operation that
/// saved a backup, with the default retention and the restore bar's snapshot to keep
/// (<see cref="ViewModels.RepositoryViewModel.BackupsToKeep"/>); it removes the old backups, which
/// the git layer logs; an operation that saved none does not run it; and a clean-up that fails
/// shows nothing on screen and logs a Warning. Each test makes its old backup only once the tab's
/// clean-up at opening (section 9 (b)) has looked and found nothing to remove
/// (<see cref="CleanUpSpy.OpeningReadAsync"/>): with an old backup already there, that background
/// clean-up races the test's first operation for the queue, and where it wins (a machine that
/// starts git quickly: CI's bare container) it is a clean-up of its own, keeping nothing, before
/// the operation's.
/// </summary>
[Collection(RepositoryOperationsTestSupport.RebaseCollection)]
public class RepositoryOperationsBackupCleanupTests
{
    // The "rebase" scenario's commits, as Phase4ScenarioTests pins them.
    private const string TopicOne = "1219e45225ea55ad2c7a0e9412adaeeffd07a43c";
    private const string MainMore = "1dd58b8cfc17d4d9b80550eddd80b11ebf94c146";
    private const string OtherOne = "2d4d79a03112c8a5e2f9a783318d588df519af5d";

    private static readonly string GitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    [Fact]
    public async Task After_a_backup_the_clean_up_removes_old_backups_logs_them_and_keeps_the_restore_bars_snapshot()
    {
        using var repo = await Scenarios.RebaseAsync();
        repo.WriteFile("README.md", "# Changed here\n");
        var gitLog = new RecordingLog();
        CleanUpSpy? spy = null;
        using var app = await OperationsHarness.OpenWrappedAsync(repo, _ => CleanUpSpy.Wrap(OpenLogged(repo, gitLog), failure: null, out spy));
        Assert.Empty(await spy!.OpeningReadAsync());

        // The discard's snapshot is the restore bar's (D75): the clean-up must keep it. The
        // discard cleans up after it too (the review's finding 22; its own test is below).
        app.Dialogs.Confirm();
        await app.Operations.DiscardFilesAsync([app.Operations.Status.Unstaged.Single(file => file.Path == "README.md")]);
        var snapshot = Assert.Single(await repo.BackupRefsAsync()).Name;
        Assert.StartsWith("refs/visualcommit/backup/discard-", snapshot, StringComparison.Ordinal);
        Assert.Single(spy!.Calls);
        var old = OldBackupName("reset", days: 40);
        await repo.GitAsync("update-ref", old, TopicOne);

        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        // One more clean-up, at the end of the rebase, with the default retention and the backups to
        // keep: the restore bar's snapshot, and those the undo history's entries need
        // (docs/phase-5-design.md 7.8, D171): the discard's (the same snapshot) and the rebase's own,
        // whose entry is recorded before the clean-up.
        Assert.Equal(2, spy.Calls.Count);
        var call = spy.Calls[1];
        Assert.Equal(BackupRetention.Default, call.Retention);
        var names = (await repo.BackupRefsAsync()).Select(entry => entry.Name).ToList();
        var rebase = Assert.Single(names, name => name.StartsWith("refs/visualcommit/backup/rebase-", StringComparison.Ordinal));
        Assert.Equal(new[] { snapshot, rebase }.Order(StringComparer.Ordinal), call.Keep.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(old, names);
        Assert.Contains(snapshot, names);
        Assert.Contains(names, name => name.StartsWith("refs/visualcommit/backup/rebase-", StringComparison.Ordinal));
        Assert.Contains(gitLog.Messages, message => message.EndsWith($": Removed backup {old["refs/visualcommit/backup/".Length..]} (older than 30 days)", StringComparison.Ordinal));
        Assert.Equal("Rebased feature/topic onto main", app.StatusText);
        app.AssertScriptFollowed();

        // An operation that saved no backup does not clean up.
        await app.Operations.CherryPickAsync(OtherOne);
        Assert.Equal(2, spy.Calls.Count);
    }

    [Fact]
    public async Task A_discard_and_a_line_discard_clean_up_after_their_snapshot_and_keep_it_for_the_restore_bar()
    {
        // The review's finding 22: D67's discard snapshots are backups (section 9), and the
        // stage panel's discards saved them outside RunAsync, so no clean-up ever followed.
        using var repo = await Scenarios.RebaseAsync();
        repo.WriteFile("README.md", "# Changed here\n");
        CleanUpSpy? spy = null;
        using var app = await OperationsHarness.OpenWrappedAsync(repo, inner => CleanUpSpy.Wrap(inner, failure: null, out spy));
        Assert.Empty(await spy!.OpeningReadAsync());
        var old = OldBackupName("reset", days: 40);
        await repo.GitAsync("update-ref", old, TopicOne);
        app.Dialogs.Confirm();

        await app.Operations.DiscardFilesAsync([app.Operations.Status.Unstaged.Single(file => file.Path == "README.md")]);

        // The discard's own clean-up, the only one: it kept its snapshot and removed the old backup.
        var snapshot = Assert.Single(await repo.BackupRefsAsync()).Name;
        Assert.StartsWith("refs/visualcommit/backup/discard-", snapshot, StringComparison.Ordinal);
        var call = Assert.Single(spy.Calls);
        Assert.Equal((BackupRetention.Default, snapshot), (call.Retention, Assert.Single(call.Keep)));

        // A line discard (the whole hunk): its own snapshot is kept, and the discard's stays too
        // while its undo entry needs it (docs/phase-5-design.md 7.8, D171).
        var readme = await File.ReadAllTextAsync(Path.Combine(repo.Path, "README.md"), TestContext.Current.CancellationToken);
        repo.WriteFile("README.md", readme + "a line to discard\n");
        await app.Repository.RefreshAsync();
        var target = new DiffTarget(DiffSide.Unstaged, "README.md", FileChangeKind.Modified);
        var diff = await app.Repository.ReadDiffAsync(target, TestContext.Current.CancellationToken);
        var added = diff.Hunks[0].Lines.Select((line, index) => (line, index)).Where(pair => pair.line.IsChange).Select(pair => new DiffLineRef(0, pair.index)).ToHashSet();
        app.Dialogs.Confirm();

        Assert.Null(await app.Repository.ApplyLinesAsync(target, diff, added, LineAction.Discard, wholeHunk: true));

        Assert.Equal(readme, await File.ReadAllTextAsync(Path.Combine(repo.Path, "README.md"), TestContext.Current.CancellationToken));
        Assert.Equal(2, spy.Calls.Count);
        var lineSnapshot = Assert.Single(spy.Calls[1].Keep, name => name != snapshot);
        Assert.Contains(snapshot, spy.Calls[1].Keep);
        Assert.Contains(lineSnapshot, (await repo.BackupRefsAsync()).Select(entry => entry.Name));
    }

    [Fact]
    public async Task A_clean_up_that_fails_shows_nothing_and_logs_a_warning()
    {
        using var repo = await Scenarios.RebaseAsync();
        CleanUpSpy? spy = null;
        using var app = await OperationsHarness.OpenWrappedAsync(repo, inner => CleanUpSpy.Wrap(inner, new IOException("The backups could not be read."), out spy));
        Assert.Empty(await spy!.OpeningReadAsync());
        var old = OldBackupName("reset", days: 40);
        await repo.GitAsync("update-ref", old, TopicOne);

        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        // The rebase's own result is what shows: no dialog, its status in the secondary colour.
        Assert.Single(spy.Calls);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(("Rebased feature/topic onto main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(OperationOutcome.Succeeded, app.LastResult?.Outcome);
        var warning = Assert.Single(app.Log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("clean-up", StringComparison.Ordinal));
        Assert.EndsWith(": the backups' clean-up failed; the backups stay.", warning.Message, StringComparison.Ordinal);
        Assert.IsType<IOException>(warning.Exception);
        Assert.Contains(old, (await repo.BackupRefsAsync()).Select(entry => entry.Name));
    }

    /// <summary>A backup ref named as made <paramref name="days"/> days ago, as the app names them (section 9).</summary>
    private static string OldBackupName(string kind, int days) =>
        $"refs/visualcommit/backup/{kind}-{DateTimeOffset.UtcNow.AddDays(-days).ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture)}";

    /// <summary>The repository as the harness opens it, but with the git layer's own log lines going to <paramref name="log"/>.</summary>
    private static IGitRepository OpenLogged(TempRepo repo, IAppLog log) =>
        GitRepository.OpenAsync(new GitRunner(GitPath), repo.Path, TestContext.Current.CancellationToken, log).GetAwaiter().GetResult();

    /// <summary>
    /// Passes every call to the repository it wraps and records each
    /// <see cref="IGitRepository.CleanUpBackupsAsync"/>'s retention and refs to keep; with a
    /// failure, the clean-up throws it instead of running. It also keeps the first
    /// <see cref="IGitRepository.FindBackupsToRemoveAsync"/>, the read of the tab's clean-up at
    /// opening (<see cref="OpeningReadAsync"/>).
    /// </summary>
    public class CleanUpSpy : DispatchProxy
    {
        /// <summary>How long <see cref="OpeningReadAsync"/> waits for the read to come and to end.</summary>
        private static readonly TimeSpan OpeningReadTimeout = TimeSpan.FromSeconds(60);

        private readonly TaskCompletionSource<Task<IReadOnlyList<string>>> _openingRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IGitRepository _inner = null!;
        private Exception? _failure;

        public List<(BackupRetention Retention, IReadOnlySet<string> Keep)> Calls { get; } = [];

        public static IGitRepository Wrap(IGitRepository inner, Exception? failure, out CleanUpSpy spy)
        {
            var proxy = Create<IGitRepository, CleanUpSpy>();
            spy = (CleanUpSpy)(object)proxy;
            spy._inner = inner;
            spy._failure = failure;
            return proxy;
        }

        /// <summary>
        /// The backups the tab's clean-up at opening found to remove (docs/phase-4-design.md
        /// section 9 (b)): the first <see cref="IGitRepository.FindBackupsToRemoveAsync"/>, which
        /// the tab makes once its first load is done, waited for until it has ended. Only when it
        /// found some does that clean-up start, so once it returned none, none runs.
        /// </summary>
        public async Task<IReadOnlyList<string>> OpeningReadAsync()
        {
            var read = await _openingRead.Task.WaitAsync(OpeningReadTimeout, TestContext.Current.CancellationToken);
            return await read.WaitAsync(OpeningReadTimeout, TestContext.Current.CancellationToken);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IGitRepository.CleanUpBackupsAsync))
            {
                lock (Calls)
                {
                    Calls.Add(((BackupRetention)args![0]!, new HashSet<string>((IReadOnlySet<string>)args[1]!, StringComparer.Ordinal)));
                }

                if (_failure is not null)
                {
                    return Task.FromException<IReadOnlyList<RemovedBackup>>(_failure);
                }
            }

            if (targetMethod.Name == nameof(IGitRepository.FindBackupsToRemoveAsync))
            {
                var read = (Task<IReadOnlyList<string>>)Forward(targetMethod, args)!;
                _openingRead.TrySetResult(read);
                return read;
            }

            return Forward(targetMethod, args);
        }

        private object? Forward(MethodInfo targetMethod, object?[]? args)
        {
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
