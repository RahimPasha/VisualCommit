namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The names a screen reader reads for controls whose tooltip holds a key or names nothing, and
/// for those that had none (D435): set as local values in XAML, which beat <c>VcIconButton</c>'s
/// binding of the name to the tooltip. The tooltips, help texts and keys stay as they are; a key
/// is read from the control's accelerator key, never from its name. The rows' names are formats
/// over the row's path or the ref's full name, so that the buttons of two rows are told apart.
/// </summary>
public static class AutomationNames
{
    /// <summary>The resolver's <c>ResolverPreviousConflict</c>.</summary>
    public const string PreviousConflict = "Previous conflict";

    /// <summary>The resolver's <c>ResolverNextConflict</c>.</summary>
    public const string NextConflict = "Next conflict";

    /// <summary>The resolver's <c>ResolverPreviousFile</c>.</summary>
    public const string PreviousFile = "Previous file";

    /// <summary>The resolver's <c>ResolverNextFile</c>.</summary>
    public const string NextFile = "Next file";

    /// <summary>The resolver's <c>ResolverCloseButton</c>, whose tooltip reads only "Close (Esc)".</summary>
    public const string CloseResolver = "Close the resolver";

    /// <summary>The diff's <c>CloseDiffButton</c>, whose tooltip reads only "Close".</summary>
    public const string CloseDiff = "Close the diff";

    /// <summary>The search bar's <c>SearchPrevious</c>.</summary>
    public const string PreviousMatch = "Previous match";

    /// <summary>The search bar's <c>SearchNext</c>.</summary>
    public const string NextMatch = "Next match";

    /// <summary>The search bar's <c>SearchClose</c>.</summary>
    public const string CloseSearch = "Close the search";

    /// <summary>The activity log's <c>ActivityLogClose</c>.</summary>
    public const string CloseActivityLog = "Close the activity log";

    /// <summary>The comparison's <c>CompareClose</c>.</summary>
    public const string CloseComparison = "Close the comparison";

    /// <summary>The comparison's <c>CompareSwap</c>.</summary>
    public const string SwapFromAndTo = "Swap From and To";

    /// <summary>The restore bar's <c>DismissRestoreButton</c>, whose tooltip reads only "Close".</summary>
    public const string DismissRestoreOffer = "Dismiss the restore offer";

    /// <summary>The status bar's <c>ActivityLogToggle</c>, whose content is an icon and a text in a panel.</summary>
    public const string ActivityLog = "Activity log";

    /// <summary>The splitter <c>LeftSplitter</c>.</summary>
    public const string ResizeLeftPanel = "Resize the left panel";

    /// <summary>The splitter <c>RightSplitter</c>.</summary>
    public const string ResizeRightPanel = "Resize the right panel";

    /// <summary>The splitter <c>ActivityLogSplitter</c>.</summary>
    public const string ResizeActivityLog = "Resize the activity log";

    /// <summary>A stage row's <c>RowStageButton</c>, over the row's path: <c>Stage README.md</c>.</summary>
    public const string StageFileFormat = "Stage {0}";

    /// <summary>A stage row's <c>RowDiscardButton</c>, over the row's path.</summary>
    public const string DiscardFileFormat = "Discard {0}";

    /// <summary>A staged row's <c>RowUnstageButton</c>, over the row's path.</summary>
    public const string UnstageFileFormat = "Unstage {0}";

    /// <summary>A hidden branch's or remote's <c>RefHiddenToggle</c>, over its full name, as the menus word it: <c>Show origin/feature/alpha in the graph</c>.</summary>
    public const string ShowRefFormat = "Show {0} in the graph";

    /// <summary>A soloed branch's <c>RefSoloToggle</c>, over its full name.</summary>
    public const string StopSoloingRefFormat = "Stop soloing {0}";
}
