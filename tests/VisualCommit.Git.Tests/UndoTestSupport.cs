using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What the undo tests share (docs/phase-5-design.md 7.11): the repository they start from, the
/// recording of an action as the app records it (the states before and after it, read inside the
/// action as <see cref="IGitRepository.ReadRefStatesAsync"/> reads them, and what the action
/// captured), the steps, and a picture of a repository to compare before and after a step: HEAD,
/// the refs, the index's entries (<c>ls-files --stage</c>, never the index file's bytes), the
/// status, every file's bytes, the stash list and the repository's own settings.
/// </summary>
internal static class UndoTestSupport
{
    /// <summary>The repository every undo test starts from, built once and copied per test.</summary>
    private static readonly Lazy<Task<TempRepo>> Template = new(BuildAsync);

    /// <summary>
    /// A copy of the undo tests' repository. <c>main</c> (checked out, clean): <c>c1</c> adds
    /// <c>a.txt</c>, <c>b.txt</c> and <c>src/c.txt</c>, <c>c2</c> changes <c>a.txt</c>, <c>c3</c>
    /// changes <c>b.txt</c>. <c>feature</c> leaves <c>c2</c>: <c>f1</c> adds <c>f.txt</c>,
    /// <c>f2</c> changes <c>src/c.txt</c>. <c>behind</c> is at <c>c2</c>. The bare remote
    /// <c>origin</c> has <c>main</c> and <c>feature</c>, both tracked.
    /// </summary>
    public static async Task<TempRepo> UndoRepoAsync() => await (await Template.Value.ConfigureAwait(false)).CopyAsync().ConfigureAwait(false);

    private static async Task<TempRepo> BuildAsync()
    {
        var repo = await TempRepo.CreateAsync("undo");
        repo.WriteFile("a.txt", "a1\n").WriteFile("b.txt", "b1\n").WriteFile("src/c.txt", "c1\n");
        await repo.CommitAsync("c1");
        await repo.CommitFileAsync("a.txt", "a2\n", "c2");
        await repo.CreateBranchAsync("feature");
        await repo.CreateBranchAsync("behind");
        await repo.CommitFileAsync("b.txt", "b2\n", "c3");
        await repo.CheckoutAsync("feature");
        await repo.CommitFileAsync("f.txt", "f1\n", "f1");
        await repo.CommitFileAsync("src/c.txt", "c-feature\n", "f2");
        await repo.CheckoutAsync("main");
        await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", setUpstream: true, "main", "feature");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    /// <summary>The repository opened as the app opens it, with a runner whose commit dates the test can move (<see cref="DatedRunner"/>).</summary>
    public static async Task<(GitRepository Repository, DatedRunner Runner)> OpenDatedAsync(TempRepo repo)
    {
        var runner = new DatedRunner(NewRunner());
        return (await GitRepository.OpenAsync(runner, repo.Path, TestCancelled), runner);
    }

    /// <summary>
    /// Records <paramref name="action"/> as the app's recorder does (7.7): the state before, the
    /// action with an <see cref="UndoCapture"/> running in its flow, the state after with the files
    /// the capture names. Returns the entry, or null when nothing the scope names changed.
    /// </summary>
    public static async Task<UndoEntry> RecordAsync(GitRepository repository, UndoScope scope, Func<Task> action)
    {
        var before = await repository.ReadRefStatesAsync(scope, cancellationToken: TestCancelled);
        UndoCaptured captured;
        using (UndoCapture.Begin(out var capture))
        {
            await action();
            captured = capture.Captured;
        }

        var after = await repository.ReadRefStatesAsync(scope with { Files = [.. scope.Files.Union(captured.ChangedFiles, StringComparer.Ordinal)] }, cancellationToken: TestCancelled);
        return new UndoEntry(scope.Kind ?? throw new ArgumentException("An unrecorded scope.", nameof(scope)), scope.Text, "op-1", before, after, captured) { Scope = scope };
    }

    /// <summary>Undoes <paramref name="entry"/> and asserts that it was done; returns the entry as the history keeps it.</summary>
    public static Task<UndoEntry> UndoAsync(GitRepository repository, UndoEntry entry) => StepAsync(repository, entry, UndoDirection.Undo);

    /// <summary>Redoes <paramref name="entry"/> and asserts that it was done; returns the entry as the history keeps it.</summary>
    public static Task<UndoEntry> RedoAsync(GitRepository repository, UndoEntry entry) => StepAsync(repository, entry, UndoDirection.Redo);

    private static async Task<UndoEntry> StepAsync(GitRepository repository, UndoEntry entry, UndoDirection direction)
    {
        var result = await repository.RunUndoStepAsync(UndoStep.For(entry, direction), NetworkOptions.None, TestCancelled);
        Assert.True(result.IsDone, $"The {direction} of {entry.Text} was refused: {result.Refusal}");
        return result.Entry;
    }

    /// <summary>Runs a step that must be refused; returns the refusal.</summary>
    public static async Task<UndoRefusal> RefusedAsync(GitRepository repository, UndoEntry entry, UndoDirection direction)
    {
        var result = await repository.RunUndoStepAsync(UndoStep.For(entry, direction), NetworkOptions.None, TestCancelled);
        Assert.False(result.IsDone, $"The {direction} of {entry.Text} was not refused.");
        Assert.Same(entry, result.Entry);
        return result.Refusal!;
    }

    /// <summary>
    /// A picture of the repository that a step must leave as the recorded state was (7.11): HEAD,
    /// every ref but the backups (a symbolic one with the ref it points at), the index's entries,
    /// the status, every file's bytes, the stash list, the settings (in order: the meaning, not the
    /// file's layout, which re-added keys change).
    /// </summary>
    public static async Task<RepoPicture> PictureAsync(TempRepo repo)
    {
        var head = await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD");
        var sha = await repo.GitAllowingFailureAsync("rev-parse", "-q", "--verify", "HEAD");
        var refs = await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname) %(symref)");
        var index = await repo.GitAsync("ls-files", "--stage", "-z");
        var status = await repo.GitAsync("status", "--porcelain=v2", "-z", "--untracked-files=all", "--ignored");
        var stashes = await repo.GitAsync("stash", "list", "--format=%gs");
        var config = await repo.GitAsync("config", "--local", "--list");
        return new RepoPicture(
            $"{head.StandardOutput.Trim()} {sha.StandardOutput.Trim()}",
            Lines(refs.StandardOutput.Split('\n').Where(line => !line.StartsWith(GitRepository.BackupRefPrefix, StringComparison.Ordinal) && !line.StartsWith("refs/stash ", StringComparison.Ordinal))),
            Lines(index.StandardOutput.Split('\0')),
            Lines(status.StandardOutput.Split('\0')),
            Lines(FileHashes(repo.Path)),
            Lines(stashes.StandardOutput.Split('\n')),
            Lines(config.StandardOutput.Split('\n').Order(StringComparer.Ordinal)));
    }

    /// <summary>HEAD's reflog, newest first: a refused step writes none (7.5, phase 4's check A5).</summary>
    public static async Task<string> HeadReflogAsync(TempRepo repo) =>
        (await repo.GitAllowingFailureAsync("log", "-g", "--format=%H %gs", "HEAD")).StandardOutput;

    /// <summary>The working tree's files (the git folder left out), each with the SHA-256 of its bytes.</summary>
    private static IEnumerable<string> FileHashes(string root) =>
        Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(path => !path.StartsWith(".git/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => $"{path} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))))}");

    private static string Lines(IEnumerable<string> lines) => string.Join('\n', lines.Where(line => line.Length > 0)) + "\n";

    /// <summary>A file of the working tree as text.</summary>
    public static string ReadText(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    /// <summary>The object a ref points at, or null.</summary>
    public static Task<string?> TargetAsync(TempRepo repo, string fullName) => repo.RefTargetAsync(fullName);

    /// <summary>The stash list's commits, newest first.</summary>
    public static async Task<IReadOnlyList<string>> StashShasAsync(TempRepo repo) => [.. (await repo.StashListAsync()).Select(entry => entry.Sha)];
}

/// <summary>A repository as the undo tests compare it (<see cref="UndoTestSupport.PictureAsync"/>); each part is text, so that a difference shows in the test's message.</summary>
internal sealed record RepoPicture(string Head, string Refs, string Index, string Status, string Files, string Stashes, string Config);

/// <summary>
/// A runner that gives every call the commit date the test sets (<see cref="Date"/>), as the clock
/// moves between an action and its undo and redo in the app: a stash made again then gets a new
/// commit (7.11). Null leaves the process's fixed date (<c>GitIsolation.AppCommitDate</c>).
/// </summary>
internal sealed class DatedRunner(IGitRunner real) : IGitRunner
{
    public DateTimeOffset? Date { get; set; }

    public string ExecutablePath => real.ExecutablePath;

    public Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
    {
        if (Date is not { } date)
        {
            return real.RunAsync(command, cancellationToken);
        }

        var text = date.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var environment = new Dictionary<string, string?>(command.Environment)
        {
            ["GIT_AUTHOR_DATE"] = text,
            ["GIT_COMMITTER_DATE"] = text,
        };
        var dated = new GitCommand([.. command.Arguments])
        {
            WorkingDirectory = command.WorkingDirectory,
            StandardInput = command.StandardInput,
            RecordsStandardInput = command.RecordsStandardInput,
            Encoding = command.Encoding,
            Environment = environment,
            OnOutputLine = command.OnOutputLine,
            OnErrorLine = command.OnErrorLine,
        };
        return real.RunAsync(dated, cancellationToken);
    }

    /// <summary>Moves the date on by a minute.</summary>
    public void Advance() => Date = (Date ?? new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero)).AddMinutes(1);
}
