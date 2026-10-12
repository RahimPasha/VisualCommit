using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of phase 4's conflict operations (docs/phase-4-design.md 10.1, 10.2, 7.2): resolving
/// a file, the external merge tool, and the questions about conflict markers that staging and
/// Continue ask. <c>{file}</c> is the file's name (14.3). Built by work package WP4.
/// </summary>
public static partial class OperationTexts
{
    // 10.1: the operations.

    /// <summary>"Resolve {file}": Mark resolved, a whole side, a card's buttons (11.7, 11.8).</summary>
    public static OperationText Resolve(string file) =>
        new($"Resolve {file}", $"Resolving {file}…", "Resolving failed", $"{file} could not be marked resolved.");

    /// <summary>"Merge tool for {file}" (11.9): it waits for <paramref name="tool"/>, and the user can cancel it (D149).</summary>
    public static OperationText MergeTool(string file, string tool) =>
        new($"Merge tool for {file}", $"Waiting for {tool} to close…", "Merge tool failed", $"{tool} did not resolve {file}.");

    // 10.2: results.

    /// <summary>"Resolved {file}" (secondary, 14.3).</summary>
    public static string Resolved(string file) => $"Resolved {file}";

    /// <summary>"Resolved {file} in {tool}" (secondary, 14.3).</summary>
    public static string ResolvedInTool(string file, string tool) => $"Resolved {file} in {tool}";

    /// <summary>"{tool} closed without resolving {file}" (secondary).</summary>
    public static string ToolClosedWithoutResolving(string tool, string file) => $"{tool} closed without resolving {file}";

    // 7.2: the questions about conflict markers.

    /// <summary>
    /// Staging or marking resolved a conflicted file whose working-tree file still has conflict
    /// markers (7.1, 7.2, D148): "{file} still has conflict markers." (several: "{N} files still
    /// have conflict markers."), "Mark it resolved anyway? The markers are committed with it."
    /// ("… them"), no detail, the primary button "Mark resolved".
    /// </summary>
    /// <param name="fileNames">The names of the files that still have markers, at least one.</param>
    public static ConfirmationRequest ConfirmStagingWithMarkers(IReadOnlyList<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        return fileNames.Count == 1
            ? new ConfirmationRequest($"{fileNames[0]} still has conflict markers.", "Mark it resolved anyway? The markers are committed with it.", string.Empty, "Mark resolved", IsDestructive: false)
            : new ConfirmationRequest($"{Count(fileNames.Count, "file")} still have conflict markers.", "Mark them resolved anyway? The markers are committed with them.", string.Empty, "Mark resolved", IsDestructive: false);
    }

    /// <summary>
    /// Continue (or the stage panel's commit that concludes an operation) with conflict markers
    /// staged (5.1, 7.2, 14.3): "1 file still has conflict markers." / "Continue anyway? The
    /// markers are committed with it." ("{N} files still have conflict markers." / "… with
    /// them."), no detail, the primary button "Continue".
    /// </summary>
    public static ConfirmationRequest ConfirmContinueWithMarkers(int files) => files == 1
        ? new ConfirmationRequest("1 file still has conflict markers.", "Continue anyway? The markers are committed with it.", string.Empty, "Continue", IsDestructive: false)
        : new ConfirmationRequest($"{Count(files, "file")} still have conflict markers.", "Continue anyway? The markers are committed with them.", string.Empty, "Continue", IsDestructive: false);

    // 10.4: disabled reasons the conflicted file's menu and the resolver share.

    /// <summary>"Open in merge tool" and the resolver's "Merge tool…" while git's configuration names no tool (11.9).</summary>
    public const string SetMergeTool = "Set merge.tool in git's configuration to use a merge tool";
}
