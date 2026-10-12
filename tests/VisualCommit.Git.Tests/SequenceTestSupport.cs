using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What the tests of lists, rebase stops, Continue and Skip share (docs/phase-4-design.md
/// sections 2.5, 4, 5 and 8): git calls with variables of their own, todos shaped as the app
/// writes them (2.5) handed to <c>git rebase -i</c> through a sequence editor that copies them,
/// hooks, and the version of the git under test.
/// </summary>
internal static class SequenceTestSupport
{
    /// <summary>
    /// Runs git in <paramref name="repo"/> with its isolation, the clock's time as the author and
    /// committer date (as <see cref="TempRepo.GitAsync"/> gives them), and
    /// <paramref name="environment"/> on top; returns the result also when git fails.
    /// </summary>
    public static Task<GitResult> GitWithEnvironmentAsync(TempRepo repo, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var time = repo.ClockTime.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var merged = new Dictionary<string, string?>(repo.Environment)
        {
            ["GIT_AUTHOR_DATE"] = time,
            ["GIT_COMMITTER_DATE"] = time,
        };
        foreach (var (name, value) in environment)
        {
            merged[name] = value;
        }

        return repo.Runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = repo.Path, Environment = merged });
    }

    /// <summary>
    /// <c>git rebase -i &lt;onto&gt;</c> with <paramref name="todo"/> as its todo and
    /// <paramref name="messages"/> as <c>rebase-merge/vc-message-1</c>, <c>-2</c>, … as the app
    /// delivers them (2.5): written into a folder of their own (UTF-8 without BOM, LF), and
    /// <c>GIT_SEQUENCE_EDITOR</c> a shell function that copies them, so that the <c>"$@"</c> git
    /// appends becomes its argument. Returns git's result, also when it stops or fails.
    /// </summary>
    public static Task<GitResult> InteractiveRebaseAsync(TempRepo repo, string onto, string todo, params string[] messages)
    {
        var folder = repo.SiblingPath($"todo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var copies = new StringBuilder("vc() { ");
        for (var i = 0; i < messages.Length; i++)
        {
            var file = Path.Combine(folder, $"m{i + 1}");
            File.WriteAllText(file, messages[i], encoding);
            copies.Append(CultureInfo.InvariantCulture, $"cp {ShellQuote(file)} \"$(dirname \"$1\")/vc-message-{i + 1}\" && ");
        }

        var todoFile = Path.Combine(folder, "todo");
        File.WriteAllText(todoFile, todo, encoding);
        copies.Append(CultureInfo.InvariantCulture, $"cp {ShellQuote(todoFile)} \"$1\"; }}; vc");
        return GitWithEnvironmentAsync(repo, new Dictionary<string, string?> { ["GIT_SEQUENCE_EDITOR"] = copies.ToString() }, "rebase", "-i", onto);
    }

    /// <summary>
    /// The app's guarded amend for group <paramref name="group"/> (2.5): it amends HEAD with
    /// <c>rebase-merge/vc-message-&lt;n&gt;</c> only when the group made exactly one commit on
    /// top of its label.
    /// </summary>
    public static string GuardedAmend(int group) => string.Create(
        CultureInfo.InvariantCulture,
        $"exec if l=$(git rev-parse -q --verify refs/rewritten/vc-{group}) && [ -n \"$l\" ] && [ \"$(git rev-parse -q --verify HEAD^)\" = \"$l\" ]; then git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file=\"$(git rev-parse --git-path rebase-merge/vc-message-{group})\"; fi");

    /// <summary>
    /// A group as the app writes it (2.5): <c>label vc-&lt;n&gt;</c>, <c>pick &lt;target&gt;</c>,
    /// <c>fixup &lt;member&gt;</c> for each member, the guarded amend; one line each, LF.
    /// </summary>
    public static string Group(int group, string target, params string[] members)
    {
        var lines = new StringBuilder();
        lines.Append(CultureInfo.InvariantCulture, $"label vc-{group}\npick {target}\n");
        foreach (var member in members)
        {
            lines.Append(CultureInfo.InvariantCulture, $"fixup {member}\n");
        }

        return lines.Append(GuardedAmend(group)).Append('\n').ToString();
    }

    /// <summary>A path as one word for <c>sh</c>: forward slashes, in single quotes, a <c>'</c> written <c>'\''</c>.</summary>
    public static string ShellQuote(string path) => "'" + path.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Writes the hook <paramref name="name"/> (<c>pre-commit</c>, <c>commit-msg</c>) of
    /// <paramref name="repo"/> as a <c>sh</c> script with <paramref name="body"/>, executable.
    /// </summary>
    public static void WriteHook(TempRepo repo, string name, string body)
    {
        var hooks = Path.Combine(repo.Path, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        var path = Path.Combine(hooks, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>A hook body that refuses while the file <c>refuse</c> exists in the git folder, and lets the commit through otherwise.</summary>
    public const string RefusingWhileMarked = "if [ -e \"$(git rev-parse --git-dir)/refuse\" ]; then echo 'the hook refuses this message' >&2; exit 1; fi\nexit 0";

    /// <summary>The version of the git under test (<c>git --version</c>).</summary>
    public static async Task<GitVersion> GitVersionAsync(TempRepo repo)
    {
        var output = (await repo.GitAsync("--version")).StandardOutput;
        Assert.True(GitVersion.TryParse(output, out var version), output);
        return version;
    }

    /// <summary>A file of the repository's git folder exists (<c>rebase-merge/amend</c>, <c>CHERRY_PICK_HEAD</c>).</summary>
    public static bool InGitDir(TempRepo repo, string relativePath) => OperationTestSupport.GitFileExists(repo, relativePath);
}
