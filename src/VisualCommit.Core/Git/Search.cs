namespace VisualCommit.Core.Git;

/// <summary>What a search matches (docs/phase-5-design.md 3.2, D174).</summary>
public enum SearchMode
{
    /// <summary>
    /// The whole message contains the text, case ignored (git, <c>-F --regexp-ignore-case
    /// --grep</c>); the author and the SHA prefix are matched by the app over the rows, in memory.
    /// </summary>
    Commits,

    /// <summary>The commit's own change (against its first parent) touches a path containing the text; case ignored for A to Z.</summary>
    Files,

    /// <summary>The commit's own change adds or removes the text (the number of its occurrences changes, O7); case ignored for A to Z.</summary>
    Changes,
}
