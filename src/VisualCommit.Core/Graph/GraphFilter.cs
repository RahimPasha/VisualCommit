using VisualCommit.Core.Git;

namespace VisualCommit.Core.Graph;

/// <summary>
/// What the graph hides or solos (docs/phase-5-design.md 8.1, 8.2, D178), and the revisions
/// <c>git log</c> gets for it in place of today's <c>--branches --remotes --tags --ignore-missing
/// HEAD --</c> (<see cref="Unfiltered"/>). Hiding excludes branches by their names relative to
/// their namespace (<c>--exclude=&lt;b&gt;</c> before <c>--branches</c>, <c>--exclude=&lt;r&gt;/&lt;b&gt;</c>
/// before <c>--remotes</c>, and <c>&lt;r&gt;/HEAD</c> of every remote with a hidden branch, which
/// otherwise keeps its target's history); never <c>^ref</c>, which removes shared history too.
/// Soloing names the soloed refs and HEAD. HEAD is always named, so the checked-out branch's
/// history shows even while it is hidden, and the arguments never depend on HEAD's branch. Past
/// <see cref="MaxArgumentLength"/> characters the revisions go on standard input as plain full
/// names, which every git from 2.30 accepts there. Two filters are equal when they load the same
/// graph (<see cref="Key"/>).
/// </summary>
public sealed class GraphFilter : IEquatable<GraphFilter>
{
    /// <summary>
    /// How long the revision arguments may be on the command line: a command line over about
    /// 32,760 characters makes <c>Process.Start</c> fail on Windows (0.6), about 800 hidden refs.
    /// </summary>
    public const int MaxArgumentLength = 30_000;

    private const string HeadsPrefix = "refs/heads/";
    private const string RemotesPrefix = "refs/remotes/";

    /// <param name="hidden">The hidden branches' full names; anything but a local or remote branch is left out (tags stay, 8.1).</param>
    /// <param name="soloed">The soloed refs' full names; solo mode is on while there is one.</param>
    /// <param name="remotes">The repository's remotes, which tell a hidden remote branch's remote (the longest name it starts with): their <c>&lt;r&gt;/HEAD</c> is excluded too.</param>
    public GraphFilter(IEnumerable<string> hidden, IEnumerable<string> soloed, IEnumerable<string> remotes)
    {
        ArgumentNullException.ThrowIfNull(hidden);
        ArgumentNullException.ThrowIfNull(soloed);
        ArgumentNullException.ThrowIfNull(remotes);
        Hidden = [.. hidden.Where(name => name.StartsWith(HeadsPrefix, StringComparison.Ordinal) || name.StartsWith(RemotesPrefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Soloed = [.. soloed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        var known = remotes.ToList();
        RemotesWithHiddenBranches = [.. Hidden.Where(name => name.StartsWith(RemotesPrefix, StringComparison.Ordinal))
            .Select(name => RemoteOf(name[RemotesPrefix.Length..], known))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Key = string.Join('\n', [.. Hidden.Select(name => "hide " + name), .. RemotesWithHiddenBranches.Select(remote => "remote " + remote), .. Soloed.Select(name => "solo " + name)]);
        var commandLine = IsSolo ? SoloArguments() : HideArguments();
        UsesStandardInput = commandLine.Sum(argument => argument.Length + 1) > MaxArgumentLength;
        RevisionArguments = UsesStandardInput ? ["--ignore-missing", "--stdin", "--"] : commandLine;
    }

    /// <summary>Today's revisions, which the graph loads without a filter (D47).</summary>
    public static IReadOnlyList<string> Unfiltered { get; } = ["--branches", "--remotes", "--tags", "--ignore-missing", "HEAD", "--"];

    /// <summary>The hidden branches' full names, sorted.</summary>
    public IReadOnlyList<string> Hidden { get; }

    /// <summary>The soloed refs' full names, sorted.</summary>
    public IReadOnlyList<string> Soloed { get; }

    /// <summary>The remotes with a hidden branch, sorted: their <c>&lt;r&gt;/HEAD</c> is excluded too (0.6).</summary>
    public IReadOnlyList<string> RemotesWithHiddenBranches { get; }

    /// <summary>Solo mode: the soloed refs' histories and HEAD's are shown; <see cref="Hidden"/> waits until it ends.</summary>
    public bool IsSolo => Soloed.Count > 0;

    /// <summary>Nothing is hidden or soloed: the graph loads as without a filter.</summary>
    public bool IsEmpty => Hidden.Count == 0 && Soloed.Count == 0;

    /// <summary>A text that differs exactly when two filters load different graphs: it joins the refs' fingerprint in the refresh's comparison (8.2).</summary>
    public string Key { get; }

    /// <summary>
    /// The revision arguments of <c>git log</c>, after its format options: the hide or solo form,
    /// or <c>--ignore-missing --stdin --</c> when that is too long for a command line
    /// (<see cref="UsesStandardInput"/>).
    /// </summary>
    public IReadOnlyList<string> RevisionArguments { get; }

    /// <summary>The revisions are too many for a command line: <see cref="StandardInput"/> gives them on standard input.</summary>
    public bool UsesStandardInput { get; }

    /// <summary>
    /// The revisions on standard input, one per line, when <see cref="UsesStandardInput"/>: for solo
    /// the soloed names and <c>HEAD</c>; for hide the full names of every branch, remote branch and
    /// tag of <paramref name="refs"/> that is not hidden, and <c>HEAD</c>. Hiding this way waits for
    /// the refs (about 0.2 s).
    /// </summary>
    public string StandardInput(RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var names = IsSolo
            ? Soloed
            : refs.Refs.Select(gitRef => gitRef.FullName).Where(name => !Hidden.Contains(name, StringComparer.Ordinal));
        return string.Concat(names.Append("HEAD").Select(name => name + "\n"));
    }

    public bool Equals(GraphFilter? other) => other is not null && string.Equals(Key, other.Key, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is GraphFilter other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);

    /// <summary>
    /// The remote a remote branch's name (relative to <c>refs/remotes/</c>) belongs to: the longest
    /// of <paramref name="remotes"/> it starts with, followed by a slash; without one, its first
    /// component (remote names may hold a slash, 0.5): <see cref="RemoteBranchNames.RemoteOf"/>.
    /// </summary>
    public static string RemoteOf(string remoteBranch, IEnumerable<string> remotes) => RemoteBranchNames.RemoteOf(remoteBranch, remotes);

    /// <summary>
    /// <c>--exclude=&lt;b&gt;</c> … <c>--branches</c>, <c>--exclude=&lt;r&gt;/&lt;b&gt;</c> … and
    /// <c>--exclude=&lt;r&gt;/HEAD</c> … <c>--remotes</c>, <c>--tags --ignore-missing HEAD --</c>; an
    /// exclude applies only to the next <c>--branches</c> or <c>--remotes</c>. Ref names cannot hold
    /// <c>*?[\</c>, so an exact name needs no escaping.
    /// </summary>
    private List<string> HideArguments()
    {
        List<string> arguments =
        [
            .. Hidden.Where(name => name.StartsWith(HeadsPrefix, StringComparison.Ordinal)).Select(name => "--exclude=" + name[HeadsPrefix.Length..]),
            "--branches",
            .. Hidden.Where(name => name.StartsWith(RemotesPrefix, StringComparison.Ordinal)).Select(name => "--exclude=" + name[RemotesPrefix.Length..]),
            .. RemotesWithHiddenBranches.Select(remote => $"--exclude={remote}/HEAD"),
            "--remotes",
            "--tags",
            "--ignore-missing",
            "HEAD",
            "--",
        ];
        return arguments;
    }

    /// <summary><c>--ignore-missing &lt;full name&gt; … HEAD --</c>: <c>--ignore-missing</c> comes first, and full names, since a short one is ambiguous (a tag wins over a branch of its name).</summary>
    private List<string> SoloArguments() => ["--ignore-missing", .. Soloed, "HEAD", "--"];
}
