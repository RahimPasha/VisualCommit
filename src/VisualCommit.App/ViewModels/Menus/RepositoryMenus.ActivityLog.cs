// Phase 5's activity log menu (docs/phase-5-design.md 2.2, 13.1): a row's "Copy command", "Copy
// output" and "Copy title". Work package WP5 builds it here, with its texts in
// OperationTexts.Menus.ActivityLog.cs; until then it throws, and nothing opens it.

using VisualCommit.App.ViewModels.Operations;

namespace VisualCommit.App.ViewModels.Menus;

public static partial class RepositoryMenus
{
    /// <summary>
    /// A row of the activity log (2.2): <c>Copy command</c> for a git call, <c>Copy output</c> for a
    /// call with output, <c>Copy title</c> for an operation. No other action: the log only shows.
    /// </summary>
    public static MenuModel ForActivityLogRow(RepositoryMenuContext context, ActivityLogMenuTarget row, IRepositoryActions actions) =>
        throw new NotImplementedException("The activity log's menu is work package WP5's (docs/phase-5-design.md 2.2).");
}

/// <summary>A right-click on a row of the activity log (docs/phase-5-design.md 2.2): an operation's or a git call's.</summary>
/// <param name="Title">An operation's title; null for a call.</param>
/// <param name="Command">A call's command as the log shows it, credentials hidden; null for an operation.</param>
/// <param name="Output">A call's output as the log shows it (its standard output and error); null for an operation or a call without output.</param>
public sealed record ActivityLogMenuTarget(string? Title, string? Command, string? Output);
