using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
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
/// shows nothing on screen and logs a Warning.
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
        var old = OldBackupName("reset", days: 40);
        await repo.GitAsync("update-ref", old, TopicOne);
        repo.WriteFile("README.md", "# Changed here\n");
        var gitLog = new RecordingLog();
        CleanUpSpy? spy = null;
        using var app = await OperationsHarness.OpenWrappedAsync(repo, _ => CleanUpSpy.Wrap(OpenLogged(repo, gitLog), failure: null, out spy));

        // The discard's snapshot is the restore bar's (D75): the clean-up must keep it.
        app.Dialogs.Confirm();
        await app.Operations.DiscardFilesAsync([app.Operations.Status.Unstaged.Single(file => file.Path == "README.md")]);
        var snapshot = Assert.Single(await repo.BackupRefsAsync(), entry => entry.Name != old).Name;
        Assert.StartsWith("refs/visualcommit/backup/discard-", snapshot, StringComparison.Ordinal);
        Assert.Empty(spy!.Calls);

        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        // One clean-up, at the end of the rebase, with the default retention and the snapshot to keep.
        var call = Assert.Single(spy.Calls);
        Assert.Equal(BackupRetention.Default, call.Retention);
        Assert.Equal(snapshot, Assert.Single(call.Keep));
        var names = (await repo.BackupRefsAsync()).Select(entry => entry.Name).ToList();
        Assert.DoesNotContain(old, names);
        Assert.Contains(snapshot, names);
        Assert.Contains(names, name => name.StartsWith("refs/visualcommit/backup/rebase-", StringComparison.Ordinal));
        Assert.Contains(gitLog.Messages, message => message.EndsWith($": Removed backup {old["refs/visualcommit/backup/".Length..]} (older than 30 days)", StringComparison.Ordinal));
        Assert.Equal("Rebased feature/topic onto main", app.StatusText);
        app.AssertScriptFollowed();

        // An operation that saved no backup does not clean up.
        await app.Operations.CherryPickAsync(OtherOne);
        Assert.Single(spy.Calls);
    }

    [Fact]
    public async Task A_clean_up_that_fails_shows_nothing_and_logs_a_warning()
    {
        using var repo = await Scenarios.RebaseAsync();
        var old = OldBackupName("reset", days: 40);
        await repo.GitAsync("update-ref", old, TopicOne);
        CleanUpSpy? spy = null;
        using var app = await OperationsHarness.OpenWrappedAsync(repo, inner => CleanUpSpy.Wrap(inner, new IOException("The backups could not be read."), out spy));

        await app.Operations.RebaseAsync(new RebaseTarget(MainMore, "main"));

        // The rebase's own result is what shows: no dialog, its status in the secondary colour.
        Assert.Single(spy!.Calls);
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
    /// failure, the clean-up throws it instead of running.
    /// </summary>
    public class CleanUpSpy : DispatchProxy
    {
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
