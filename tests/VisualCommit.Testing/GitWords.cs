using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>
/// Git's version and the words it changed between the oldest git the app supports (2.34.1, D420)
/// and the git the reports quote (2.36), in one place: a test that compares git's own words takes
/// both, through these members, and keeps 2.36's words in its expectations. Each member names the
/// version that changed it and where, in git's sources at their tags and its release notes.
/// </summary>
public static class GitWords
{
    private static readonly Lazy<Task<GitVersion>> Version = new(async () =>
    {
        var result = await new GitRunner(TempRepo.GitPath).RunAsync(new GitCommand("--version"), CancellationToken.None).ConfigureAwait(false);
        return GitVersion.TryParse(result.StandardOutput, out var version)
            ? version
            : throw new InvalidOperationException($"git --version printed no version: {result.StandardOutput.Trim()}");
    });

    /// <summary>The prefixes after which a stash's text names its branch, up to the next <c>": "</c> (<c>builtin/stash.c</c> v2.34.1:1109, :1308, :1351, :1353).</summary>
    private static readonly string[] StashPrefixes = ["On ", "WIP on ", "index on ", "untracked files on "];

    /// <summary>The version of the git the tests run (<see cref="TempRepo"/>'s), read once with <c>git --version</c>.</summary>
    public static Task<GitVersion> VersionAsync() => Version.Value;

    /// <summary>
    /// Whether git knows <c>merge.conflictStyle=zdiff3</c>: from 2.35.0 (release notes 2.35.0, line
    /// 52). Before, a merge with it exits 128 with <c>unknown style 'zdiff3' given for
    /// 'merge.conflictstyle'</c> (<c>xdiff-interface.c</c> v2.34.1:323).
    /// </summary>
    public static bool HasZdiff3(GitVersion git) => git >= new GitVersion(2, 35, 0);

    /// <summary>
    /// Whether <c>git fetch . a:refs/heads/b</c> refuses a <c>b</c> checked out in any worktree:
    /// from 2.35.0 (release notes 2.35.0, lines 64 to 69; <c>builtin/fetch.c</c> v2.35.0:1396-1407).
    /// Before, it checks the current branch alone (v2.34.1:1388-1400) and moves another worktree's.
    /// </summary>
    public static bool FetchGuardsEveryWorktree(GitVersion git) => git >= new GitVersion(2, 35, 0);

    /// <summary>
    /// Whether a stash names its branch in full: from 2.36.0 (release notes 2.36.0, line 344;
    /// <c>builtin/stash.c</c> v2.36.0:1319). Before, it names the part after the branch's last
    /// <c>/</c> (v2.34.1:1302, v2.35.0:1335).
    /// </summary>
    public static bool StashNamesTheWholeBranch(GitVersion git) => git >= new GitVersion(2, 36, 0);

    /// <summary>The name a stash made on <paramref name="branch"/> gives it: the whole name from 2.36, the part after its last <c>/</c> before (<see cref="StashNamesTheWholeBranch"/>).</summary>
    public static string StashBranch(string branch, GitVersion git)
    {
        ArgumentNullException.ThrowIfNull(branch);
        return StashNamesTheWholeBranch(git) ? branch : branch[(branch.LastIndexOf('/') + 1)..];
    }

    /// <summary>
    /// <paramref name="text"/>, in 2.36's words, as <paramref name="git"/> writes it: below 2.36,
    /// each branch name that follows <c>On </c>, <c>WIP on </c>, <c>index on </c> or
    /// <c>untracked files on </c> at the text's start or after a space or a <c>"</c>, up to the next
    /// <c>": "</c>, is replaced by <see cref="StashBranch"/>'s; a name with a space in it is no
    /// branch and stays. Nothing else changes: <c>On feature/other: VisualCommit: before checking
    /// out feature/clash</c> becomes <c>On other: VisualCommit: before checking out feature/clash</c>.
    /// </summary>
    public static string StashText(string text, GitVersion git)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (StashNamesTheWholeBranch(git))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var at = 0;
        while (at < text.Length)
        {
            var name = NameAt(text, at);
            if (name is { } found)
            {
                result.Append(text, at, found.Start - at).Append(StashBranch(text[found.Start..found.End], git));
                at = found.End;
                continue;
            }

            result.Append(text[at]);
            at++;
        }

        return result.ToString();
    }

    /// <summary>
    /// What git prints for a branch <paramref name="local"/> made with <paramref name="remote"/>'s
    /// <paramref name="remoteBranch"/> as its upstream: <c>branch 'l' set up to track 'r/rb'.</c>
    /// from 2.35.0 (<c>branch.c</c> v2.35.0:144-145), <c>Branch 'l' set up to track remote branch
    /// 'rb' from 'r'.</c> before (v2.34.1:93-95).
    /// </summary>
    public static string TrackingSetUp(string local, string remote, string remoteBranch, GitVersion git) =>
        git >= new GitVersion(2, 35, 0)
            ? $"branch '{local}' set up to track '{remote}/{remoteBranch}'."
            : $"Branch '{local}' set up to track remote branch '{remoteBranch}' from '{remote}'.";

    /// <summary>The branch name a stash prefix at <paramref name="at"/> introduces, as the range it takes in <paramref name="text"/>; null when none starts there.</summary>
    private static (int Start, int End)? NameAt(string text, int at)
    {
        if (at > 0 && text[at - 1] is not (' ' or '"'))
        {
            return null;
        }

        foreach (var prefix in StashPrefixes)
        {
            if (!text.AsSpan(at).StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var start = at + prefix.Length;
            var end = text.IndexOf(": ", start, StringComparison.Ordinal);
            if (end > start && text.AsSpan(start, end - start).IndexOf(' ') < 0)
            {
                return (start, end);
            }
        }

        return null;
    }
}
