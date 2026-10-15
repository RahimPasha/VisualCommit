// Phase 5's activity log menu (docs/phase-5-design.md 2.2, 13.1): a row's "Copy command", "Copy
// output" and "Copy title", with their texts in OperationTexts.Menus.ActivityLog.cs. The log's view
// opens it on an operation's row, a call's row and a call's output box.

using VisualCommit.App.ViewModels.Operations;
using static VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.App.ViewModels.Menus;

public static partial class RepositoryMenus
{
    /// <summary>
    /// A row of the activity log (2.2): header the operation's title or the call's command, then
    /// <c>Copy command</c> for a git call, <c>Copy output</c> for a call with output, <c>Copy
    /// title</c> for an operation, each copying the text as the log shows it. No other action: the
    /// log only shows. Items that cannot apply are left out (D77).
    /// </summary>
    public static MenuModel ForActivityLogRow(RepositoryMenuContext context, ActivityLogMenuTarget row, IRepositoryActions actions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(actions);
        return Build(
            Header(row.Title ?? row.Command ?? string.Empty),
            [
                row.Command is { } command ? Item(actions, CopyCommand, () => actions.CopyTextAsync(command)) : null,
                row.Output is { Length: > 0 } output ? Item(actions, CopyOutput, () => actions.CopyTextAsync(output)) : null,
                row.Title is { } title ? Item(actions, CopyTitle, () => actions.CopyTextAsync(title)) : null,
            ]);
    }
}

/// <summary>A right-click on a row of the activity log (docs/phase-5-design.md 2.2): an operation's or a git call's.</summary>
/// <param name="Title">An operation's title; null for a call.</param>
/// <param name="Command">A call's command as the log shows it, credentials hidden; null for an operation.</param>
/// <param name="Output">A call's output as the log shows it (its standard output and error); null for an operation or a call without output.</param>
public sealed record ActivityLogMenuTarget(string? Title, string? Command, string? Output);
