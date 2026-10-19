using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// The merge tools git knows, as <c>git mergetool --tool-help</c> lists them
/// (docs/phase-6-design.md 8.10, facts 0.3): the pure reading of its output.
/// </summary>
public static class GitMergeTools
{
    /// <summary>The end of the header above the tools git found (<c>'git mergetool --tool=&lt;tool&gt;' may be set to one of the following:</c>).</summary>
    public const string FoundHeaderEnd = "may be set to one of the following:";

    /// <summary>The header above the tools git knows but did not find.</summary>
    public const string NotFoundHeader = "The following tools are valid, but not currently available:";

    /// <summary>The header of the tools the configuration defines (<c>mergetool.&lt;name&gt;.cmd</c>), one tab in.</summary>
    public const string UserDefinedHeader = "\tuser-defined:";

    /// <summary>
    /// The tools that need a terminal, which the app cannot run (D149 starts git without one): the
    /// settings screen leaves them out of its list (8.10). Git 2.36 gives no descriptions to tell
    /// them by, so the app keeps this list. <c>gvimdiff</c> and its numbered forms are windowed and stay.
    /// </summary>
    public static IReadOnlySet<string> TerminalTools { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "vimdiff",
        "vimdiff1",
        "vimdiff2",
        "vimdiff3",
        "nvimdiff",
        "nvimdiff1",
        "nvimdiff2",
        "nvimdiff3",
        "emerge",
    };

    /// <summary>
    /// Reads <c>git mergetool --tool-help</c>'s output (8.10): the lines that start with two tabs
    /// after the header ending <see cref="FoundHeaderEnd"/> are tools found, those after
    /// <see cref="NotFoundHeader"/> tools not found; a tool's first whitespace-separated token is its
    /// name and the rest Git 2.55's description (none on 2.36). The <see cref="UserDefinedHeader"/>
    /// block and its lines are skipped (those tools come from the configuration read), and so are
    /// git's closing note and anything else. A blank line ends a block. Nothing is left out here:
    /// the screen leaves out <see cref="TerminalTools"/>. <see cref="MergeToolHelp.Failed"/> is false.
    /// </summary>
    public static MergeToolHelp ParseToolHelp(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var found = new List<MergeTool>();
        var notFound = new List<MergeTool>();
        List<MergeTool>? block = null;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.EndsWith(FoundHeaderEnd, StringComparison.Ordinal))
            {
                block = found;
            }
            else if (line == NotFoundHeader)
            {
                block = notFound;
            }
            else if (line.TrimEnd() == UserDefinedHeader || line.Trim().Length == 0)
            {
                block = null;
            }
            else if (block is not null && line.StartsWith("\t\t", StringComparison.Ordinal) && ToolOf(line[2..]) is { } tool)
            {
                block.Add(tool);
            }
        }

        return new MergeToolHelp(found, notFound, Failed: false);
    }

    private static MergeTool? ToolOf(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end]))
        {
            end++;
        }

        var description = trimmed[end..].Trim();
        return new MergeTool(trimmed[..end], description.Length == 0 ? null : description);
    }
}
