using VisualCommit.Core.Git;
using VisualCommit.Testing;

namespace VisualCommit.Git.Tests;

/// <summary>Plain git reads and set-ups that phase 4's rebase tests share (WP1).</summary>
internal static class RebaseTestSupport
{
    /// <summary>
    /// A commit's message exactly as git keeps it: the part of <c>git cat-file commit</c> after the
    /// header's first empty line (line feeds as stored).
    /// </summary>
    public static async Task<string> MessageAsync(TempRepo repo, string revision)
    {
        var raw = (await repo.GitAsync("cat-file", "commit", revision)).StandardOutput;
        return raw[(raw.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];
    }

    /// <summary>The subjects of <paramref name="range"/>, oldest first.</summary>
    public static async Task<IReadOnlyList<string>> SubjectsAsync(TempRepo repo, string range) =>
        [.. (await repo.GitAsync("log", "--reverse", "--format=%s", range, "--")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))];

    /// <summary>A commit's author as name, e-mail and the author date in seconds (never git's dates as text).</summary>
    public static async Task<string> AuthorAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("log", "-1", "--format=%an <%ae> %at", revision, "--")).StandardOutput.Trim();

    /// <summary>Every branch and its commit (<c>refs/heads</c>), one line each.</summary>
    public static async Task<string> BranchesAsync(TempRepo repo) =>
        (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)", "refs/heads")).StandardOutput;

    /// <summary>The full name of the branch HEAD is on, or null when HEAD is detached.</summary>
    public static async Task<string?> HeadBranchAsync(TempRepo repo)
    {
        var result = await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD");
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>
    /// Whether the git the tests run is 2.36, whose lines a test may assert word for word; on any
    /// other version only the classifying lines (phase 3 handoff, "Two gits").
    /// </summary>
    public static async Task<bool> GitIs236Async(TempRepo repo) =>
        GitVersion.TryParse((await repo.GitAsync("--version")).StandardOutput, out var version) && version is { Major: 2, Minor: 36 };

    /// <summary>Whether the working tree's git folder holds a rebase folder (<c>rebase-merge/</c> or <c>rebase-apply/</c>).</summary>
    public static bool RebaseInProgress(TempRepo repo) =>
        Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-merge")) || Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-apply"));

    /// <summary>
    /// Writes the hook <paramref name="name"/> into the repository's hooks folder as an <c>sh</c>
    /// script (executable on Linux and macOS; git for Windows runs any file there).
    /// </summary>
    public static void InstallHook(TempRepo repo, string name, string body)
    {
        var path = Path.Combine(repo.Path, ".git", "hooks", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/bin/sh\n" + body.ReplaceLineEndings("\n") + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>A plan from <paramref name="list"/>'s commits, oldest first, with one action (and message) per commit.</summary>
    public static RebasePlan Plan(RebaseList list, string branchRef, params (RebaseAction Action, string? Message)[] steps)
    {
        if (steps.Length != list.CommitsOldestFirst.Count)
        {
            throw new ArgumentException($"The list has {list.CommitsOldestFirst.Count} commits, the plan {steps.Length}.", nameof(steps));
        }

        return new RebasePlan(
            list.Onto,
            list.BranchTip,
            branchRef,
            [.. list.CommitsOldestFirst.Select((entry, index) => new RebaseStep(entry.Sha, steps[index].Action, steps[index].Message))]);
    }

    /// <summary><paramref name="plan"/> with its steps put in the order of <paramref name="order"/> (indexes into the plan's steps, oldest first).</summary>
    public static RebasePlan Reorder(RebasePlan plan, params int[] order) =>
        plan with { StepsOldestFirst = [.. order.Select(index => plan.StepsOldestFirst[index])] };

    public static (RebaseAction, string?) Pick => (RebaseAction.Pick, null);

    public static (RebaseAction, string?) Drop => (RebaseAction.Drop, null);

    public static (RebaseAction, string?) Fixup => (RebaseAction.Fixup, null);

    public static (RebaseAction, string?) Squash => (RebaseAction.Squash, null);

    public static (RebaseAction, string?) Reword(string message) => (RebaseAction.Reword, message);

    public static (RebaseAction, string?) Target(string message) => (RebaseAction.Pick, message);
}
