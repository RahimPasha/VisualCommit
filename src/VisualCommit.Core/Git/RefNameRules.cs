namespace VisualCommit.Core.Git;

/// <summary>How a new name clashes with an existing ref (phase 3 design 2.4, D96).</summary>
public enum RefNameClashKind
{
    /// <summary>A ref of exactly that name exists: "A branch named &lt;name&gt; exists."</summary>
    SameName,

    /// <summary>
    /// A ref whose name, or a folder of it, differs only in case: "A branch named &lt;existing&gt;
    /// exists in another case." On Windows and macOS, where refs may be files, git would store
    /// the new ref under the existing folder's case.
    /// </summary>
    OtherCase,

    /// <summary>
    /// A folder clash in either direction (<c>feature</c> beside <c>feature/alpha</c>, or
    /// <c>feature/alpha/x</c> beside <c>feature/alpha</c>): "This conflicts with the branch
    /// &lt;existing&gt;." Git refuses both with <c>cannot lock ref</c>.
    /// </summary>
    Folder,
}

/// <summary>A clash of a new name with an existing one.</summary>
/// <param name="Kind">How they clash.</param>
/// <param name="Existing">The existing name it clashes with.</param>
public sealed record RefNameClash(RefNameClashKind Kind, string Existing);

/// <summary>
/// Git's rules for ref names (<c>git check-ref-format</c>), checked in C# as the user types
/// (phase 3 design 2.4, D96), and the clashes with existing names that git refuses only when it
/// runs. A test compares <see cref="RefNameProblem"/> with <c>git check-ref-format</c>.
/// </summary>
public static class RefNameRules
{
    /// <summary>
    /// Whether names that differ only in case clash on this platform: on Windows and macOS refs
    /// may live in files, on file systems that ignore case.
    /// </summary>
    public static bool NamesIgnoreCase => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <summary>
    /// Why <paramref name="fullName"/> (<c>refs/heads/feature/x</c>) is not a valid ref name by
    /// <c>git check-ref-format</c>'s rules, as the end of "This is not a valid branch name: …";
    /// null when it is valid. The rules: no component that is empty, starts with <c>.</c> or ends
    /// with <c>.lock</c>; no <c>..</c>, no <c>@{</c>; no control character, space, <c>~</c>,
    /// <c>^</c>, <c>:</c>, <c>?</c>, <c>*</c>, <c>[</c> or <c>\</c>; not ending with <c>/</c> or
    /// <c>.</c>; not <c>@</c>; at least two components.
    /// </summary>
    public static string? RefNameProblem(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        if (fullName.Length == 0)
        {
            return "it is empty";
        }

        if (fullName == "@")
        {
            return "it cannot be @";
        }

        foreach (var c in fullName)
        {
            if (c < ' ' || c == '\x7f')
            {
                return "it contains a control character";
            }

            var problem = c switch
            {
                ' ' => "it contains a space",
                '~' => "it contains ~",
                '^' => "it contains ^",
                ':' => "it contains :",
                '?' => "it contains ?",
                '*' => "it contains *",
                '[' => "it contains [",
                '\\' => "it contains \\",
                _ => null,
            };
            if (problem is not null)
            {
                return problem;
            }
        }

        if (fullName.Contains("..", StringComparison.Ordinal))
        {
            return "it contains ..";
        }

        if (fullName.Contains("@{", StringComparison.Ordinal))
        {
            return "it contains @{";
        }

        if (fullName.EndsWith('/'))
        {
            return "it ends with /";
        }

        if (fullName.EndsWith('.'))
        {
            return "it ends with .";
        }

        var components = fullName.Split('/');
        if (components.Length < 2)
        {
            return "it needs a folder";
        }

        foreach (var component in components)
        {
            if (component.Length == 0)
            {
                return fullName.StartsWith('/') ? "it starts with /" : "it contains //";
            }

            if (component.StartsWith('.'))
            {
                return "a part of it starts with .";
            }

            if (component.EndsWith(".lock", StringComparison.Ordinal))
            {
                return "a part of it ends with .lock";
            }
        }

        return null;
    }

    /// <summary>
    /// Why <paramref name="name"/> is not a valid local branch name (null when it is):
    /// <see cref="RefNameProblem"/> of <c>refs/heads/&lt;name&gt;</c>, and it may not be empty,
    /// start with <c>-</c> or be <c>HEAD</c> (git branch refuses those).
    /// </summary>
    public static string? BranchNameProblem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length == 0 ? "it is empty"
            : name.StartsWith('-') ? "it starts with -"
            : name == "HEAD" ? "it cannot be HEAD"
            : RefNameProblem("refs/heads/" + name);
    }

    /// <summary>Whether <paramref name="name"/> is a valid local branch name (<see cref="BranchNameProblem"/>).</summary>
    public static bool IsValidBranchName(string name) => BranchNameProblem(name) is null;

    /// <summary>
    /// Why <paramref name="name"/> is not a valid tag name (null when it is):
    /// <see cref="RefNameProblem"/> of <c>refs/tags/&lt;name&gt;</c>, and it may not be empty or
    /// start with <c>-</c> (git tag refuses that).
    /// </summary>
    public static string? TagNameProblem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length == 0 ? "it is empty"
            : name.StartsWith('-') ? "it starts with -"
            : RefNameProblem("refs/tags/" + name);
    }

    /// <summary>Whether <paramref name="name"/> is a valid tag name (<see cref="TagNameProblem"/>).</summary>
    public static bool IsValidTagName(string name) => TagNameProblem(name) is null;

    /// <summary>
    /// Why <paramref name="name"/> is not a valid remote name (null when it is):
    /// <see cref="RefNameProblem"/> of <c>refs/remotes/&lt;name&gt;/HEAD</c>, as git remote checks
    /// it (2.23), and it may not be empty or start with <c>-</c>.
    /// </summary>
    public static string? RemoteNameProblem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length == 0 ? "it is empty"
            : name.StartsWith('-') ? "it starts with -"
            : RefNameProblem("refs/remotes/" + name + "/HEAD");
    }

    /// <summary>Whether <paramref name="name"/> is a valid remote name (<see cref="RemoteNameProblem"/>).</summary>
    public static bool IsValidRemoteName(string name) => RemoteNameProblem(name) is null;

    /// <summary>
    /// The first clash of the new name <paramref name="name"/> with one of <paramref name="existing"/>
    /// (short names of the same kind: local branches, or tags), or null. With
    /// <paramref name="ignoreCase"/> (<see cref="NamesIgnoreCase"/> on Windows and macOS) every
    /// path component is also compared ignoring case. An exact clash is preferred to one in
    /// another case, which is preferred to a folder clash.
    /// </summary>
    public static RefNameClash? FindClash(string name, IEnumerable<string> existing, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(existing);
        var parts = name.Split('/');
        RefNameClash? best = null;
        foreach (var other in existing)
        {
            var clash = Clash(parts, name, other, ignoreCase);
            if (clash is not null && (best is null || clash.Kind < best.Kind))
            {
                best = clash;
                if (clash.Kind == RefNameClashKind.SameName)
                {
                    break;
                }
            }
        }

        return best;
    }

    private static RefNameClash? Clash(string[] parts, string name, string other, bool ignoreCase)
    {
        if (string.Equals(name, other, StringComparison.Ordinal))
        {
            return new RefNameClash(RefNameClashKind.SameName, other);
        }

        var otherParts = other.Split('/');
        var shared = Math.Min(parts.Length, otherParts.Length);
        for (var i = 0; i < shared; i++)
        {
            if (string.Equals(parts[i], otherParts[i], StringComparison.Ordinal))
            {
                continue;
            }

            // The names part here. In another case only, they still meet in one file or folder.
            return ignoreCase && string.Equals(parts[i], otherParts[i], StringComparison.OrdinalIgnoreCase)
                ? new RefNameClash(RefNameClashKind.OtherCase, other)
                : null;
        }

        // One is a folder of the other: feature beside feature/alpha.
        return new RefNameClash(RefNameClashKind.Folder, other);
    }
}
