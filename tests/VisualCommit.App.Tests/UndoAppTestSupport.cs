using System.Security.Cryptography;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the App tests of the undo history share (docs/phase-5-design.md 7.11): their collection,
/// the toolbar's states read from the operations, a reflog's newest entry, a picture of a
/// repository that a step must leave as it was (HEAD, the refs but the backups, the index's
/// entries, the status, every file's bytes, the stash list and the repository's own settings),
/// and for the backups list's tests a start with the fixed backup clock and a backup by its kind.
/// </summary>
internal static class UndoAppTestSupport
{
    /// <summary>
    /// The undo tests' collection: its classes one after another, beside the operations' four
    /// collections (<see cref="RepositoryOperationsTestSupport"/>), whose reason holds here too:
    /// every test runs git many times.
    /// </summary>
    public const string UndoCollection = "Undo: recording, steps, the toolbar and the restores";

    /// <summary>The toolbar's Undo and Redo as the operations give them now (7.4).</summary>
    public static void AssertStates(OperationsHarness app, UndoAvailability expected) =>
        Assert.Equal(expected, app.Operations.ReadUndoAvailability());

    /// <summary>The message of the newest entry of <paramref name="fullRef"/>'s reflog (<c>%gs</c>).</summary>
    public static async Task<string> NewestReflogAsync(TempRepo repo, string fullRef) =>
        (await repo.GitAsync("log", "-g", "-n", "1", "--format=%gs", fullRef, "--")).StandardOutput.Trim();

    /// <summary>
    /// A scenario opened with the harnesses' fixed backup clock, after the clean-up at opening
    /// (which removes the "reflog" scenario's two old backups); its git calls through what
    /// <paramref name="wrap"/> makes of the repository, where given.
    /// </summary>
    public static async Task<OperationsHarness> OpenWithBackupClockAsync(TempRepo repo, Func<IGitRepository, IGitRepository>? wrap = null)
    {
        var app = await OperationsHarness.OpenWrappedAsync(repo, git =>
        {
            ((GitRepository)git).BackupClock = BackupClock.Fixed(AppClocks.HarnessTime);
            return wrap?.Invoke(git) ?? git;
        });
        await RepositoryOperationsTestSupport.WaitUntilAsync(() => app.Log.Messages.Any(message => message.Contains($": {RepositoryViewModel.CleanUpBackupsTitle}: ", StringComparison.Ordinal)), "the clean-up at opening");
        await app.WaitForIdleAsync();
        return app;
    }

    /// <summary>The backup of <paramref name="kind"/> the list shows (the newest), with a snapshot of that subject where given.</summary>
    public static async Task<BackupInfo> BackupAsync(OperationsHarness app, string kind, string? snapshotSubject = null) =>
        (await app.Repository.Repository.ReadBackupsAsync()).First(backup => backup.Kind == kind
            && (snapshotSubject is null || backup.Refs.Any(entry => entry.IsChanges && entry.Subject == snapshotSubject)));

    /// <summary>A file of the working tree as text.</summary>
    public static string ReadText(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    /// <summary>
    /// A picture of the repository (7.11): what a step must give back. Each part is text, so that a
    /// difference shows in the test's message.
    /// </summary>
    public static async Task<string> PictureAsync(TempRepo repo)
    {
        var head = await repo.GitAllowingFailureAsync("rev-parse", "--symbolic-full-name", "HEAD", "HEAD");
        var refs = await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname) %(symref)");
        var index = await repo.GitAsync("ls-files", "--stage");
        var status = await repo.GitAsync("status", "--porcelain=v1", "--untracked-files=all");
        var stashes = await repo.GitAsync("stash", "list", "--format=%H %gs");
        var config = await repo.GitAsync("config", "--local", "--list");
        var files = Directory.EnumerateFiles(repo.Path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
            .Select(file => Path.GetRelativePath(repo.Path, file).Replace('\\', '/'))
            .Where(path => !path.StartsWith(".git/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => $"{path} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo.Path, path))))}");
        return string.Join(
            "\n---\n",
            head.StandardOutput.Trim(),
            string.Join('\n', refs.StandardOutput.Split('\n').Where(line => line.Length > 0 && !line.StartsWith("refs/visualcommit/", StringComparison.Ordinal))),
            index.StandardOutput.Trim(),
            status.StandardOutput.Trim(),
            string.Join('\n', files),
            stashes.StandardOutput.Trim(),
            string.Join('\n', config.StandardOutput.Split('\n').Order(StringComparer.Ordinal)));
    }
}
