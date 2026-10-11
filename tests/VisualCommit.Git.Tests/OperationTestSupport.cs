using VisualCommit.Core.Git;
using VisualCommit.Testing;

namespace VisualCommit.Git.Tests;

/// <summary>Plain git reads that phase 3's operation tests check their results with.</summary>
internal static class OperationTestSupport
{
    /// <summary>The full id <paramref name="revision"/> names, as <c>git rev-parse --verify</c> gives it.</summary>
    public static async Task<string> RevAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("rev-parse", "--verify", revision)).StandardOutput.Trim();

    /// <summary>Git's standard output without the line ending at its end (and with LF line endings).</summary>
    public static async Task<string> GitTextAsync(TempRepo repo, params string[] arguments) =>
        (await repo.GitAsync(arguments)).StandardOutput.ReplaceLineEndings("\n").TrimEnd('\n');

    /// <summary>
    /// Git's standard output as lines, without empty ones. A command that exits with 1 and prints
    /// nothing, as <c>git config --get-regexp</c> does when nothing matches, gives no line.
    /// </summary>
    public static async Task<IReadOnlyList<string>> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var result = await repo.GitAllowingFailureAsync(arguments);
        if (result.ExitCode == 1 && result.StandardOutput.Length == 0 && result.StandardError.Length == 0)
        {
            return [];
        }

        return [.. result.EnsureSuccess(new GitCommand(arguments)).StandardOutput
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)];
    }

    /// <summary>Runs git in <paramref name="folder"/> (a linked worktree) with <paramref name="repo"/>'s isolation; its output trimmed.</summary>
    public static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment };
        return (await repo.Runner.RunAsync(command)).EnsureSuccess(command).StandardOutput.Trim();
    }

    /// <summary>A file of the working tree, as text.</summary>
    public static string ReadText(TempRepo repo, string relativePath) =>
        File.ReadAllText(Path.Combine(repo.Path, relativePath));

    /// <summary>A file of the repository's git folder (<c>MERGE_MSG</c>, <c>sequencer/todo</c>), as text.</summary>
    public static string ReadGitFile(TempRepo repo, string relativePath) =>
        File.ReadAllText(Path.Combine(repo.Path, ".git", relativePath));

    /// <summary>Whether the repository's git folder has the file or folder <paramref name="relativePath"/>.</summary>
    public static bool GitFileExists(TempRepo repo, string relativePath)
    {
        var path = Path.Combine(repo.Path, ".git", relativePath);
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>A file's content in a commit or tree (<c>&lt;rev&gt;:&lt;path&gt;</c>).</summary>
    public static async Task<string> ShowFileAsync(TempRepo repo, string revision, string path) =>
        (await repo.GitAsync("cat-file", "blob", $"{revision}:{path}")).StandardOutput;

    /// <summary>The files a commit's tree holds, in git's order.</summary>
    public static Task<IReadOnlyList<string>> TreeFilesAsync(TempRepo repo, string revision) =>
        GitLinesAsync(repo, "ls-tree", "-r", "--name-only", revision);
}
