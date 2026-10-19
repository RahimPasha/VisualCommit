namespace VisualCommit.Core.Git;

/// <summary>
/// Git's own settings as the settings screen reads and writes them (D190, docs/phase-6-design.md
/// section 8): the identity, the pull mode and the merge tool, kept in git's configuration, never
/// in the app's files. Built in the Git project over the session's runner; one instance per
/// session (<c>TabServices.GitSettings</c>). Work package WP2 builds it.
/// </summary>
public interface IGitConfiguration : IDisposable
{
    /// <summary>
    /// Every value git reads, with its scope and file: one <c>git config --show-scope
    /// --show-origin -z --list</c> in <paramref name="workingTree"/>, or for null the outside read
    /// in the data folder, where no repository's settings apply (8.1).
    /// </summary>
    Task<GitConfigRead> ReadAsync(string? workingTree, CancellationToken cancellationToken = default);

    /// <summary><c>git var GIT_AUTHOR_IDENT</c> and <c>GIT_COMMITTER_IDENT</c> in the working tree (8.6).</summary>
    Task<GitIdentityRead> ReadIdentityAsync(string workingTree, CancellationToken cancellationToken = default);

    /// <summary>The file <c>git config --global</c> writes (8.2), worked out by the app, never asked of git.</summary>
    string GlobalFile { get; }

    /// <summary>
    /// The XDG file git reads before the global file when <c>GIT_CONFIG_GLOBAL</c> is unset, as a
    /// full path, for 8.2's naming of files; empty without a home folder.
    /// </summary>
    string XdgFile { get; }

    /// <summary>
    /// A variable of the environment git gets, the session's (8.12) over the process's; null when
    /// unset or removed. What 8.6's notices read (<c>GIT_AUTHOR_NAME</c>, <c>EMAIL</c>).
    /// </summary>
    string? Variable(string name);

    /// <summary>The global writes, in order, one git call each, one save at a time across the app (8.3); the first failure stops them and throws <see cref="GitException"/>.</summary>
    Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default);

    /// <summary><c>git mergetool --tool-help</c>, run once per session in the background; the list kept in the data folder answers at once (8.10, D202).</summary>
    Task<MergeToolHelp> ReadToolHelpAsync(CancellationToken cancellationToken = default);
}

/// <summary>A value's scope as <c>git config --show-scope</c> names it (8.1).</summary>
public enum GitConfigScope
{
    System,
    Global,
    Local,
    Worktree,
    Command,
}

/// <summary>One entry of a configuration read (8.1), in git's reading order.</summary>
/// <param name="Scope">Its scope.</param>
/// <param name="Origin">The file's full path; null for <see cref="GitConfigScope.Command"/>.</param>
/// <param name="Key">The key as git prints it: section and variable in lower case, the subsection as written.</param>
/// <param name="Value">Null for a key written without a value (git reads it as true).</param>
public sealed record GitConfigValue(GitConfigScope Scope, string? Origin, string Key, string? Value);

/// <summary>One configuration read (8.1): every entry, in git's reading order.</summary>
/// <param name="Values">The entries.</param>
/// <param name="InRepository">The read ran in a repository's working tree (its local and worktree scopes are in it).</param>
public sealed record GitConfigRead(IReadOnlyList<GitConfigValue> Values, bool InRepository)
{
    /// <summary>
    /// The entries of <paramref name="key"/>, in git's order. Keys compare as git compares them:
    /// the section and the variable ignoring case, the subsection (<c>branch.&lt;b&gt;</c>,
    /// <c>mergetool.&lt;t&gt;</c>) exactly.
    /// </summary>
    public IReadOnlyList<GitConfigValue> Of(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return [.. Values.Where(value => SameKey(value.Key, key))];
    }

    /// <summary>The last entry of <paramref name="key"/>, which git uses for a single-valued key; null when none sets it.</summary>
    public GitConfigValue? Winner(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Values.LastOrDefault(value => SameKey(value.Key, key));
    }

    /// <summary>Whether two keys name one setting: section and variable ignoring case, the subsection exactly.</summary>
    public static bool SameKey(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var (sectionA, subsectionA, variableA) = Split(a);
        var (sectionB, subsectionB, variableB) = Split(b);
        return string.Equals(sectionA, sectionB, StringComparison.OrdinalIgnoreCase)
            && string.Equals(variableA, variableB, StringComparison.OrdinalIgnoreCase)
            && string.Equals(subsectionA, subsectionB, StringComparison.Ordinal);
    }

    /// <summary>A key's section (before its first dot), subsection (between its first and last dots, or null) and variable (after its last dot).</summary>
    private static (string Section, string? Subsection, string Variable) Split(string key)
    {
        var first = key.IndexOf('.', StringComparison.Ordinal);
        var last = key.LastIndexOf('.');
        if (first < 0)
        {
            return (key, null, string.Empty);
        }

        return first == last
            ? (key[..first], null, key[(first + 1)..])
            : (key[..first], key[(first + 1)..last], key[(last + 1)..]);
    }
}

/// <summary>One write of the settings screen's Save (8.3).</summary>
/// <param name="Key">The key.</param>
/// <param name="Value">The value (<c>--replace-all</c>); null: the key is unset (<c>--unset-all</c>, exit 5 counting as done).</param>
public sealed record GitConfigWrite(string Key, string? Value);

/// <summary>Who git makes commits as in a working tree (8.6): <c>git var GIT_AUTHOR_IDENT</c> and <c>GIT_COMMITTER_IDENT</c>.</summary>
/// <param name="Author">The author, <c>Name &lt;email&gt;</c> without git's trailing time and zone; null when git refused.</param>
/// <param name="Committer">The committer, as <paramref name="Author"/>; null when git refused.</param>
/// <param name="Refusal">Git's last <c>fatal:</c> line when it refused either (no name or email set, an empty name, …); null when both were read.</param>
public sealed record GitIdentityRead(string? Author, string? Committer, string? Refusal);

/// <summary>What <c>git mergetool --tool-help</c> lists (8.10).</summary>
/// <param name="Found">The tools git found on this computer.</param>
/// <param name="NotFound">The tools git knows but did not find.</param>
/// <param name="Failed">The lookup failed (exit not 0, or git did not start): both lists are empty.</param>
public sealed record MergeToolHelp(IReadOnlyList<MergeTool> Found, IReadOnlyList<MergeTool> NotFound, bool Failed)
{
    /// <summary>No tools, failed: what a lookup that could not run gives.</summary>
    public static MergeToolHelp FailedLookup { get; } = new([], [], Failed: true);
}

/// <summary>One merge tool git knows (8.10).</summary>
/// <param name="Name">Its name, what <c>merge.tool</c> takes (<c>kdiff3</c>).</param>
/// <param name="Description">Git 2.55's description of it; null where git gives none (2.36).</param>
public sealed record MergeTool(string Name, string? Description);
