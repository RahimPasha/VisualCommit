using VisualCommit.App.ViewModels.Menus;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Phase 5's activity log menu (docs/phase-5-design.md 2.2, 13.1; phase 5's report, part A):
/// <see cref="RepositoryMenus.ForActivityLogRow"/>, which the log's rows open. The rows' targets
/// as the log makes them are <c>ActivityLogViewModelTests</c>', the menu opened by a right-click in
/// the window <c>ActivityLogViewTests</c>'.
/// </summary>
public partial class RepositoryMenusTests
{
    // --- The activity log's rows (phase 5, 2.2) ---------------------------------------------------

    [Fact]
    public void An_activity_log_rows_menu_copies_a_calls_command_and_output_or_an_operations_title_and_leaves_out_the_rest()
    {
        var context = Context(OpsLikeRefs());
        var (actions, recorder) = RecordingActions.Create();
        const string Merge = "git merge refs/heads/feature/theirs";
        const string Output = "Auto-merging notes.txt\nCONFLICT (content): Merge conflict in notes.txt";

        var operation = RepositoryMenus.ForActivityLogRow(context, new ActivityLogMenuTarget("Merge feature/theirs", null, null), actions);
        var call = RepositoryMenus.ForActivityLogRow(context, new ActivityLogMenuTarget(null, Merge, Output), actions);
        var quiet = RepositoryMenus.ForActivityLogRow(context, new ActivityLogMenuTarget(null, "git status --porcelain=v2", null), actions);
        var empty = RepositoryMenus.ForActivityLogRow(context, new ActivityLogMenuTarget(null, "git update-ref --stdin", string.Empty), actions);

        // Header the title or the command; an item that cannot apply is left out, not disabled
        // (D77); one group, no separator.
        Assert.Equal(["Merge feature/theirs", "Copy title"], Lines(operation));
        Assert.Equal([Merge, "Copy command", "Copy output"], Lines(call));
        Assert.Equal(["git status --porcelain=v2", "Copy command"], Lines(quiet));
        Assert.Equal(["git update-ref --stdin", "Copy command"], Lines(empty));
        Assert.Equal((58, 84), (Height(operation), Height(call)));

        // Each copies the text as the log shows it.
        InvokeAll(operation);
        InvokeAll(call);
        Assert.Equal(
            ["CopyTextAsync(Merge feature/theirs)", $"CopyTextAsync({Merge})", $"CopyTextAsync({Output})"],
            recorder.Calls.Select(recorded => recorded.ToString()));
    }
}
