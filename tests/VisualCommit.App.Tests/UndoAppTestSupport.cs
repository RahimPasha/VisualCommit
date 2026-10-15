using System.Security.Cryptography;
using VisualCommit.App.ViewModels;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the App tests of the undo history share (docs/phase-5-design.md 7.11): their collection,
/// the toolbar's states read from the operations, a reflog's newest entry, and a picture of a
/// repository that a step must leave as it was: HEAD, the refs but the backups, the index's
/// entries, the status, every file's bytes, the stash list and the repository's own settings.
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
